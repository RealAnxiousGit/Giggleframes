#!/usr/bin/env bash
# Builds the release zip: BepInEx/plugins/Giggleframes/{Giggleframes.dll, README.txt, LICENSE.txt,
# CHANGELOG.txt, dxvk/{d3d11.dll, dxgi.dll, dxvk.conf, LICENSE}}. Run after a Release build and tools/get_dxvk.sh.
# Usage: package.sh "<version name>"   e.g. package.sh "Pre-Release 0.20"
cd "$(dirname "$0")/.."
name=${1:?version name}; file="Giggleframes-${name// /-}.zip"
st=artifacts/stage; out=artifacts/release
rm -rf "${st:?}"; mkdir -p "$st/BepInEx/plugins/Giggleframes/dxvk" "$out"
P="$st/BepInEx/plugins/Giggleframes"
cp artifacts/build/Giggleframes.dll "$P/"
cp LICENSE "$P/LICENSE.txt"; cp CHANGELOG.md "$P/CHANGELOG.txt"
cp dxvk/d3d11.dll dxvk/dxgi.dll dxvk/LICENSE res/dxvk.conf "$P/dxvk/"
ver=$(grep -o 'Version = "[0-9.]*"' src/Plugin.cs | cut -d'"' -f2)
cat > "$P/README.txt" <<TXT
Giggleframes - $name (plugin $ver)
Performance mod for Giggleland: adds Settings > Graphics > Advanced.

Needs BepInEx 6.0.0-be.791 Unity.IL2CPP win-x64, installed and started once.
Extract this zip into the Giggleland game folder (next to Giggleland.exe).
The plugin ends up at BepInEx/plugins/Giggleframes/Giggleframes.dll.

The dxvk folder holds DXVK 2.6.2 (https://github.com/doitsujin/dxvk, zlib license in dxvk/LICENSE)
for the Vulkan (DXVK) Render API option. The mod copies it next to the game only when you pick Vulkan.

Full instructions, features and source: https://github.com/RealAnxiousGit/Giggleframes
TXT
rm -f "$out/$file"
python -c "import os,sys,zipfile; z=zipfile.ZipFile(sys.argv[2],'w',zipfile.ZIP_DEFLATED); [z.write(os.path.join(r,f), os.path.relpath(os.path.join(r,f), sys.argv[1]).replace(os.sep,'/')) for r,_,fs in os.walk(sys.argv[1]) for f in sorted(fs)]; z.close()" "$st" "$out/$file"
unzip -l "$out/$file"
