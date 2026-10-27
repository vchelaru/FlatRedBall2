<#
.SYNOPSIS
    Build (Release), install, and run PremultiplyBench on an attached Android device, then pull the
    results into ./results as results-Android-<Label>.md/.csv.
.EXAMPLE
    pwsh diagnostics/PremultiplyBench/run-android.ps1 -Serial TULA2020091172 -Label slow
#>
param(
    [string]$Serial,
    [string]$Label = 'device',
    [int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
$adb = Join-Path $env:LOCALAPPDATA 'Android/Sdk/platform-tools/adb.exe'
$adbArgs = if ($Serial) { @('-s', $Serial) } else { @() }
$pkg = 'com.flatredball.premultiplybench'
$remote = "/sdcard/Android/data/$pkg/files"
$proj = Join-Path $PSScriptRoot 'PremultiplyBench.Android/PremultiplyBench.Android.csproj'
$results = Join-Path $PSScriptRoot 'results'

dotnet build $proj -c Release -v q
if ($LASTEXITCODE) { throw 'build failed' }
$apk = Get-ChildItem (Join-Path $PSScriptRoot 'PremultiplyBench.Android/bin/Release') -Recurse -Filter "$pkg-Signed.apk" | Select-Object -First 1
& $adb @adbArgs install -r $apk.FullName
& $adb @adbArgs shell am force-stop $pkg
& $adb @adbArgs shell rm -f "$remote/results-Android.done"
& $adb @adbArgs shell monkey -p $pkg -c android.intent.category.LAUNCHER 1 | Out-Null

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    $done = & $adb @adbArgs shell "ls $remote/results-Android.done 2>/dev/null"
    if ($done) { break }
    Start-Sleep -Seconds 5
    if (-not (& $adb @adbArgs shell pidof $pkg)) { & $adb @adbArgs logcat -d -b crash | Select-Object -Last 20; throw 'App is not running (crashed?)' }
}
if (-not $done) { throw "No results within $TimeoutSeconds s" }
New-Item -ItemType Directory -Force $results | Out-Null
foreach ($ext in 'md', 'csv') {
    $dst = Join-Path $results "results-Android-$Label.$ext"
    & $adb @adbArgs pull "$remote/results-Android.$ext" $dst | Out-Null
    (Get-Content $dst -Raw) -replace '## Android', "## Android ($Label)" -replace '(?m)^Android,', "Android-$Label," | Set-Content -NoNewline $dst
}
& $adb @adbArgs shell am force-stop $pkg
Get-Content (Join-Path $results "results-Android-$Label.md")
