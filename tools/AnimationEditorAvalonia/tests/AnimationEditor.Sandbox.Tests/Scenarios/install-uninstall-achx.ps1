# Runs inside Windows Sandbox (Windows PowerShell 5.1) as the logon command. Installs Setup.exe,
# checks the .achx/.achj registration, uninstalls, checks cleanup, writes results.json, shuts down.
# Every check is recorded rather than thrown, so one boot reports every failure.
param([Parameter(Mandatory)] [string] $Folder)

$ErrorActionPreference = 'Stop'
$log = Join-Path $Folder 'scenario.log'
$results = New-Object System.Collections.Generic.List[object]

function Log($message) { Add-Content -Path $log -Value "$(Get-Date -Format o) $message" }
function Check($name, [bool] $passed, $detail) {
    $results.Add([pscustomobject]@{ name = $name; passed = $passed; detail = "$detail" })
    Log "$(if ($passed) { 'PASS' } else { 'FAIL' }) $name : $detail"
}
function RegValue($path, $name) {
    $item = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue
    if ($null -eq $item) { return $null }
    if ($name -eq '') { return $item.'(default)' }
    return $item.$name
}

$progId = 'FlatRedBall.AnimationEditor.achx'
$classes = 'HKCU:\Software\Classes'
$capabilities = 'HKCU:\Software\FlatRedBall\AnimationEditor\Capabilities'
$registeredApps = 'HKCU:\Software\RegisteredApplications'
$appName = 'FlatRedBall AnimationEditor'
$installRoot = Join-Path $env:LOCALAPPDATA 'FlatRedBall2.AnimationEditor'

# AssocQueryString returns the exe Explorer would launch on double-click, so it checks the merged
# association Windows actually uses, not just our raw keys.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Assoc {
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int AssocQueryString(int flags, int str, string assoc, string extra, StringBuilder outBuf, ref uint outLen);
    public static string Executable(string extension) {
        uint length = 1024;
        var buffer = new StringBuilder((int)length);
        // ASSOCSTR_EXECUTABLE = 2
        return AssocQueryString(0, 2, extension, null, buffer, ref length) == 0 ? buffer.ToString() : null;
    }
}
'@

try {
    Log 'Installing'
    $setup = Start-Process -FilePath (Join-Path $Folder 'Setup.exe') -ArgumentList '--silent' -Wait -PassThru
    Check 'setup-exit-code' ($setup.ExitCode -eq 0) "exit $($setup.ExitCode)"

    # Whether a silent Setup launches the editor is logged as a fact, then the editor is closed so it
    # doesn't hold files open during uninstall.
    Start-Sleep -Seconds 5
    $launched = @(Get-Process -Name 'AnimationEditor' -ErrorAction SilentlyContinue)
    Log "OBSERVED silent-setup-launched-editor: $($launched.Count -gt 0)"
    $launched | Stop-Process -Force

    $command = RegValue "$classes\$progId\shell\open\command" ''
    $exe = if ($command -match '^"([^"]+)"') { $Matches[1] } else { $null }
    Check 'open-command-targets-installed-exe' `
        ($exe -and $exe.StartsWith($installRoot, 'OrdinalIgnoreCase') -and (Test-Path $exe)) $command
    Check 'registered-applications' `
        ((RegValue $registeredApps $appName) -eq 'Software\FlatRedBall\AnimationEditor\Capabilities') `
        (RegValue $registeredApps $appName)

    foreach ($extension in '.achx', '.achj') {
        Check "capabilities-$extension" ((RegValue "$capabilities\FileAssociations" $extension) -eq $progId) `
            (RegValue "$capabilities\FileAssociations" $extension)
        Check "extension-default-$extension" ((RegValue "$classes\$extension" '') -eq $progId) `
            (RegValue "$classes\$extension" '')
        Check "open-with-progids-$extension" ($null -ne (RegValue "$classes\$extension\OpenWithProgids" $progId)) ''
        $resolved = [Assoc]::Executable($extension)
        Check "double-click-resolves-$extension" ($resolved -and $exe -and ($resolved -ieq $exe)) $resolved
    }

    # Simulate another app taking over .achj; uninstall must leave that mapping alone.
    Set-ItemProperty -Path "$classes\.achj" -Name '(default)' -Value 'Other.App.achj'

    Log 'Uninstalling'
    $update = Join-Path $installRoot 'Update.exe'
    Check 'update-exe-exists' (Test-Path $update) $update
    if (Test-Path $update) {
        $uninstall = Start-Process -FilePath $update -ArgumentList '--silent', 'uninstall' -Wait -PassThru
        Check 'uninstall-exit-code' ($uninstall.ExitCode -eq 0) "exit $($uninstall.ExitCode)"
    }

    Check 'uninstall-removes-progid' (-not (Test-Path "$classes\$progId")) ''
    Check 'uninstall-removes-capabilities' (-not (Test-Path 'HKCU:\Software\FlatRedBall\AnimationEditor')) ''
    Check 'uninstall-removes-registered-application' ($null -eq (RegValue $registeredApps $appName)) ''
    Check 'uninstall-clears-our-achx-default' ([string]::IsNullOrEmpty((RegValue "$classes\.achx" ''))) `
        (RegValue "$classes\.achx" '')
    Check 'uninstall-keeps-other-apps-achj-default' ((RegValue "$classes\.achj" '') -eq 'Other.App.achj') `
        (RegValue "$classes\.achj" '')
    Check 'uninstall-removes-open-with-progid' ($null -eq (RegValue "$classes\.achx\OpenWithProgids" $progId)) ''
}
catch {
    Check 'scenario-script' $false $_
}
finally {
    # Written last: the host treats the file's appearance as "done". Any failure here is logged
    # (the host shows scenario.log on timeout) and never skips the shutdown.
    try {
        Log "Writing $($results.Count) results"
        $json = ConvertTo-Json -InputObject $results.ToArray() -Depth 3
        [System.IO.File]::WriteAllText((Join-Path $Folder 'results.tmp'), $json)
        Move-Item -Path (Join-Path $Folder 'results.tmp') -Destination (Join-Path $Folder 'results.json') -Force
        Log 'Done; shutting down'
    }
    catch {
        Log "Failed writing results: $_"
    }
    shutdown.exe /s /t 0
}
