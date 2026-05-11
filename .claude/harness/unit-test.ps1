# Unit tests for ~/.copilot/statusline.ps1
# Verifies each detection path with synthetic JSON payloads and state files.
# Run: pwsh -NoProfile -File unit-test.ps1
# All tests are self-contained and clean up after themselves.

$statusScript = "$env:USERPROFILE\.copilot\statusline.ps1"
$stateDir     = "$env:USERPROFILE\.copilot\statusline-state"
$worktreeBase = "C:\Users\devin\OneDrive\Documents\Repos\FlatRedBall2\.claude\worktrees"

if (-not (Test-Path $statusScript)) {
    Write-Host "ERROR: statusline.ps1 not found at $statusScript" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $stateDir)) {
    New-Item -ItemType Directory -Path $stateDir -Force | Out-Null
}

$pass = 0; $fail = 0

function Invoke-StatusTest {
    param(
        [string]$Name,
        [hashtable]$Payload,
        [hashtable]$StateFiles  = @{},
        [string]$EnvSessionId   = $null,   # simulates COPILOT_AGENT_SESSION_ID in shell
        [string]$ExpectedMatch  = $null     # $null means expect empty output
    )

    # Write any state files
    foreach ($kv in $StateFiles.GetEnumerator()) {
        Set-Content -Path (Join-Path $stateDir $kv.Key) -Value $kv.Value -Encoding UTF8 -NoNewline
    }

    # Always snapshot and control COPILOT_AGENT_SESSION_ID so tests are hermetic.
    # If EnvSessionId is provided, set it; if not, clear it so ambient state files
    # from the current copilot session don't bleed into tests that expect empty output.
    $hadEnv = [bool]$env:COPILOT_AGENT_SESSION_ID
    $oldVal = $env:COPILOT_AGENT_SESSION_ID
    if ($EnvSessionId) {
        $env:COPILOT_AGENT_SESSION_ID = $EnvSessionId
    } else {
        Remove-Item Env:\COPILOT_AGENT_SESSION_ID -ErrorAction SilentlyContinue
    }

    try {
        $json   = $Payload | ConvertTo-Json -Depth 5
        $result = ($json | pwsh -NoProfile -File $statusScript) -join ''
    } finally {
        # Restore original env var value
        if ($hadEnv) { $env:COPILOT_AGENT_SESSION_ID = $oldVal }
        else          { Remove-Item Env:\COPILOT_AGENT_SESSION_ID -ErrorAction SilentlyContinue }
        foreach ($kv in $StateFiles.GetEnumerator()) {
            Remove-Item (Join-Path $stateDir $kv.Key) -ErrorAction SilentlyContinue
        }
    }

    $ok = if ($ExpectedMatch) { $result -like $ExpectedMatch } else { [string]::IsNullOrWhiteSpace($result) }
    if ($ok) {
        Write-Host "  PASS  $Name" -ForegroundColor Green
        $script:pass++
    } else {
        Write-Host "  FAIL  $Name" -ForegroundColor Red
        Write-Host "        expected: $(if ($ExpectedMatch) { $ExpectedMatch } else { '(empty)' })" -ForegroundColor Yellow
        Write-Host "        got:      $(if ($result) { $result } else { '(empty)' })" -ForegroundColor Yellow
        $script:fail++
    }
}

Write-Host "`n=== statusline.ps1 unit tests ===`n" -ForegroundColor Cyan

# --- Path 1: session cwd IS a worktree ---
Write-Host "Path 1 — session cwd is a worktree"

Invoke-StatusTest "workspace.current_dir points to worktree" `
    -Payload @{ context_window = @{ used_percentage = 42 }
                workspace      = @{ current_dir = "$worktreeBase\999-test-branch" } } `
    -ExpectedMatch "*worktree: 999-test-branch*"

Invoke-StatusTest "top-level cwd field points to worktree" `
    -Payload @{ cwd = "$worktreeBase\888-other-branch" } `
    -ExpectedMatch "*worktree: 888-other-branch*"

Invoke-StatusTest "non-worktree cwd → empty output" `
    -Payload @{ workspace = @{ current_dir = "C:\Users\devin\Documents" } } `
    -ExpectedMatch $null

# --- Path 2: state-file fallback via session_id in JSON ---
Write-Host "`nPath 2 — state-file fallback (session_id from JSON)"

Invoke-StatusTest "state file exists and contains worktree path" `
    -Payload @{ workspace  = @{ current_dir = "C:\Users\devin\Documents" }
                session_id = "json-session-abc" } `
    -StateFiles @{ "json-session-abc.txt" = "$worktreeBase\777-from-state" } `
    -ExpectedMatch "*worktree: 777-from-state*"

Invoke-StatusTest "state file exists but contains non-worktree path" `
    -Payload @{ workspace  = @{ current_dir = "C:\Users\devin\Documents" }
                session_id = "json-session-def" } `
    -StateFiles @{ "json-session-def.txt" = "C:\Users\devin\Documents" } `
    -ExpectedMatch $null

Invoke-StatusTest "session_id in JSON but no matching state file → empty" `
    -Payload @{ workspace  = @{ current_dir = "C:\Users\devin\Documents" }
                session_id = "no-such-session-xyz" } `
    -ExpectedMatch $null

# --- Path 3: state-file fallback via COPILOT_AGENT_SESSION_ID env var ---
Write-Host "`nPath 3 — state-file fallback (session_id from env var COPILOT_AGENT_SESSION_ID)"

Invoke-StatusTest "env var session ID matches state file with worktree" `
    -Payload @{ workspace = @{ current_dir = "C:\Users\devin\Documents" } } `
    -StateFiles @{ "env-session-111.txt" = "$worktreeBase\666-env-branch" } `
    -EnvSessionId "env-session-111" `
    -ExpectedMatch "*worktree: 666-env-branch*"

Invoke-StatusTest "env var set but no matching state file → empty" `
    -Payload @{ workspace = @{ current_dir = "C:\Users\devin\Documents" } } `
    -EnvSessionId "env-session-no-file" `
    -ExpectedMatch $null

# --- Path 4: JSON session_id takes priority over env var ---
Write-Host "`nPath 4 — JSON session_id takes priority over env var"

$sf4 = @{
    "json-wins.txt" = "$worktreeBase\555-json-branch"
    "env-wins.txt"  = "$worktreeBase\444-env-branch"
}
Invoke-StatusTest "JSON session_id wins over env var" `
    -Payload @{ workspace  = @{ current_dir = "C:\Users\devin\Documents" }
                session_id = "json-wins" } `
    -StateFiles $sf4 `
    -EnvSessionId "env-wins" `
    -ExpectedMatch "*worktree: 555-json-branch*"

# --- Path 5: agent writes state file explicitly (primary fix for no-profile shells) ---
Write-Host "`nPath 5 — agent explicitly writes state file (COPILOT_AGENT_SESSION_ID = session_id in JSON)"

# Simulates the fix: agent runs:
#   "$(Get-Location)" | Set-Content "$env:USERPROFILE\.copilot\statusline-state\$env:COPILOT_AGENT_SESSION_ID.txt"
# and the statusline JSON contains the matching session_id.
Invoke-StatusTest "agent-written state file detected via JSON session_id" `
    -Payload @{ workspace  = @{ current_dir = "C:\Users\devin\OneDrive\Documents\Repos\FlatRedBall2" }
                session_id = "agent-explicit-abc" } `
    -StateFiles @{ "agent-explicit-abc.txt" = "$worktreeBase\333-agent-wrote-this" } `
    -EnvSessionId "agent-explicit-abc" `
    -ExpectedMatch "*worktree: 333-agent-wrote-this*"

Invoke-StatusTest "agent-written state file detected via env var when JSON has no session_id" `
    -Payload @{ workspace = @{ current_dir = "C:\Users\devin\OneDrive\Documents\Repos\FlatRedBall2" } } `
    -StateFiles @{ "agent-env-only-abc.txt" = "$worktreeBase\222-agent-env-only" } `
    -EnvSessionId "agent-env-only-abc" `
    -ExpectedMatch "*worktree: 222-agent-env-only*"

# --- Edge cases ---
Write-Host "`nEdge cases"

Invoke-StatusTest "empty JSON → empty output" `
    -Payload @{} `
    -ExpectedMatch $null

Invoke-StatusTest "completely invalid JSON string" `
    -Payload @{ __raw = "not-json" } `
    -ExpectedMatch $null   # ConvertFrom-Json error → silently swallowed

# Summary
Write-Host "`n==================================="
$color = if ($fail -eq 0) { 'Green' } else { 'Red' }
Write-Host "$pass passed, $fail failed" -ForegroundColor $color

exit $fail
