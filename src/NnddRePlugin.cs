using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using NCV_Plugin;

namespace NnddRe
{
    /// <summary>
    /// NNDD-RE 連携プラグイン。
    /// NCV が放送に接続したとき、同じ放送を NNDD-RE の生放送プレイヤーで開く。
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
        public virtual string Description => "放送に接続したとき NNDD-RE の生放送プレイヤーでも同じ放送を開きます。";
        public virtual bool HasSettingForm => false;
        public virtual void ShowSettingForm() { }
        public virtual bool IsAutoRun => true;
        public virtual void Run() { }

        public virtual void AutoRun()
        {
            if (_host == null) return;
            _host.LiveConnected += (_, _) => OpenInNnddRe();
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
