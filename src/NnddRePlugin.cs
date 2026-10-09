using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using NCV_Abstractions;
using NCV_Plugin;

namespace NnddRe
{
    /// <summary>
    /// NNDD-RE 連携プラグイン。
    /// - NCV が放送に接続したとき、同じ放送を NNDD-RE の生放送プレイヤーで開く。
    /// - コメントの右クリックメニューから、選択中コメントを NNDD-RE の NG リストに追加する。
    /// </summary>
    public class NnddRePlugin : IPlugin
    {
        private static readonly Regex LiveIdRe = new(@"\b(lv\d+|co\d+|ch\d+)\b", RegexOptions.Compiled);

        private IPluginHost? _host;

        public virtual IPluginHost? Host
        {
            get => _host;
            set => _host = value;
        }
        public virtual string Name => "NNDD-RE";
        public virtual string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        public virtual string Description => "放送に接続したとき NNDD-RE の生放送プレイヤーでも同じ放送を開きます。コメントの右クリックから NNDD-RE の NG リストにも追加できます。";
        public virtual bool HasSettingForm => true;
        public virtual bool IsAutoRun => true;
        public virtual void Run() { }

        public virtual void AutoRun()
        {
            if (_host == null) return;
            _launchEnabled = LoadLaunchEnabled();
            _host.LiveConnected += (_, _) =>
            {
                if (_launchEnabled) OpenInNnddRe();
            };
            AddNgMenu();
        }

        /// <summary>放送接続時に NNDD-RE でも開く (起動連携) か。既定は ON</summary>
        private bool _launchEnabled = true;

        private string SettingPath => Path.Combine(_host?.DirectoryPathAppSetting ?? "", "NNDD-RE.json");

        private bool LoadLaunchEnabled()
        {
            try
            {
                if (!File.Exists(SettingPath)) return true;
                using var doc = JsonDocument.Parse(File.ReadAllText(SettingPath));
                return !doc.RootElement.TryGetProperty("launchEnabled", out var v) || v.ValueKind != JsonValueKind.False;
            }
            catch
            {
                return true;
            }
        }

        private void SaveLaunchEnabled(bool enabled)
        {
            try
            {
                File.WriteAllText(SettingPath, JsonSerializer.Serialize(new { launchEnabled = enabled }));
            }
            catch (Exception ex)
            {
                _host?.ShowMessageToStatusLabel(this, $"NNDD-RE 連携の設定を保存できません: {ex.Message}", Color.Red);
            }
        }

        public virtual void ShowSettingForm()
        {
            using var form = new Form
            {
                Text = "NNDD-RE 連携の設定",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(380, 110)
            };
            var check = new CheckBox
            {
                Text = "放送に接続したとき NNDD-RE でも同じ放送を開く",
                Checked = _launchEnabled,
                AutoSize = true,
                Location = new Point(16, 16)
            };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(212, 66), Size = new Size(75, 28) };
            var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Location = new Point(293, 66), Size = new Size(75, 28) };
            form.Controls.AddRange(new Control[] { check, ok, cancel });
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            var owner = _host?.MainForm;
            if (form.ShowDialog(owner) != DialogResult.OK) return;
            _launchEnabled = check.Checked;
            SaveLaunchEnabled(_launchEnabled);
            _host?.ShowMessageToStatusLabel(this, _launchEnabled ? "NNDD-RE との起動連携: ON" : "NNDD-RE との起動連携: OFF");
        }

        /// <summary>NNDD-RE が受け付ける値の最大長 (超えると無視されるため送らない)</summary>
        private const int NgValueMaxLength = 500;

        /// <summary>コメント右クリックに「NNDD-RE の NG に追加」を足す</summary>
        private void AddNgMenu()
        {
            var root = new ToolStripMenuItem("NNDD-RE の NG に追加");
            root.DropDownItems.Add(NgItem("ユーザー (ID)", "userId", c => c.UserId));
            root.DropDownItems.Add(NgItem("コメント (部分一致)", "word", c => c.Content));
            root.DropDownItems.Add(NgItem("コメント (完全一致)", "wordExact", c => c.Content));
            root.DropDownItems.Add(NgItem("コマンド", "command", c => c.Mail));
            _host?.SetRightclickMenuItemsInCommentDGV(this, new[] { root });
        }

        private ToolStripMenuItem NgItem(string text, string type, Func<IChatInfo, string?> pick)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (_, _) =>
            {
                try
                {
                    var chat = _host?.GetSelectedCommentData();
                    var value = chat == null ? null : pick(chat)?.Trim();
                    if (string.IsNullOrEmpty(value))
                    {
                        _host?.ShowMessageToStatusLabel(this, "NG に追加できる値がありません");
                        return;
                    }
                    if (value.Length > NgValueMaxLength)
                    {
                        _host?.ShowMessageToStatusLabel(this, $"長すぎるため NG に追加できません ({NgValueMaxLength} 文字まで)");
                        return;
                    }
                    // 値に '/' を含んでも 3 区切り目として扱われるよう、必ずエンコードする
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"nndd-re-cmd://ngAdd/{type}/{Uri.EscapeDataString(value)}",
                        UseShellExecute = true
                    });
                    _host?.ShowMessageToStatusLabel(this, $"NNDD-RE の NG に送信しました: {text}");
                }
                catch (Exception ex)
                {
                    _host?.ShowMessageToStatusLabel(this, $"NNDD-RE 連携エラー: {ex.Message}", Color.Red);
                }
            };
            return item;
        }

        /// <summary>
        /// アドレスバー (tsComboBox_LiveNum) の入力から放送番号を取り、NNDD-RE で開く。
        /// IPluginHost に放送番号を返す API がないための代替。取れない場合 (数字のみのユーザーID等) は何もしない。
        /// </summary>
        private void OpenInNnddRe()
        {
            try
            {
                var form = _host?.MainForm;
                if (form == null) return;

                string? text = null;
                void Read()
                {
                    foreach (var strip in form.Controls.Find("toolStrip1", true).OfType<ToolStrip>())
                    {
                        if (strip.Items.Find("tsComboBox_LiveNum", true).FirstOrDefault() is ToolStripComboBox cb)
                            text = cb.Text;
                    }
                }
                if (form.InvokeRequired) form.Invoke(Read); else Read();

                var m = text == null ? null : LiveIdRe.Match(text);
                if (m == null || !m.Success) return;

                // from=ncv: NNDD-RE 側で NCV を再起動しないための目印 (相互起動ループ防止)
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"nndd-re-cmd://live/{m.Value}?from=ncv",
                    UseShellExecute = true
                });
                _host?.ShowMessageToStatusLabel(this, $"NNDD-RE で {m.Value} を開きました");
            }
            catch (Exception ex)
            {
                _host?.ShowMessageToStatusLabel(this, $"NNDD-RE 連携エラー: {ex.Message}", Color.Red);
            }
        }
    }
}
