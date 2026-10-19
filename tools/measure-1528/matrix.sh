#!/bin/bash
# usage: matrix.sh <mode: s|b> <reps> <variants...>   interleaved, shuffled per rep
SP="C:/Users/VICPER~1/AppData/Local/Temp/claude/F--GitHub-FlatRedBall2/589312bc-2aa5-48fb-a2e2-d7c7b6af27fb/scratchpad"
cd /f/GitHub/FlatRedBall2/.claude/worktrees/gum-load-measure
mode=$1; reps=$2; shift 2
for r in $(seq 1 $reps); do
  for v in $(printf '%s\n' "$@" | shuf); do
    if [ $mode = s ]; then q="?p=stages,fonts&pkg=$v"; stop='warm1 Initialize'; else q="?p=fonts&pkg=$v"; stop='S7 WatchContent'; fi
    for attempt in 1 2 3 4 5 6 7 8 9 10; do
      pwsh -NoProfile -File tools/measure-web.ps1 -Angle d3d11 -ProfileDir "$SP/prof" -Query "$q" -RunSeconds 40 -TimeoutSeconds 100 -StopPattern "$stop" -OutFile "$SP/out/m${mode}_$v.$r.txt" > /dev/null 2>&1
      ec=$?
      [ $ec -ne 1 ] && break
      sleep 15
    done
    echo "m${mode}_$v.$r exit=$ec attempt=$attempt"
  done
done
