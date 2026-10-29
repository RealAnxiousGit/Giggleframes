#!/usr/bin/env bash
# Usage: cmd.sh "<line>\n<line>" [wait-seconds]  -> sends dev commands and prints the mod's new log lines.
B="F:/SteamLibrary/steamapps/common/Giggleland/BepInEx"; L="$B/LogOutput.log"
n=$(wc -l < "$L"); printf "$1" > "$B/gigglelandperf.cmd"; sleep ${2:-3}
tail -n +$((n+1)) "$L" | grep -E 'Giggleframes|Giggleland Performance|Error|Exception' | cut -c1-260
