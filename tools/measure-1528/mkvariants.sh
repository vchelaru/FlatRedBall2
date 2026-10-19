#!/bin/bash
export SP="C:/Users/VICPER~1/AppData/Local/Temp/claude/F--GitHub-FlatRedBall2/589312bc-2aa5-48fb-a2e2-d7c7b6af27fb/scratchpad"
W=/f/GitHub/FlatRedBall2/.claude/worktrees/gum-load-measure
OUT=$W/samples/Solitaire/Solitaire.Common/Content/GumProject
mkdir -p $SP/stage
for v in "$@"; do
  tag=$(echo $v | tr '+' '_')
  rm -rf $SP/stage/$tag; mkdir -p $SP/stage/$tag
  python3 $SP/variant2.py $v $SP/stage/$tag | tail -1
  mkdir -p $OUT/v_$tag
  (cd $W/samples/Solitaire/Solitaire.Common && dotnet gumcli pack "$SP/stage/$tag/GumProject.gumj" -o "$OUT/v_$tag/GumProject.gumpkg" 2>&1 | tail -1)
  ls -l $OUT/v_$tag/GumProject.gumpkg | awk '{print $5, $9}'
done
