# Run AnimationEditor.Browser UI-drive Playwright suite (#690).
# Starts the Browser host if AE_BROWSER_URL is unset, then runs npm test.
param(
    [string]$Url = $env:AE_BROWSER_URL,
    [switch]$SkipLaunch
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$uiDir = Join-Path $repoRoot "tools/AnimationEditorAvalonia/tests/AnimationEditor.Browser.Ui"
$browserProj = Join-Path $repoRoot "tools/AnimationEditorAvalonia/src/AnimationEditor.Browser"

if (-not (Test-Path (Join-Path $uiDir "node_modules"))) {
    Push-Location $uiDir
    npm install
    npm run install-browsers
    Pop-Location
}

$browserProc = $null
try {
    if (-not $SkipLaunch -and [string]::IsNullOrWhiteSpace($Url)) {
        $Url = "http://127.0.0.1:5420/"
        Write-Host "Launching AnimationEditor.Browser at $Url ..."
        $browserProc = Start-Process -PassThru -NoNewWindow -FilePath "dotnet" -ArgumentList @(
            "run", "--project", $browserProj, "--no-launch-profile", "--urls", "http://127.0.0.1:5420"
        )
        Start-Sleep -Seconds 25
    }

    if ([string]::IsNullOrWhiteSpace($Url)) {
        throw "Set AE_BROWSER_URL or omit -SkipLaunch so this script can start the Browser host."
    }

    $env:AE_BROWSER_URL = $Url
    Push-Location $uiDir
    npm test
    Pop-Location
}
finally {
    if ($browserProc -and -not $browserProc.HasExited) {
        Stop-Process -Id $browserProc.Id -Force -ErrorAction SilentlyContinue
    }
}
