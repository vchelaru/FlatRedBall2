$appDir = Join-Path $PSScriptRoot "FRBDK\AnimationEditorAvalonia\src\AnimationEditor.App"
Start-Process wt -ArgumentList "--title `"Issue #115 - Animation Editor`" -d `"$appDir`""
