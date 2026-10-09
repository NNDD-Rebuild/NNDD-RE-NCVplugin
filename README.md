# NNDD-RE-NCVplugin

[NNDD-RE](https://github.com/NNDD-Rebuild/NNDD-RE) 連携用の NCV (ニコ生コメントビューア) プラグイン。プラグイン名は `NNDD-RE`。

## 機能
- NCV が放送に接続したとき、同じ放送を NNDD-RE の生放送プレイヤーで開く。
  アドレスバーの `lv` / `co` / `ch` を使う (数字のみのユーザーID入力時は何もしない)。
- NNDD-RE 側の「設定 > 生放送 > NCV 連携」と合わせると、NNDD-RE で開いた放送を NCV でも開ける。
  相互起動ループは `nndd-re-cmd://live/<id>?from=ncv` で防いでいる。

## 必要なもの
- NCV α228 以上 (.NET 10 版)
- NNDD-RE (`nndd-re-cmd://` プロトコルが登録されていること。一度起動すれば登録される)

## インストール
1. Actions の最新 run の artifact `NNDD-RE` から `NNDD-RE.dll` を取得
2. NCV 実行ファイルのフォルダの `Plugins` に `NNDD-RE.dll` だけを置く
   (`NCV_Plugin.dll` / `NCV_Abstractions.dll` は置かない。プラグインとして読まれてエラーになる)
3. NCV を再起動

## ビルド
.NET 10 SDK (Windows) が必要。
```sh
./scripts/fetch-libs.sh        # NCV 付属 DLL を src/libs/ に取得
dotnet build src/NNDD-RE.csproj -c Release -o out
```
NCV 付属 DLL は再配布しないためリポジトリに含めず、公式サンプル ([mororomo/NCV-PluginSample-dotNET10](https://github.com/mororomo/NCV-PluginSample-dotNET10)) から取得する。
NCV の利用規約に従うこと。
