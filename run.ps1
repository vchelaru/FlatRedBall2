$appDir = Join-Path $PSScriptRoot "tools\AnimationEditorAvalonia\src\AnimationEditor.App"
Start-Process wt -ArgumentList "new-tab --title `"Issue #175 - AnimationEditor`" -d `"$appDir`""
