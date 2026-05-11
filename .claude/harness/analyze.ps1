# analyze.ps1 — Reads diagnostic log files and produces a diagnosis report.
# Run: pwsh -NoProfile -File analyze.ps1 [-LogDir <path>]

param(
    [string]$LogDir = "$env:USERPROFILE\.copilot\harness\logs"
)

$files = Get-ChildItem $LogDir -Filter '*.json' -Recurse -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime

if ($files.Count -eq 0) {
    Write-Host "No .json log files found in $LogDir" -ForegroundColor Red
    return
}

Write-Host "`n=== Status-line diagnostic report ===" -ForegroundColor Cyan
Write-Host "Files: $($files.Count)   Dir: $LogDir"

$worktreeSeen = $false

foreach ($f in $files) {
    $log = Get-Content $f.FullName -Raw | ConvertFrom-Json -ErrorAction SilentlyContinue
    if (-not $log) { Write-Host "  (could not parse $($f.Name))"; continue }

    Write-Host "`n--- $($f.Name) ---" -ForegroundColor Yellow

    # JSON payload fields
    $p = $log.parsed
    if ($p) {
        $keys = $p.PSObject.Properties.Name -join ', '
        Write-Host "  JSON keys: $keys"
        $cwd       = $p.workspace.current_dir ?? $p.cwd
        $sessionId = $p.session_id
        Write-Host "  session_id           : $(if ($sessionId) { $sessionId } else { '(absent)' })"
        Write-Host "  workspace.current_dir: $(if ($cwd) { $cwd } else { '(absent)' })"
        Write-Host "  context_window.used  : $($p.context_window.used_percentage)"
    } else {
        Write-Host "  raw payload: $($log.raw_json)" -ForegroundColor DarkGray
    }

    # Env vars
    Write-Host "  Env COPILOT_CLI              : $($log.env.COPILOT_CLI)"
    Write-Host "  Env COPILOT_AGENT_SESSION_ID : $(if ($log.env.COPILOT_AGENT_SESSION_ID) { $log.env.COPILOT_AGENT_SESSION_ID } else { '(absent)' })"
    Write-Host "  Env COPILOT_HOME             : $(if ($log.env.COPILOT_HOME) { $log.env.COPILOT_HOME } else { '(absent)' })"

    # State files
    $sf = @($log.state_dir_contents)
    if ($sf.Count -gt 0) {
        Write-Host "  State files ($($sf.Count)):"
        $sf | ForEach-Object { Write-Host "    $($_.name)  =>  $($_.content)" }
    } else {
        Write-Host "  State files: (none)"
    }

    # Diagnosis
    Write-Host "  --- Diagnosis ---" -ForegroundColor Cyan
    $sid = $p.session_id
    if (-not $sid) { $sid = $log.env.COPILOT_AGENT_SESSION_ID }

    if (-not $sid) {
        Write-Host "  !! No session ID available (JSON.session_id absent AND COPILOT_AGENT_SESSION_ID not set)" -ForegroundColor Red
        Write-Host "     State-file fallback is IMPOSSIBLE without a session ID." -ForegroundColor Red
    } else {
        Write-Host "  Session ID: $sid" -ForegroundColor Green
        $match = $sf | Where-Object { $_.name -eq "$sid.txt" }
        if ($match) {
            $content = $match.content
            if ($content -match '[\\/]\.claude[\\/]worktrees[\\/]([^\\/]+)') {
                Write-Host "  State file found + contains worktree: $($Matches[1])" -ForegroundColor Green
                Write-Host "  >> Status line SHOULD show: worktree: $($Matches[1])" -ForegroundColor Green
                $script:worktreeSeen = $true
            } else {
                Write-Host "  State file found but path is NOT a worktree: $content" -ForegroundColor Yellow
                Write-Host "  >> Agent hasn't cd'd into a worktree yet (or Set-Location hook didn't fire)" -ForegroundColor Yellow
            }
        } else {
            Write-Host "  No state file for session '$sid'" -ForegroundColor Red
            Write-Host "  Available state file names: $(($sf | Select-Object -Expand name) -join ', ')" -ForegroundColor Red
            Write-Host "  >> Set-Location hook may not be firing, or session ID mismatch" -ForegroundColor Red
        }
    }

    # Check cwd path for worktree
    if ($cwd -and $cwd -match '[\\/]\.claude[\\/]worktrees[\\/]([^\\/]+)') {
        Write-Host "  Session cwd IS a worktree: $($Matches[1])" -ForegroundColor Green
        Write-Host "  >> Status line SHOULD show: worktree: $($Matches[1]) (via session cwd)" -ForegroundColor Green
        $script:worktreeSeen = $true
    }
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
if ($worktreeSeen) {
    Write-Host "At least one log entry shows a working worktree detection path." -ForegroundColor Green
} else {
    Write-Host "No working worktree detection path found in any log entry." -ForegroundColor Red
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Run unit-test.ps1 to verify detection logic in isolation" -ForegroundColor Yellow
    Write-Host "  2. Run integration-noninteractive.ps1 to check if COPILOT_AGENT_SESSION_ID is set in shells" -ForegroundColor Yellow
    Write-Host "  3. Check if the JSON payload contains session_id" -ForegroundColor Yellow
}
