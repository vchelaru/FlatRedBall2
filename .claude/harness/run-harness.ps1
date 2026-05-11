# run-harness.ps1 — Master entry point for the statusline worktree test harness.
#
# Usage:
#   pwsh -File run-harness.ps1 unit          — unit tests only (fast, no copilot instance)
#   pwsh -File run-harness.ps1 integration   — non-interactive integration test (copilot -p)
#   pwsh -File run-harness.ps1 tui           — TUI test (opens new terminal, captures status JSON)
#   pwsh -File run-harness.ps1 all           — run all three in sequence
#   pwsh -File run-harness.ps1 analyze       — analyze latest log files only

param(
    [ValidateSet('unit','integration','tui','all','analyze')]
    [string]$Mode = 'unit'
)

$here = $PSScriptRoot

function Run-Step([string]$title, [scriptblock]$block) {
    Write-Host "`n$('=' * 60)" -ForegroundColor Cyan
    Write-Host "  $title" -ForegroundColor Cyan
    Write-Host "$('=' * 60)`n" -ForegroundColor Cyan
    & $block
}

switch ($Mode) {
    'unit' {
        Run-Step "Unit tests" { & "$here\unit-test.ps1" }
    }
    'integration' {
        Run-Step "Non-interactive integration test" { & "$here\integration-noninteractive.ps1" }
    }
    'tui' {
        Run-Step "TUI integration test" { & "$here\integration-tui.ps1" }
    }
    'all' {
        Run-Step "Unit tests" { & "$here\unit-test.ps1" }
        Run-Step "Non-interactive integration test" { & "$here\integration-noninteractive.ps1" }
        Run-Step "TUI integration test" { & "$here\integration-tui.ps1" }
    }
    'analyze' {
        Run-Step "Log analysis" { & "$here\analyze.ps1" }
    }
}
