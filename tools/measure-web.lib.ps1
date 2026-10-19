# Pure helpers for measure-web.ps1 and serve-static.ps1. Dot-sourced; no side effects on load.
# Tested by tools/measure-web.tests.ps1.

$script:AngleBackends = @{
    d3d11       = @('--use-gl=angle', '--use-angle=d3d11')
    swiftshader = @('--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader')
    gl          = @('--use-gl=angle', '--use-angle=gl')
}

# Chrome flags for a headless run whose console output lands on stderr.
function Get-BrowserArguments {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$ProfileDir,
        [ValidateSet('d3d11', 'swiftshader', 'gl')][string]$Angle = 'd3d11'
    )
    @(
        '--headless=new'
        '--enable-logging=stderr'
        '--v=0'
        "--user-data-dir=$ProfileDir"
        '--no-first-run'
        '--no-default-browser-check'
        '--disable-extensions'
        '--disable-background-networking'
        '--window-size=1280,720'
        '--autoplay-policy=no-user-gesture-required'
    ) + $script:AngleBackends[$Angle] + @($Url)
}

# Returns the page's console text from one Chrome stderr line, or $null for any other log line.
# Chrome format: [pid:tid:MMDD/HHMMSS.mmm:INFO:CONSOLE:line] "text", source: url (line)
function ConvertFrom-ChromeLogLine {
    param([string]$Line)
    if ($Line -match '^\[\d+:\d+:[\d/\.]+:\w+:CONSOLE(?::\d+)?\] "(?<text>.*)", source: .* \(\d+\)\s*$') {
        return $Matches['text']
    }
    if ($Line -match '^\[\d+:\d+:[\d/\.]+:\w+:CONSOLE(?::\d+)?\] (?<text>.*)$') {
        return $Matches['text']
    }
    return $null
}

# Chooses which processes this script owns. Never matches by image name alone, so the user's own
# browser (different --user-data-dir) and dev servers are never selected.
#   -Processes: objects with ProcessId, CommandLine (Windows Win32_Process shape).
function Select-OwnedProcess {
    param(
        [object[]]$Processes,
        [string]$ProfileDir,
        [string]$ProjectPath,
        [string]$ServerUrl
    )
    $profileFlag = if ($ProfileDir) { "--user-data-dir=$ProfileDir" } else { $null }
    $normalize = { param($s) ($s -replace '/', '\').TrimEnd('\') }
    $projNorm = if ($ProjectPath) { & $normalize $ProjectPath } else { $null }
    foreach ($p in $Processes) {
        $cmd = $p.CommandLine
        if (-not $cmd) { continue }
        $cmdNorm = $cmd -replace '/', '\'
        if ($profileFlag -and $cmdNorm.Contains(($profileFlag -replace '/', '\'))) { $p; continue }
        if ($projNorm -and $ServerUrl -and $cmdNorm.Contains($projNorm) -and $cmd.Contains($ServerUrl)) { $p; continue }
    }
}

# Parses a lock file's JSON; $null when missing or malformed.
function ConvertFrom-LockFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json } catch { $null }
}

# Maps a request path onto a file under Root. Returns $null for traversal or a missing file.
function Resolve-StaticPath {
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$UrlPath)
    $rel = [Uri]::UnescapeDataString(($UrlPath -split '\?')[0]).TrimStart('/', '\')
    if ($rel -eq '' -or $rel.EndsWith('/')) { $rel += 'index.html' }
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($rootFull, $rel))
    if (-not $full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) { return $null }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return $null }
    $full
}

function Get-StaticContentType {
    param([string]$Path)
    switch ([IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        '.html' { 'text/html; charset=utf-8' }
        '.js'   { 'text/javascript; charset=utf-8' }
        '.mjs'  { 'text/javascript; charset=utf-8' }
        '.css'  { 'text/css; charset=utf-8' }
        '.json' { 'application/json; charset=utf-8' }
        '.wasm' { 'application/wasm' }
        '.png'  { 'image/png' }
        '.jpg'  { 'image/jpeg' }
        '.svg'  { 'image/svg+xml' }
        '.wav'  { 'audio/wav' }
        '.ogg'  { 'audio/ogg' }
        '.mp3'  { 'audio/mpeg' }
        default { 'application/octet-stream' }
    }
}

# Processes in -Table (ProcessId, ParentProcessId) that descend from any of -RootPids, roots included.
function Get-ProcessTreeIds {
    param([object[]]$Table, [int[]]$RootPids)
    $found = [Collections.Generic.HashSet[int]]::new()
    foreach ($r in $RootPids) { [void]$found.Add($r) }
    do {
        $before = $found.Count
        foreach ($p in $Table) {
            if ($found.Contains([int]$p.ParentProcessId)) { [void]$found.Add([int]$p.ProcessId) }
        }
    } while ($found.Count -ne $before)
    $found
}

# Start-Process -ArgumentList joins with spaces and does not quote, so quote anything containing whitespace.
function ConvertTo-QuotedArgument {
    param([string[]]$Arguments)
    $Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
}
