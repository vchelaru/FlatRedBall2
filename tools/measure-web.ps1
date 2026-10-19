<#
.SYNOPSIS
    Headless measurement of a FlatRedBall2 web (BlazorGL) build: serve it, load it in a headless
    Chrome/Edge, and print the page's console output (e.g. the [frb-diag] line from ?diag=perf, or the
    StartupProfiler tree from EngineInitSettings.ProfileStartup).

.DESCRIPTION
    Safe to run on a working machine: one server and one browser at a time, below-normal priority, a
    hard time cap, a lock file against a second copy, and only processes this script started (matched by
    PID tree, the unique --user-data-dir, or project path + server URL) are ever killed. The user's own
    browser is never touched.

    Build the head first (the script serves with --no-build). After clearing wwwroot/Content, build twice.

.PARAMETER Project      Web head .csproj to serve with `dotnet run --no-build`. Default: Solitaire.BlazorGL.
.PARAMETER StaticDir    Serve this folder with tools/serve-static.ps1 instead (publish wwwroot, experiment pages).
.PARAMETER Query        Appended to the URL, e.g. '?diag=perf&frames=300'.
.PARAMETER StopPattern  Regex; stop as soon as a console line matches (e.g. '\[frb-diag\]'). Without it, runs to -RunSeconds.
.PARAMETER RunSeconds   How long to let the page run once the server is up. Default 30.
.PARAMETER TimeoutSeconds  Hard cap on the whole script, server startup included. Default 120.
.PARAMETER Angle        d3d11 (real GPU), swiftshader (software GL; inflates GPU time, not managed CPU time), gl.
.PARAMETER ProfileDir   Browser profile. Default: fresh temp dir (cold shader cache), deleted afterwards.
                        Pass a persistent dir to measure with a warm shader cache; it is kept.
.PARAMETER OutFile      Also write the captured console lines here.
.PARAMETER BrowserPath  Chrome/Edge executable; auto-detected otherwise.
.PARAMETER Port         Local port. Default 5199.
.PARAMETER KeepLogs     Keep the server/browser raw logs and print their folder (for debugging a silent run).

.EXAMPLE
    pwsh tools/measure-web.ps1 -Query '?diag=perf&frames=300' -StopPattern '\[frb-diag\]'
#>
param(
    [string]$Project = (Join-Path $PSScriptRoot '..\samples\Solitaire\Solitaire.BlazorGL\Solitaire.BlazorGL.csproj'),
    [string]$StaticDir,
    [string]$Query = '',
    [string]$StopPattern,
    [int]$RunSeconds = 30,
    [int]$TimeoutSeconds = 120,
    [ValidateSet('d3d11', 'swiftshader', 'gl')][string]$Angle = 'd3d11',
    [string]$ProfileDir,
    [string]$OutFile,
    [string]$BrowserPath,
    [int]$Port = 5199,
    [switch]$KeepLogs
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'measure-web.lib.ps1')

$onWindows = $env:OS -eq 'Windows_NT'
$lockPath = Join-Path ([IO.Path]::GetTempPath()) 'frb-measure-web.lock'
$serverUrl = "http://127.0.0.1:$Port"
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)

function Get-ProcessTable {
    if ($onWindows) {
        Get-CimInstance Win32_Process | ForEach-Object {
            [pscustomobject]@{ ProcessId = [int]$_.ProcessId; ParentProcessId = [int]$_.ParentProcessId; CommandLine = $_.CommandLine }
        }
    } else {
        ps -eo pid=, ppid=, args= | ForEach-Object {
            if ($_ -match '^\s*(\d+)\s+(\d+)\s+(.*)$') {
                [pscustomobject]@{ ProcessId = [int]$Matches[1]; ParentProcessId = [int]$Matches[2]; CommandLine = $Matches[3] }
            }
        }
    }
}

# Kills only what this script owns: the PID trees it started, plus anything carrying its unique
# profile dir or (project path + server URL). Never matches by process name alone.
function Stop-Owned {
    param([int[]]$RootPids, [string]$Profile, [string]$ProjectDir)
    $table = @(Get-ProcessTable)
    $ids = [Collections.Generic.HashSet[int]]::new()
    foreach ($id in (Get-ProcessTreeIds -Table $table -RootPids $RootPids)) { [void]$ids.Add($id) }
    foreach ($p in (Select-OwnedProcess -Processes $table -ProfileDir $Profile -ProjectPath $ProjectDir -ServerUrl $serverUrl)) { [void]$ids.Add($p.ProcessId) }
    foreach ($id in $ids) {
        if ($id -eq $PID) { continue }
        Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
    }
}

$projectDir = if ($StaticDir) { $null } else { Split-Path -Parent (Resolve-Path -LiteralPath $Project).Path }
$ownsProfile = -not $ProfileDir
if ($ownsProfile) { $ProfileDir = Join-Path ([IO.Path]::GetTempPath()) ("frb-measure-profile-" + [guid]::NewGuid().ToString('N').Substring(0, 8)) }
else { $ProfileDir = [IO.Path]::GetFullPath($ProfileDir) }

# Lock against a second copy; a stale lock (dead owner) is cleaned up, including its leftovers.
$stale = ConvertFrom-LockFile $lockPath
if ($stale) {
    if (Get-Process -Id $stale.pid -ErrorAction SilentlyContinue) { throw "Another measure-web run is active (PID $($stale.pid)); lock: $lockPath" }
    Stop-Owned -RootPids @() -Profile $stale.profileDir -ProjectDir $stale.projectDir
    Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
}

if (-not $BrowserPath) {
    $candidates = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
        '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'
        '/usr/bin/google-chrome'
        '/usr/bin/chromium'
    )
    $BrowserPath = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
    if (-not $BrowserPath) { throw 'No Chrome/Edge found; pass -BrowserPath.' }
}

$tag = [guid]::NewGuid().ToString('N').Substring(0, 8)
$work = Join-Path ([IO.Path]::GetTempPath()) "frb-measure-$tag"
New-Item -ItemType Directory -Path $work | Out-Null
$serverLog = Join-Path $work 'server.log'
$serverErr = Join-Path $work 'server.err'
$browserOut = Join-Path $work 'browser.out'
$browserErr = Join-Path $work 'browser.err'

$started = [Collections.Generic.List[int]]::new()
$oldPriority = [Diagnostics.Process]::GetCurrentProcess().PriorityClass
$captured = [Collections.Generic.List[string]]::new()
$exitCode = 0
try {
    @{ pid = $PID; profileDir = $ProfileDir; projectDir = $projectDir; port = $Port } | ConvertTo-Json | Set-Content -LiteralPath $lockPath

    # Children inherit this, which covers the browser's GPU/renderer processes spawned immediately.
    [Diagnostics.Process]::GetCurrentProcess().PriorityClass = 'BelowNormal'

    if ($StaticDir) {
        $serverArgs = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'serve-static.ps1'), '-Root', $StaticDir, '-Port', $Port)
        $server = Start-Process pwsh -ArgumentList (ConvertTo-QuotedArgument $serverArgs) -PassThru -WindowStyle Hidden -RedirectStandardOutput $serverLog -RedirectStandardError $serverErr
    } else {
        $serverArgs = @('run', '--project', $Project, '--no-build', '--no-launch-profile', '--', '--urls', $serverUrl)
        $server = Start-Process dotnet -ArgumentList (ConvertTo-QuotedArgument $serverArgs) -PassThru -WindowStyle Hidden -RedirectStandardOutput $serverLog -RedirectStandardError $serverErr
    }
    $started.Add($server.Id)

    $ready = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($server.HasExited) { throw "Server exited early ($($server.ExitCode)):`n$(Get-Content -LiteralPath $serverLog, $serverErr -ErrorAction SilentlyContinue | Out-String)" }
        try { if ((Invoke-WebRequest -Uri "$serverUrl/" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $ready = $true; break } } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw "Server not ready within $TimeoutSeconds s:`n$(Get-Content -LiteralPath $serverLog, $serverErr -ErrorAction SilentlyContinue | Out-String)" }

    $browserArgs = Get-BrowserArguments -Url "$serverUrl/$Query" -ProfileDir $ProfileDir -Angle $Angle
    $browser = Start-Process $BrowserPath -ArgumentList (ConvertTo-QuotedArgument $browserArgs) -PassThru -RedirectStandardOutput $browserOut -RedirectStandardError $browserErr
    $started.Add($browser.Id)

    $runEnd = [DateTime]::UtcNow.AddSeconds($RunSeconds)
    $reader = $null
    $matched = $false
    try {
        while ([DateTime]::UtcNow -lt $runEnd -and [DateTime]::UtcNow -lt $deadline -and -not $matched) {
            if (-not $reader -and (Test-Path -LiteralPath $browserErr)) {
                $fs = [IO.FileStream]::new($browserErr, 'Open', 'Read', 'ReadWrite,Delete')
                $reader = [IO.StreamReader]::new($fs)
            }
            while ($reader -and ($line = $reader.ReadLine()) -ne $null) {
                $text = ConvertFrom-ChromeLogLine $line
                if ($null -eq $text) { continue }
                $captured.Add($text)
                Write-Output $text
                if ($StopPattern -and $text -match $StopPattern) { $matched = $true; break }
            }
            Start-Sleep -Milliseconds 250
        }
    } finally { if ($reader) { $reader.Dispose() } }

    if ($StopPattern -and -not $matched) { Write-Warning "StopPattern '$StopPattern' never matched within the run window."; $exitCode = 2 }
    if ($captured.Count -eq 0) { Write-Warning 'No console output captured.' }
    if ($OutFile) { $captured | Set-Content -LiteralPath $OutFile }
}
finally {
    Stop-Owned -RootPids $started.ToArray() -Profile $ProfileDir -ProjectDir $projectDir
    [Diagnostics.Process]::GetCurrentProcess().PriorityClass = $oldPriority
    if ($ownsProfile) { Remove-Item -LiteralPath $ProfileDir -Recurse -Force -ErrorAction SilentlyContinue }
    if ($KeepLogs) { Write-Host "raw logs kept in $work" } else { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
    $lock = ConvertFrom-LockFile $lockPath
    if ($lock -and $lock.pid -eq $PID) { Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue }
}
exit $exitCode
