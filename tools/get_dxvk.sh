#!/usr/bin/env bash
# Downloads the DXVK release used by the Vulkan option (2.6.2: the newest that runs on Vulkan 1.3 drivers
# without VK_KHR_maintenance5, such as 2023 Intel iGPU drivers; 3.x needs Vulkan 1.4) and puts its 64-bit d3d11.dll, dxgi.dll and
# LICENSE in dxvk/ (git-ignored). The build copies them into BepInEx/plugins/Giggleframes/dxvk.
cd "$(dirname "$0")/.."
V=2.6.2
SHA=17761876556afd55736cb895d184f5a1c55d43350f1b1e3b129f8d28706d7992
mkdir -p .downloads/dxvk dxvk
f=.downloads/dxvk/dxvk-$V.tar.gz
[ -f $f ] || curl -sL -o $f https://github.com/doitsujin/dxvk/releases/download/v$V/dxvk-$V.tar.gz
echo "$SHA  $f" | sha256sum -c - || { echo "checksum mismatch"; exit 1; }
tar -xzf $f -C .downloads/dxvk dxvk-$V/x64/d3d11.dll dxvk-$V/x64/dxgi.dll
cp .downloads/dxvk/dxvk-$V/x64/d3d11.dll .downloads/dxvk/dxvk-$V/x64/dxgi.dll dxvk/
curl -sL -o dxvk/LICENSE https://raw.githubusercontent.com/doitsujin/dxvk/v$V/LICENSE
ls -la dxvk
