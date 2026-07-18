using System.IO;

namespace AnimationEditor.Core.Update;

/// <summary>
/// Builds the PowerShell script that finishes a Windows self-update after the running app
/// exits — a process can't overwrite its own running exe, so the swap has to happen from a
/// separate process that waits for this one to close first. That process is this same
/// PowerShell script: it pops up a small WinForms status window (no separate compiled updater
/// app needed) that reports "waiting" / "installing" / "launching" so the user isn't staring
/// at nothing between the app closing and the new version appearing, then closes itself.
/// Kept as a pure string builder (no file/process I/O) so the exact commands are unit-testable.
/// </summary>
public static class SelfUpdateScriptBuilder
{
    public static string Build(int processId, string extractedDir, string installDir, string exeFileName, string workDirToCleanUp) =>
        $@"Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$form = New-Object System.Windows.Forms.Form
$form.Text = ""Animation Editor Update""
$form.Size = New-Object System.Drawing.Size(360, 120)
$form.StartPosition = ""CenterScreen""
$form.TopMost = $true
$form.FormBorderStyle = ""FixedDialog""
$form.ControlBox = $false
$form.MaximizeBox = $false
$form.MinimizeBox = $false

$label = New-Object System.Windows.Forms.Label
$label.Text = ""Waiting for Animation Editor to close...""
$label.AutoSize = $false
$label.Size = New-Object System.Drawing.Size(320, 24)
$label.Location = New-Object System.Drawing.Point(20, 20)
$form.Controls.Add($label)

$progress = New-Object System.Windows.Forms.ProgressBar
$progress.Style = ""Marquee""
$progress.MarqueeAnimationSpeed = 30
$progress.Size = New-Object System.Drawing.Size(320, 20)
$progress.Location = New-Object System.Drawing.Point(20, 55)
$form.Controls.Add($progress)

$form.Show()
$form.Refresh()

while (Get-Process -Id {processId} -ErrorAction SilentlyContinue) {{
    [System.Windows.Forms.Application]::DoEvents()
    Start-Sleep -Milliseconds 300
}}

$label.Text = ""Installing update...""
[System.Windows.Forms.Application]::DoEvents()
Copy-Item -Path ""{extractedDir}\*"" -Destination ""{installDir}"" -Recurse -Force

$label.Text = ""Launching Animation Editor...""
[System.Windows.Forms.Application]::DoEvents()
Start-Process -FilePath ""{Path.Combine(installDir, exeFileName)}""

$form.Close()
Remove-Item -Path ""{workDirToCleanUp}"" -Recurse -Force -ErrorAction SilentlyContinue
";
}
