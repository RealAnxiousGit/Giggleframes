#!/usr/bin/env bash
# Performance lab. Starts the game on a chosen render API, loads the save with Continue, waits for
# the gameplay scene, then benches each preset in turn (alternating, several rounds) and appends the
# numbers to lab/results.md. Needs Debug.DevCommands = true in BepInEx/config/gigglelandperf.cfg.
#
# Usage: perf_lab.sh <dx11|dx12> [rounds=2] [seconds=10] [presets="Game Low-Spec Balanced"]
cd "$(dirname "$0")/.."
G="F:/SteamLibrary/steamapps/common/Giggleland"; B="$G/BepInEx"; L="$B/LogOutput.log"; C="$B/config/gigglelandperf.cfg"
api=${1:?dx11 or dx12}; rounds=${2:-2}; secs=${3:-10}; presets=${4:-"Game Low-Spec Balanced"}
case $api in dx11) want=Direct3D11;; dx12) want=Direct3D12;; *) echo "bad api"; exit 1;; esac

mkdir -p lab; cp "$C" lab/cfg.before   # restored when the lab finishes
started=$(bash tools/start_game.sh $api) || { echo "start failed: $started"; cp lab/cfg.before "$C"; exit 1; }
scene=$(tail -1 <<< "$started")

out=lab/results.md
[ -f $out ] || printf "# Perf lab results\n\n| time | API | scene | preset | avg fps | avg ms | 1%% low fps |\n|---|---|---|---|---|---|---|\n" > $out
echo "lab: $want, $scene"
for r in $(seq $rounds); do
  for p in $presets; do
    bash tools/cmd.sh "preset $p\n" 4 >/dev/null
    line=$(bash tools/cmd.sh "bench $p $secs\n" $((secs + 5)) | grep "\[bench\]")
    echo "  round $r $line"
    avg=$(sed -n 's/.*avg \([0-9.]*\) fps (\([0-9.]*\) ms), 1% low \([0-9.]*\) fps.*/\1|\2|\3/p' <<< "$line")
    [ -n "$avg" ] && echo "| $(date +%F' '%T) | $want | $scene | $p | ${avg//|/ | } |" >> $out
  done
done
bash tools/cmd.sh "shot lab-$api\n" 3 >/dev/null
P=$(tasklist //FI "IMAGENAME eq Giggleland.exe" //FO CSV //NH | grep -i giggle | cut -d'"' -f4); [ -n "$P" ] && taskkill //PID $P //F >/dev/null && sleep 3
cp lab/cfg.before "$C"
echo "results appended to $out; game closed, config restored"
