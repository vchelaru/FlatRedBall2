<#
.SYNOPSIS
    Tiny static file server (localhost only) for standalone experiment pages and published wwwroot folders.
.EXAMPLE
    pwsh tools/serve-static.ps1 -Root bin/Release/net10.0/publish/wwwroot -Port 5199
#>
param(
    [Parameter(Mandatory)][string]$Root,
    [int]$Port = 5199
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'measure-web.lib.ps1')

$Root = (Resolve-Path -LiteralPath $Root).Path
$listener = [Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
$listener.Start()
Write-Host "serving $Root on http://127.0.0.1:$Port/"
try {
    while ($listener.IsListening) {
        $ctx = $listener.GetContext()
        try {
            $file = Resolve-StaticPath -Root $Root -UrlPath $ctx.Request.RawUrl
            if (-not $file) { $ctx.Response.StatusCode = 404 }
            else {
                $bytes = [IO.File]::ReadAllBytes($file)
                $ctx.Response.ContentType = Get-StaticContentType $file
                $ctx.Response.ContentLength64 = $bytes.Length
                $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            }
        } catch { $ctx.Response.StatusCode = 500 }
        finally { $ctx.Response.Close() }
    }
} finally { $listener.Close() }
