#!/bin/sh
# NCV 付属 DLL を公式サンプル (mororomo/NCV-PluginSample-dotNET10, MIT) から src/libs/ に取得する
set -e
tmp=$(mktemp -d)
git clone --depth 1 https://github.com/mororomo/NCV-PluginSample-dotNET10 "$tmp"
mkdir -p "$(dirname "$0")/../src/libs"
cp "$tmp"/NCV-PluginSample-dotNET10/libs/*.dll "$(dirname "$0")/../src/libs/"
rm -rf "$tmp"
