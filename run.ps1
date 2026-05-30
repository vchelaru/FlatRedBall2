$appDir = Join-Path $PSScriptRoot "tools\AnimationEditorAvalonia"
$wtArgs = "new-tab --title `"Issue #400 - AnimationEditor`" -d `"$appDir`""
Start-Process wt $wtArgs
