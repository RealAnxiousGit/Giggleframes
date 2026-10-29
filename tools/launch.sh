#!/usr/bin/env bash
# Usage: launch.sh [extra game args]  -> closes the game, starts it via Steam, waits for the mod's first render dump.
G="F:/SteamLibrary/steamapps/common/Giggleland"; B="$G/BepInEx"; L="$B/LogOutput.log"
P=$(tasklist //FI "IMAGENAME eq Giggleland.exe" //FO CSV //NH | grep -i giggle | cut -d'"' -f4); [ -n "$P" ] && taskkill //PID $P //F >/dev/null && sleep 3
rm -f "$B/gigglelandperf.cmd" "$L"
"/c/Program Files (x86)/Steam/steam.exe" -applaunch 3370490 "$@" >/dev/null 2>&1 &
for i in $(seq 1 300); do [ -f "$L" ] && grep -q "\[render\]" "$L" && break; sleep 1; done; sleep 5
tasklist | grep -qi giggleland && echo "game up" || echo "game NOT running"
