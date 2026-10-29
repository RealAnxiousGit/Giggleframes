#!/usr/bin/env bash
# Exit-to-main-menu stress test: Continue -> gameplay -> Main Menu, N times, watching for a crash.
# Usage: menu_cycle.sh <dx11|dx12> [cycles=4]. Needs DevCommands=true; the game must not be running.
cd "$(dirname "$0")/.."
G="F:/SteamLibrary/steamapps/common/Giggleland"; L="$G/BepInEx/LogOutput.log"
alive() { tasklist //FI "IMAGENAME eq Giggleland.exe" //NH | grep -qi giggle; }
bash tools/start_game.sh ${1:?api} >/dev/null || { echo "start failed"; exit 1; }
for c in $(seq ${2:-4}); do
  n=$(grep -c "scene loaded: MainMenu" "$L")
  bash tools/cmd.sh 'mainmenu\n' 3 >/dev/null
  for i in $(seq 60); do [ $(grep -c "scene loaded: MainMenu" "$L") -gt $n ] && break; alive || break; sleep 2; done
  sleep 20; alive || { echo "cycle $c: CRASHED after returning to the main menu"; exit 2; }
  echo "cycle $c: main menu ok"
  n=$(grep "scene loaded" "$L" | grep -vcE "MainMenu|LoadingScene")
  bash tools/cmd.sh 'menuclick 0 1\n' 3 >/dev/null
  for i in $(seq 90); do [ $(grep "scene loaded" "$L" | grep -vcE "MainMenu|LoadingScene") -gt $n ] && break; alive || break; sleep 2; done
  sleep 25; alive || { echo "cycle $c: CRASHED loading gameplay"; exit 2; }
  echo "cycle $c: gameplay ok"
done
echo "no crash in ${2:-4} cycles"
