#!/usr/bin/env bash
# Starts the game on a render API, loads the save with Continue and waits for the gameplay scene.
# Usage: start_game.sh <dx11|dx12|vk>   (prints the scene name on the last line)
cd "$(dirname "$0")/.."
G="F:/SteamLibrary/steamapps/common/Giggleland"; B="$G/BepInEx"; L="$B/LogOutput.log"; C="$B/config/gigglelandperf.cfg"
case $1 in dx11) idx=0; want="Direct3D11,";; dx12) idx=1; want=Direct3D12;; vk) idx=2; want="Direct3D11 via DXVK";; *) echo "dx11, dx12 or vk"; exit 1;; esac
# Saved API, then a normal Steam launch: the mod relaunches onto it if needed.
sed -i "s/^renderApi = .*/renderApi = $idx/; s/^RenderApiTries = .*/RenderApiTries = 0/; s/^DevCommands = .*/DevCommands = true/" "$C"
P=$(tasklist //FI "IMAGENAME eq Giggleland.exe" //FO CSV //NH | grep -i giggle | cut -d'"' -f4); [ -n "$P" ] && taskkill //PID $P //F >/dev/null && sleep 3
rm -f "$B/gigglelandperf.cmd" "$L"
"/c/Program Files (x86)/Steam/steam.exe" -applaunch 3370490 >/dev/null 2>&1 &
for i in $(seq 150); do grep -q "render API: running $want" "$L" 2>/dev/null && grep -q "MainMenu" "$L" && break; sleep 2; done
grep -q "render API: running $want" "$L" || { echo "game did not come up on $want"; exit 1; }
sleep 8

bash tools/cmd.sh 'menuclick 0 1\n' 3 >/dev/null
for i in $(seq 120); do grep "scene loaded" "$L" | grep -qvE "MainMenu|LoadingScene" && break; sleep 2; done
scene=$(grep "scene loaded" "$L" | grep -vE "MainMenu|LoadingScene" | tail -1 | sed 's/.*scene loaded: \([^ ]*\).*/\1/')
[ -n "$scene" ] || { echo "gameplay scene never loaded"; exit 1; }
sleep 15   # streaming and shader warm-up
echo "$scene"
