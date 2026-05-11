$appDir = Join-Path $PSScriptRoot "FRBDK\AnimationEditorAvalonia\src\AnimationEditor.App"
Start-Process wt -ArgumentList "new-tab --title `"Issue #150 - DI Migration`" -d `"$appDir`""
