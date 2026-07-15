# Start Animation Editor (browser/WASM) on http://localhost:5420
# Usage: .\run-browser.ps1   (from this directory)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

Write-Host "Stopping stale AnimationEditor.Browser / WasmAppHost processes..."
Get-CimInstance Win32_Process |
    Where-Object { $_.CommandLine -like "*AnimationEditor.Browser*" -or $_.CommandLine -like "*WasmAppHost*" } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Write-Host "Clean rebuild (prevents stale wasm fingerprints)..."
dotnet clean -c Debug -v q
Remove-Item -Recurse -Force "bin","obj" -ErrorAction SilentlyContinue
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dotnetJs = Join-Path $here "bin\Debug\net10.0-browser\wwwroot\_framework\dotnet.js"
$coreWasm = Get-ChildItem (Join-Path $here "bin\Debug\net10.0-browser\wwwroot\_framework") -Filter "AnimationEditor.Core.*.wasm" | Select-Object -First 1
if (-not (Test-Path $dotnetJs) -or -not $coreWasm) {
    Write-Error "Build output missing under bin\Debug\net10.0-browser\wwwroot\_framework"
    exit 1
}
Write-Host "Built $($coreWasm.Name)"

Write-Host ""
Write-Host "Starting at http://localhost:5420/"
Write-Host "(Keep this terminal open. Ctrl+C stops the server → browser shows 'Failed to fetch'.)"
Write-Host ""
Write-Host "Wait for 'App url: http://localhost:5420/' below, THEN open or refresh Chrome."
Write-Host ""
Write-Host "IMPORTANT if you see a spinner or Console 404/SRI errors:"
Write-Host "  1. Close EVERY tab on localhost (any port)"
Write-Host "  2. Chrome DevTools -> Application -> Storage -> Clear site data"
Write-Host "  3. Open ONLY http://localhost:5420/"
Write-Host ""

dotnet run --no-build -c Debug --launch-profile AnimationEditor.Browser
