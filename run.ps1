$appDir = Join-Path $PSScriptRoot "FRBDK\AnimationEditorAvalonia\src\AnimationEditor.App"
Start-Process wt -ArgumentList "new-tab --title `"Issue #131 - AnimationEditor`" -d `"$appDir`""
