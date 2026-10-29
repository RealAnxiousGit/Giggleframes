#!/usr/bin/env bash
# Busy-view A/B lab: faces the player at the busiest direction and benches the game with one
# experiment switched on at a time. Usage: busy_lab.sh <yaw> "<exp name value>;<exp name value>;..."
# Each entry is "on-command|off-command", e.g. "exp holdcam 0|exp holdcam 1". Needs DevCommands=true.
cd "$(dirname "$0")/.."
yaw=${1:?yaw}; IFS=';' read -ra exps <<< "$2"; secs=${3:-6}
b() { bash tools/cmd.sh "look $yaw\nbench $1 $secs\n" $((secs + 5)) | grep "\[bench\]" | sed 's/.*\[bench\] //'; }
echo "base:   $(b base)"
for e in "${exps[@]}"; do
  on=${e%%|*}; off=${e#*|}
  bash tools/cmd.sh "$on\n" 3 | grep -E "hidden|off$" | sed 's/.*\] /  /'
  echo "$on: $(b "${on// /_}")"
  [ "$off" != "$e" ] && bash tools/cmd.sh "$off\n" 3 >/dev/null
done
