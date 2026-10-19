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
    New-Item -ItemType Directory -Path "$root\sub" | Out-Null
    Set-Content "$root\index.html" 'a'; Set-Content "$root\sub\b.txt" 'b'; Set-Content "$root\..\frb-secret-$([IO.Path]::GetFileName($root)).txt" 's'
    try {
        Assert-Equal "$root\index.html" (Resolve-StaticPath -Root $root -UrlPath '/?diag=perf')
        Assert-Equal "$root\sub\b.txt" (Resolve-StaticPath -Root $root -UrlPath '/sub/b.txt')
        Assert-Equal $null (Resolve-StaticPath -Root $root -UrlPath "/../frb-secret-$([IO.Path]::GetFileName($root)).txt")
        Assert-Equal $null (Resolve-StaticPath -Root $root -UrlPath '/%2e%2e/x')
        Assert-Equal $null (Resolve-StaticPath -Root $root -UrlPath '/nope.js')
    } finally { Remove-Item $root, "$root\..\frb-secret-$([IO.Path]::GetFileName($root)).txt" -Recurse -Force -ErrorAction SilentlyContinue }
}
Test-Case 'content type: wasm is application/wasm' {
    Assert-Equal 'application/wasm' (Get-StaticContentType 'x.wasm')
}
Test-Case 'lock file: missing or malformed gives null' {
    Assert-Equal $null (ConvertFrom-LockFile 'C:\does\not\exist.lock')
    $f = New-TemporaryFile; Set-Content $f '{not json'
    try { Assert-Equal $null (ConvertFrom-LockFile $f) } finally { Remove-Item $f }
}

if ($script:failures) { Write-Host "$script:failures failed"; exit 1 }
Write-Host 'all passed'
