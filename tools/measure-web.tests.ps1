# Plain-PowerShell tests for tools/measure-web.lib.ps1 (no Pester dependency).
#   pwsh tools/measure-web.tests.ps1      exits non-zero on any failure
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'measure-web.lib.ps1')

$script:failures = 0
function Test-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; Write-Host "ok   $Name" }
    catch { $script:failures++; Write-Host "FAIL $Name : $($_.Exception.Message)" }
}
function Assert-Equal($Expected, $Actual) {
    if (-not ($Expected -ceq $Actual) -and ($Expected | Out-String) -ne ($Actual | Out-String)) {
        throw "expected <$Expected> but got <$Actual>"
    }
}
function Assert-True($Value, [string]$Message = 'expected true') { if (-not $Value) { throw $Message } }

Test-Case 'browser args: swiftshader adds the unsafe-swiftshader flag' {
    $a = Get-BrowserArguments -Url 'http://x/' -ProfileDir 'C:\p' -Angle swiftshader
    Assert-True ($a -contains '--use-angle=swiftshader')
    Assert-True ($a -contains '--enable-unsafe-swiftshader')
    Assert-True ($a -contains '--enable-logging=stderr')
    Assert-True ($a -contains '--user-data-dir=C:\p')
    Assert-Equal 'http://x/' $a[-1]
}
Test-Case 'browser args: d3d11 has no swiftshader flags' {
    $a = Get-BrowserArguments -Url 'http://x/' -ProfileDir 'C:\p' -Angle d3d11
    Assert-True ($a -contains '--use-angle=d3d11')
    Assert-True (-not ($a -contains '--enable-unsafe-swiftshader'))
}
Test-Case 'quoting: only arguments containing whitespace are quoted' {
    $q = @(ConvertTo-QuotedArgument @('--user-data-dir=C:\Vic Personal\p', '--v=0'))
    Assert-Equal '"--user-data-dir=C:\Vic Personal\p"' $q[0]
    Assert-Equal '--v=0' $q[1]
}
Test-Case 'log line: extracts console text' {
    $l = '[1234:5678:1019/101010.123:INFO:CONSOLE:42] "[frb-diag] {""fps"":60}", source: http://127.0.0.1:5199/x.js (42)'
    Assert-Equal '[frb-diag] {""fps"":60}' (ConvertFrom-ChromeLogLine $l)
}
Test-Case 'log line: non-console lines are ignored' {
    Assert-Equal $null (ConvertFrom-ChromeLogLine '[1234:5678:1019/101010.123:ERROR:gpu_process_host.cc(1)] GPU died')
}
Test-Case 'owned: selects only the unique profile dir, not another browser' {
    $procs = @(
        [pscustomobject]@{ ProcessId = 1; CommandLine = '"chrome.exe" --headless=new --user-data-dir=C:\t\frb-measure-profile-ab12 http://x' }
        [pscustomobject]@{ ProcessId = 2; CommandLine = '"chrome.exe" --type=renderer --user-data-dir=C:\t\frb-measure-profile-ab12' }
        [pscustomobject]@{ ProcessId = 3; CommandLine = '"chrome.exe" --user-data-dir=C:\Users\me\AppData\Local\Google\Chrome\User Data' }
        [pscustomobject]@{ ProcessId = 4; CommandLine = '"chrome.exe"' }
        [pscustomobject]@{ ProcessId = 5; CommandLine = $null }
    )
    $ids = @(Select-OwnedProcess -Processes $procs -ProfileDir 'C:\t\frb-measure-profile-ab12' | ForEach-Object ProcessId)
    Assert-Equal '1 2' ($ids -join ' ')
}
Test-Case 'owned: project server needs project path AND server url' {
    $procs = @(
        [pscustomobject]@{ ProcessId = 10; CommandLine = 'dotnet exec F:\r\Solitaire.BlazorGL\bin\Debug\x.dll --urls http://127.0.0.1:5199' }
        [pscustomobject]@{ ProcessId = 11; CommandLine = 'dotnet exec F:\r\Solitaire.BlazorGL\bin\Debug\x.dll --urls http://127.0.0.1:50511' }
        [pscustomobject]@{ ProcessId = 12; CommandLine = 'dotnet exec F:\other\x.dll --urls http://127.0.0.1:5199' }
    )
    $ids = @(Select-OwnedProcess -Processes $procs -ProjectPath 'F:\r\Solitaire.BlazorGL' -ServerUrl 'http://127.0.0.1:5199' | ForEach-Object ProcessId)
    Assert-Equal '10' ($ids -join ' ')
}
Test-Case 'owned: with no profile/project given, nothing is selected' {
    $procs = @([pscustomobject]@{ ProcessId = 1; CommandLine = 'chrome.exe --user-data-dir=C:\x' })
    Assert-Equal 0 @(Select-OwnedProcess -Processes $procs).Count
}
Test-Case 'tree: includes descendants of roots, not siblings' {
    $t = @(
        [pscustomobject]@{ ProcessId = 2; ParentProcessId = 1 }
        [pscustomobject]@{ ProcessId = 3; ParentProcessId = 2 }
        [pscustomobject]@{ ProcessId = 4; ParentProcessId = 99 }
    )
    $ids = @(Get-ProcessTreeIds -Table $t -RootPids @(1) | Sort-Object)
    Assert-Equal '1 2 3' ($ids -join ' ')
}
Test-Case 'static path: resolves index, blocks traversal and missing files' {
    $root = Join-Path ([IO.Path]::GetTempPath()) ("frb-static-test-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $root 'sub') | Out-Null
    Set-Content (Join-Path $root 'index.html') 'a'; Set-Content (Join-Path (Join-Path $root 'sub') 'b.txt') 'b'; Set-Content "$root/../frb-secret-$([IO.Path]::GetFileName($root)).txt" 's'
    try {
        Assert-Equal (Join-Path $root 'index.html') (Resolve-StaticPath -Root $root -UrlPath '/?diag=perf')
        Assert-Equal (Join-Path (Join-Path $root 'sub') 'b.txt') (Resolve-StaticPath -Root $root -UrlPath '/sub/b.txt')
        Assert-Equal $null (Resolve-StaticPath -Root $root -UrlPath "/../frb-secret-$([IO.Path]::GetFileName($root)).txt")
        Assert-Equal $null (Resolve-StaticPath -Root $root -UrlPath '/%2e%2e/x')
        Assert-Equal $null (Resolve-StaticPath -Root $root -UrlPath '/nope.js')
    } finally { Remove-Item $root, "$root/../frb-secret-$([IO.Path]::GetFileName($root)).txt" -Recurse -Force -ErrorAction SilentlyContinue }
}
Test-Case 'content type: wasm is application/wasm' {
    Assert-Equal 'application/wasm' (Get-StaticContentType 'x.wasm')
}
Test-Case 'lock file: missing or malformed gives null' {
    Assert-Equal $null (ConvertFrom-LockFile 'C:\does\not\exist.lock')
    $f = New-TemporaryFile; Set-Content $f '{not json'
    try { Assert-Equal $null (ConvertFrom-LockFile $f) } finally { Remove-Item $f }
}

Test-Case 'browser args: metal selects the ANGLE metal backend' {
    $a = Get-BrowserArguments -Url 'http://x/' -ProfileDir '/p' -Angle metal
    Assert-True ($a -contains '--use-angle=metal')
    Assert-True (-not ($a -contains '--enable-unsafe-swiftshader'))
}
Test-Case 'angle: measure-web.ps1 accepts exactly the backends the library defines' {
    $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'measure-web.ps1'), [ref]$null, [ref]$null)
    $param = $ast.ParamBlock.Parameters | Where-Object { $_.Name.VariablePath.UserPath -eq 'Angle' }
    $set = $param.Attributes | Where-Object { $_.TypeName.Name -eq 'ValidateSet' }
    $values = @($set.PositionalArguments | ForEach-Object { $_.Value } | Sort-Object)
    Assert-Equal (($script:AngleBackends.Keys | Sort-Object) -join ' ') ($values -join ' ')
}
Test-Case 'ps arguments: BSD-compatible form (no -e, command not args)' {
    $a = Get-PsArguments
    Assert-True ($a -contains '-axo')
    Assert-True ($a -contains 'pid=,ppid=,command=')
    Assert-True (-not ($a -contains '-eo'))
}
Test-Case 'ps output: parses macOS BSD ps lines, including paths with spaces' {
    $lines = @(
        '    1     0 /sbin/launchd'
        '  501     1 /Applications/Google Chrome.app/Contents/MacOS/Google Chrome --headless=new --user-data-dir=/var/folders/x/frb-measure-profile-ab12 http://127.0.0.1:5199/'
        ' 9999   501 /Applications/Google Chrome.app/Contents/Frameworks/Helper --type=renderer'
        ''
        'garbage'
    )
    $t = @(ConvertFrom-PsOutput $lines)
    Assert-Equal 3 $t.Count
    Assert-Equal 501 $t[1].ProcessId
    Assert-Equal 1 $t[1].ParentProcessId
    Assert-True ($t[1].CommandLine.StartsWith('/Applications/Google Chrome.app'))
    Assert-Equal '501 9999' ((Get-ProcessTreeIds -Table $t -RootPids @(501) | Sort-Object) -join ' ')
    Assert-Equal '501' (@(Select-OwnedProcess -Processes $t -ProfileDir '/var/folders/x/frb-measure-profile-ab12' | Select-Object -First 1 | ForEach-Object ProcessId) -join ' ')
}
Test-Case 'priority: a refused change is a warning and returns false, not an error' {
    $p = [pscustomobject]@{}
    $p | Add-Member -MemberType ScriptProperty -Name PriorityClass -Value { 'Normal' } -SecondValue { throw 'Permission denied' }
    $result = Set-ProcessPriorityBestEffort $p 'BelowNormal' -WarningVariable w -WarningAction SilentlyContinue
    Assert-Equal $false $result
    Assert-True ("$w" -match 'Permission denied') 'expected a warning mentioning the cause'
}
Test-Case 'priority: an accepted change returns true' {
    $p = [pscustomobject]@{ PriorityClass = 'Normal' }
    Assert-Equal $true (Set-ProcessPriorityBestEffort $p 'BelowNormal')
    Assert-Equal 'BelowNormal' $p.PriorityClass
}
Test-Case 'hidden window: only requested on Windows' {
    Assert-Equal 'Hidden' (Get-HiddenWindowArguments -OnWindows $true).WindowStyle
    Assert-Equal 0 (Get-HiddenWindowArguments -OnWindows $false).Count
}
Test-Case 'cleanup: lock file and work dir are removed when the run fails' {
    $tmp = [IO.Path]::GetTempPath()
    $lock = Join-Path $tmp 'frb-measure-web.lock'
    if (Test-Path -LiteralPath $lock) { Write-Host '     (skipped: another measure-web run holds the lock)'; return }
    $site = Join-Path $tmp ("frb-cleanup-site-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $site | Out-Null
    Set-Content (Join-Path $site 'index.html') 'ok'
    $before = @(Get-ChildItem -LiteralPath $tmp -Filter 'frb-measure-*' | ForEach-Object Name)
    try {
        # Server starts, then launching the (nonexistent) browser fails: the finally block must still clean up.
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'measure-web.ps1') -StaticDir $site -BrowserPath (Join-Path $site 'no-such-browser') -Port 5197 -TimeoutSeconds 30 *> $null
        Assert-True ($LASTEXITCODE -ne 0) 'the run should have failed'
        Assert-True (-not (Test-Path -LiteralPath $lock)) 'lock file left behind'
        $after = @(Get-ChildItem -LiteralPath $tmp -Filter 'frb-measure-*' | ForEach-Object Name)
        Assert-Equal '' ((Compare-Object $before $after | ForEach-Object InputObject) -join ' ')
    } finally { Remove-Item -LiteralPath $site -Recurse -Force -ErrorAction SilentlyContinue }
}

Test-Case 'cleanup steps: a throwing step warns and does not skip the steps after it' {
    $ran = [Collections.Generic.List[string]]::new()
    Invoke-CleanupSteps @(
        @{ Name = 'kill';   Action = { $ran.Add('kill'); throw 'ps blew up' } }
        @{ Name = 'lock';   Action = { $ran.Add('lock') } }
        @{ Name = 'tmp';    Action = { $ran.Add('tmp') } }
    ) -WarningVariable w -WarningAction SilentlyContinue
    Assert-Equal 'kill lock tmp' ($ran -join ' ')
    Assert-True ("$w" -match 'kill' -and "$w" -match 'ps blew up') 'expected a warning naming the step and cause'
}
Test-Case 'cleanup steps: measure-web.ps1 runs its whole finally block through Invoke-CleanupSteps' {
    $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'measure-web.ps1'), [ref]$null, [ref]$null)
    $try = $ast.FindAll({ param($n) $n -is [Management.Automation.Language.TryStatementAst] }, $true) | Where-Object { $_.Finally } | Select-Object -First 1
    $statements = @($try.Finally.Statements)
    Assert-Equal 1 $statements.Count
    Assert-True ($statements[0].Extent.Text -match '^Invoke-CleanupSteps') 'finally must only call Invoke-CleanupSteps'
}

if ($script:failures) { Write-Host "$script:failures failed"; exit 1 }
Write-Host 'all passed'
