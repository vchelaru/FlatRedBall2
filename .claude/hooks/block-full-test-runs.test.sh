#!/bin/bash
# Tests for block-full-test-runs.sh. Pipes Bash-tool hook payloads into the hook and checks
# the exit code (0 = allowed, 2 = blocked). Run: bash .claude/hooks/block-full-test-runs.test.sh
# Payloads are JSON as Claude Code sends them, so \n, \" and \\ are JSON escapes.
hook="$(dirname "$0")/block-full-test-runs.sh"
failures=0

check() {
  expected=$1
  name=$2
  command_json=$3
  printf '{"session_id":"s","hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{"command":"%s","description":"d"}}' "$command_json" \
    | bash "$hook" 2>/dev/null
  actual=$?
  if [ "$actual" -eq "$expected" ]; then
    echo "PASS  $name"
  else
    echo "FAIL  $name (expected $expected, got $actual)"
    failures=$((failures + 1))
  fi
}

allowed=0
blocked=2

# dotnet test
check $blocked "engine suite, no filter" 'dotnet test tests/FlatRedBall2.Tests/'
check $blocked "engine csproj, no filter" 'dotnet test tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj --no-build'
check $blocked "AE root, no filter" 'cd tools/AnimationEditorAvalonia && dotnet test'
check $allowed "--filter class" 'dotnet test tests/FlatRedBall2.Tests/ --filter \"FullyQualifiedName~CircleTests\"'
check $allowed "--filter=value" 'dotnet test tests/FlatRedBall2.Tests/ --filter=CircleTests'
check $allowed "dotnet build" 'dotnet build src/FlatRedBall2.csproj'
check $allowed "FRB2_ALLOW_FULL_TESTS=1 prefix" 'FRB2_ALLOW_FULL_TESTS=1 dotnet test tests/FlatRedBall2.Tests/'
check $blocked "other env prefix" 'FOO=1 dotnet test tests/FlatRedBall2.Tests/'
check $blocked "after ;" 'echo hi; dotnet test'
check $blocked "after newline" 'echo hi\ndotnet test'
check $blocked "allow flag on a different command" 'FRB2_ALLOW_FULL_TESTS=1 echo hi && dotnet test'
check $blocked "filter on a different command" 'dotnet test && dotnet test x --filter YTests'
check $allowed "double-quoted string" 'git commit -m \"blocks dotnet test without filter\"'
check $allowed "single-quoted string" "echo 'run dotnet test here'"
check $allowed "heredoc body" 'cat > f.md <<'"'"'EOF'"'"'\nUse dotnet test with a filter.\ndotnet test\nEOF'
check $blocked "command after heredoc" 'cat > f.md <<EOF\nnotes\nEOF\ndotnet test'
check $allowed "dotnet test-like word" 'dotnet testx'
check $blocked "namespace filter" 'dotnet test tests/FlatRedBall2.Tests/ --filter \"FlatRedBall2.Tests.Collision\"'
check $blocked "namespace filter with property prefix" 'dotnet test tests/FlatRedBall2.Tests/ --filter \"FullyQualifiedName~FlatRedBall2.Tests.Collision\"'
check $blocked "bare word" 'dotnet test tests/FlatRedBall2.Tests/ --filter \"Collision\"'
check $blocked "one class and one bare word" 'dotnet test tests/FlatRedBall2.Tests/ --filter \"CircleTests|Collision\"'
check $allowed "namespace filter with FRB2_ALLOW_FULL_TESTS=1" 'FRB2_ALLOW_FULL_TESTS=1 dotnet test tests/FlatRedBall2.Tests/ --filter \"Collision\"'
check $allowed "qualified class" 'dotnet test tests/FlatRedBall2.Tests/ --filter \"FullyQualifiedName~FlatRedBall2.Tests.CircleTests\"'
check $allowed "qualified method" 'dotnet test tests/FlatRedBall2.Tests/ --filter \"FullyQualifiedName~CircleTests.Radius_Grows\"'
check $allowed "two classes" 'dotnet test tests/FlatRedBall2.Tests/ --filter \"CircleTests|AARectTests\"'
check $allowed "trait filter" 'dotnet test tests/FlatRedBall2.Tests/ --filter \"Category=Slow\"'

# scripts/test-ae.py
check $blocked "test-ae.py, no args" 'scripts/test-ae.py'
check $blocked "python test-ae.py, no args" 'python scripts/test-ae.py'
check $blocked "python3 test-ae.py --all" 'python3 scripts/test-ae.py --all'
check $blocked "python3 test-ae.py --no-build --all" 'python3 scripts/test-ae.py --no-build --all'
check $blocked "py launcher" 'py scripts/test-ae.py --all'
check $blocked "python.exe, quoted Windows path" 'python.exe \"F:\\GitHub\\FlatRedBall2\\scripts\\test-ae.py\" --all'
check $blocked "unquoted Windows path" 'python F:\\GitHub\\FlatRedBall2\\scripts\\test-ae.py --all'
check $blocked "PowerShell call operator" '& python .\\scripts\\test-ae.py --all'
check $blocked "after cd &&" 'cd x && ./scripts/test-ae.py --all'
check $blocked "test-ae.py bare-word filter" 'python scripts/test-ae.py --all --filter \"FullyQualifiedName~Dogfood\"'
check $blocked "test-ae.py namespace filter" 'python scripts/test-ae.py --all --filter \"AnimationEditor.App.Tests\"'
check $allowed "test-ae.py class filter" 'python scripts/test-ae.py --all --filter \"FullyQualifiedName~.GridTests.\"'
check $allowed "test-ae.py FRB2_ALLOW_FULL_TESTS=1" 'FRB2_ALLOW_FULL_TESTS=1 python scripts/test-ae.py --all'
check $allowed "test-ae.py named in a commit message" 'git commit -m \"document scripts/test-ae.py\"'
check $allowed "cat of test-ae.py" 'cat scripts/test-ae.py'
check $allowed "grep for test-ae.py" 'grep -rn test-ae scripts'

# the focused runner
check $allowed "run_tests.py class" 'python scripts/run_tests.py ButtonTests'
check $allowed "run_tests.py list" 'python scripts/run_tests.py --list ButtonTests,CircleTests'

echo
if [ "$failures" -eq 0 ]; then
  echo "All checks passed."
  exit 0
fi
echo "$failures check(s) failed."
exit 1
