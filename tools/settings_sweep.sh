#!/usr/bin/env bash
# Checks that every Advanced option really changes the game: in a running gameplay scene (start_game.sh
# first), sets each option to each of its values, logs a probe of everything the options write, and
# reports any option whose values all give the same probe. Puts every option back afterwards.
# Usage: settings_sweep.sh [option ...]   (default: all but graphicsPreset and renderApi)
cd "$(dirname "$0")/.."
B="F:/SteamLibrary/steamapps/common/Giggleland/BepInEx"; C="$B/config/gigglelandperf.cfg"; L="$B/LogOutput.log"
out=lab/settings-sweep.txt; : > "$out"
keys=${*:-$(grep -E '^[a-zA-Z]+ = [0-9]+$' "$C" | sed -n '/^graphicsPreset/,/^fpsCounter/p' | cut -d' ' -f1 | grep -vE '^(graphicsPreset|renderApi)$')}
for k in $keys; do
  orig=$(grep "^$k = " "$C" | cut -d' ' -f3)
  range=$(awk -v k="$k" '/Acceptable value range/{r=$0} $1==k{print r; exit}' "$C" | grep -o '[0-9]\+' | tr '\n' ' ')
  set -- $range; lo=$1; hi=$2
  for v in $(seq "$lo" "$hi"); do
    bash tools/cmd.sh "set $k $v\n" 2.5 >/dev/null
    bash tools/cmd.sh "probe $k=$v\n" 1.5 >/dev/null
    grep -o "probe $k=$v: .*" "$L" | tail -1 >> "$out"
  done
  bash tools/cmd.sh "set $k $orig\n" 1.5 >/dev/null
  tasklist | grep -qi giggleland || { echo "GAME GONE during $k"; exit 1; }
done
python - "$out" <<'EOF'
import sys, re, collections
by = collections.OrderedDict()
for line in open(sys.argv[1], encoding='utf-8'):
    m = re.match(r'probe (\w+)=(\d+): (.*)', line.strip())
    if m: by.setdefault(m[1], []).append((int(m[2]), m[3].split()))
for k, rows in by.items():
    base = rows[0][1]
    def diff(a, b): return [y for x, y in zip(a, b) if x != y]
    changes = [f"{v}: {' '.join(diff(base, p)) or '(same as ' + str(rows[0][0]) + ')'}" for v, p in rows[1:]]
    distinct = len({' '.join(p) for _, p in rows})
    print(f"{'OK  ' if distinct > 1 else 'FAIL'} {k} ({distinct} distinct of {len(rows)})")
    for c in changes: print("      " + c[:230])
EOF
