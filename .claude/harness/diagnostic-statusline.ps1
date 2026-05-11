# Diagnostic drop-in replacement for ~/.copilot/statusline.ps1
# Logs the complete JSON payload + env vars + git state to a timestamped file,
# then runs the same worktree-detection logic and emits the normal status output.
#
# Activate:  set statusLine.command in ~/.copilot/settings.json to point to
#            diagnostic-statusline.cmd (the .cmd wrapper for this script).
# Deactivate: restore the original statusLine.command value.

$raw = [Console]::In.ReadToEnd()

# --- Logging ---
$logDir = Join-Path (Split-Path $PSCommandPath) 'logs'
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$logFile   = Join-Path $logDir "$timestamp.json"

$stateDir   = Join-Path $env:USERPROFILE '.copilot\statusline-state'
$stateFiles = if (Test-Path $stateDir) {
    Get-ChildItem $stateDir -ErrorAction SilentlyContinue | ForEach-Object {
        [ordered]@{
            name    = $_.Name
            content = (Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue)
        }
    }
} else { @() }

$parsed = $raw | ConvertFrom-Json -ErrorAction SilentlyContinue

[ordered]@{
    timestamp          = $timestamp
    raw_json           = $raw
    parsed             = $parsed
    env                = [ordered]@{
        COPILOT_CLI              = $env:COPILOT_CLI
        COPILOT_AGENT_SESSION_ID = $env:COPILOT_AGENT_SESSION_ID
        COPILOT_HOME             = $env:COPILOT_HOME
        PWD                      = (Get-Location).Path
    }
    git                = [ordered]@{
        git_dir       = (git rev-parse --git-dir 2>$null)
        branch        = (git rev-parse --abbrev-ref HEAD 2>$null)
        worktree_list = @(git worktree list 2>$null)
    }
    state_dir_contents = @($stateFiles)
} | ConvertTo-Json -Depth 10 | Out-File $logFile -Encoding utf8

# --- Same worktree-detection logic as statusline.ps1 ---
function Get-WorktreeName([string]$path) {
    if ($path -match '[\\/]\.claude[\\/]worktrees[\\/]([^\\/]+)') { $Matches[1] } else { $null }
}

$cwd = $parsed.workspace.current_dir
if (-not $cwd) { $cwd = $parsed.cwd }

$wt = Get-WorktreeName $cwd
if (-not $wt) {
    $sessionId = $parsed.session_id
    if (-not $sessionId) { $sessionId = $env:COPILOT_AGENT_SESSION_ID }
    if ($sessionId) {
        $stateFile = Join-Path $stateDir "$sessionId.txt"
        if (Test-Path -LiteralPath $stateFile) {
            $shellCwd = Get-Content -LiteralPath $stateFile -Raw -ErrorAction SilentlyContinue
            $wt = Get-WorktreeName $shellCwd.Trim()
        }
    }
}

if ($wt) { "worktree: $wt" }
