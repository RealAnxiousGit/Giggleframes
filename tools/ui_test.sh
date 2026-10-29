#!/usr/bin/env bash
# Full settings-menu walk through the game's own navigation. Rows are picked by key (select <key>),
# never by counting key presses. Prints each step; stops with GAME GONE on a crash.
cd "$(dirname "$0")/.."
alive(){ tasklist | grep -qi giggleland || { echo "!!! GAME GONE after: $1"; exit 1; }; }
step(){ bash tools/cmd.sh "$1" ${2:-4} | grep -vE 'dev> (nav|shot)'; alive "$1"; }
bash tools/launch.sh
step 'menuclick 3 1\n'; step 'settingsclick 2\n'
step 'select calibrate\nsubmit\n' 5; step 'calib cancel\n' 5; step 'visible\nshot t1-after-calib-cancel\n' 3
step 'select calibrate\nsubmit\n' 5; step 'calib confirm\n' 5; step 'visible\n' 2
step 'select advanced\nshot t2-advanced-row\nsubmit\n' 5; step 'visible\nshot t3-advanced\n' 3
step 'select Back\nsubmit\n'; step 'visible\n' 2
step 'select advanced\nsubmit\n'; step 'back\n'; step 'visible\n' 2
step 'select advanced\nsubmit\n'; step 'select renderApi\nadjust 1\n' 4; step 'popup\nshot t4-restart-popup\n' 3
step 'popup later\n' 3; step 'visible\n' 2
step 'select renderApi\nadjust -1\n' 4; step 'popup\n' 2
# Drop-downs: Submit opens, Navigate moves, Submit picks, Back closes without leaving the page.
step 'select graphicsPreset\nsubmit\n'; step 'nav 1\nshot t5-preset-list\nsubmit\n'; step 'visible\n' 2
step 'select shadowQuality\nsubmit\n'; step 'back\n'; step 'visible\n' 2
step 'rowclick drawDistance\n'; step 'itemclick 3\n'; step 'select graphicsPreset\nsubmit\n'; step 'itemclick 3\n'
step 'back\n'; step 'select reset\nsubmit\n' 5; step 'popup\nshot t6-reset\n' 3; step 'popup later\n' 3
step 'back\n' 5; step 'visible\nshot t7-categories\n' 3
step 'settingsclick 4\n' 5; step 'shot t8-main\n' 3
echo "ALIVE: full walk done"
