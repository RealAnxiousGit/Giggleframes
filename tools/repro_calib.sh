#!/usr/bin/env bash
# Usage: repro_calib.sh "<SkipPatches value>"  -> prints ALIVE or GONE after the calibration + Revert sequence.
G="F:/SteamLibrary/steamapps/common/Giggleland"; B="$G/BepInEx"; L="$B/LogOutput.log"; C="$B/config/gigglelandperf.cfg"
P=$(tasklist //FI "IMAGENAME eq Giggleland.exe" //FO CSV //NH | grep -i giggle | cut -d'"' -f4); [ -n "$P" ] && taskkill //PID $P //F >/dev/null && sleep 3
rm -f "$B/gigglelandperf.cmd"
sed -i "s/^SkipPatches = .*/SkipPatches = $1/" "$C"; grep -q '^SkipPatches' "$C" || printf '\n[Debug]\nSkipPatches = %s\n' "$1" >> "$C"
rm -f "$L"; "/c/Program Files (x86)/Steam/steam.exe" -applaunch 3370490 >/dev/null 2>&1 &
for i in $(seq 1 300); do [ -f "$L" ] && grep -q "\[render\]" "$L" && break; sleep 1; done; sleep 6
c(){ printf "$1" > "$B/gigglelandperf.cmd"; sleep ${2:-3}; }
c 'menuclick 3 1\n' 4; c 'settingsclick 2\n' 4
c "$(for i in $(seq 1 13); do printf 'nav 1\n'; done)" 4; c 'submit\n' 5
c "$(for i in $(seq 1 22); do printf 'nav 1\n'; done)" 5; c 'submit\n' 10
tasklist | grep -qi giggleland && echo "ALIVE ($1)" || echo "GONE ($1)"
grep -cE 'Calibration\] Open' "$LOCALAPPDATA/../LocalLow/Hooked/Giggleland/Player.log" | sed 's/^/calibration opens: /'
grep -c 'Revert()' "$LOCALAPPDATA/../LocalLow/Hooked/Giggleland/Player.log" | sed 's/^/revert frames: /'
