#!/usr/bin/env bash
# Busy view + facing-away view on one API with one preset. Usage: api_bench.sh <dx11|dx12|vk> [preset] [rounds]
cd "$(dirname "$0")/.."
api=${1:?api}; p=${2:-Low-Spec}; rounds=${3:-2}
[ "$NOSTART" = 1 ] || bash tools/start_game.sh $api >/dev/null || { echo "start failed"; exit 1; }
bash tools/cmd.sh "preset $p\n" 9 >/dev/null
for r in $(seq $rounds); do
  for y in 50 230; do
    echo "$api $p yaw $y: $(bash tools/cmd.sh "look $y\nbench ${api}_$y 6\n" 12 | grep '\[bench\]' | sed 's/.*\] //')"
  done
done
