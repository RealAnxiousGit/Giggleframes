#!/usr/bin/env bash
# Finds which Advanced setting hangs the GPU when changed live on DirectX 12. Starts on DX12 with the
# game's defaults, changes one setting at a time to its Low-Spec value and checks the game still answers.
# Usage: bisect_dx12.sh [key=value ...]   (default: every setting the Low-Spec preset touches)
cd "$(dirname "$0")/.."
B="F:/SteamLibrary/steamapps/common/Giggleland/BepInEx"; L="$B/LogOutput.log"; C="$B/config/gigglelandperf.cfg"
steps=${*:-"upscaling=2 volumetricFog=2 volumetricLights=2 shadowQuality=2 shadowCascades=2 localShadows=1 lightingQuality=2 ambientOcclusion=1 reflections=1 postQuality=1 lensFlares=1 depthOfField=1 lodBias=2 vegetation=1 textureBudget=2"}
mkdir -p lab; cp "$C" lab/cfg.before
alive(){ bash tools/cmd.sh 'scene\n' 6 | grep -q "scene:"; }
P=$(tasklist //FI "IMAGENAME eq Giggleland.exe" //FO CSV //NH | grep -i giggle | cut -d'"' -f4); [ -n "$P" ] && taskkill //PID $P //F >/dev/null && sleep 3
# Start from the game's own values so each step is a single change.
for k in $steps; do sed -i "s/^${k%%=*} = .*/${k%%=*} = 0/" "$C"; done
bash tools/start_game.sh dx12 >/dev/null || { echo "start failed"; cp lab/cfg.before "$C"; exit 1; }
alive || { echo "not answering before any change"; exit 1; }
for k in $steps; do
  bash tools/cmd.sh "set ${k%%=*} ${k#*=}\n" 2 >/dev/null; sleep 8
  if alive; then echo "ok    $k"; else echo "HUNG  $k"; grep -m3 -E "887a|removed" "$LOCALAPPDATA/../LocalLow/Hooked/Giggleland/Player.log"; break; fi
done
P=$(tasklist //FI "IMAGENAME eq Giggleland.exe" //FO CSV //NH | grep -i giggle | cut -d'"' -f4); [ -n "$P" ] && taskkill //PID $P //F >/dev/null && sleep 3
cp lab/cfg.before "$C"
