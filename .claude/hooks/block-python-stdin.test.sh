#!/bin/bash
# Tests for block-python-stdin.sh. Run: bash .claude/hooks/block-python-stdin.test.sh
hook="$(dirname "$0")/block-python-stdin.sh"
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

check $blocked "python3 - heredoc" "python3 - <<'EOF'\nprint(1)\nEOF"
check $blocked "python - heredoc" "python - <<EOF\nprint(1)\nEOF"
check $blocked "cd then python3 -" 'cd x && python3 - 2>/dev/null || echo nopy'
check $blocked "piped into python3 -" 'cat a.py | python3 -'
check $blocked "python3 -I -" 'python3 -I - <<EOF\nx\nEOF'
check $blocked "heredoc straight after interpreter" "python3 <<'EOF'\nprint(1)\nEOF"
check $blocked "python.exe -" 'python.exe - < a.py'
check $blocked "python3.14 -" 'python3.14 -'
check $blocked "python - at the start of a later line" "cd x\npython - <<'E' 2>/dev/null || true\nE\ngit status"
check $blocked "python - at the start of a later line, from nothing" 'S=a\npython - 2>/dev/null </dev/null; ls'
check $blocked "python - on a later indented line" 'if true; then\n  python -\nfi'
check $blocked "python - ending a line" 'python -\nls'
check $blocked "python - after timeout" "timeout 60 python - <<EOF\nx\nEOF"
check $blocked "python - then a separator" 'python -; ls'
check $blocked "python - then a pipe" 'python -| head'
check $blocked "python heredoc without a space" 'python<<EOF\nx\nEOF'
check $blocked "python -X utf8 -" 'python -X utf8 - <<EOF\nx\nEOF'
check $blocked "python -u heredoc" 'python -u <<EOF\nx\nEOF'
check $blocked "python with a path" '/usr/bin/python3 - <<EOF\nx\nEOF'
check $blocked "quoted python with a Windows path" '\"C:\\\\Python\\\\python.exe\" - <<EOF\nx\nEOF'
check $blocked "py -" 'py - <<EOF\nx\nEOF'
check $blocked "py -3 -" 'py -3 -'
check $blocked "node heredoc" "node <<'EOF'\nx\nEOF"
check $blocked "bash heredoc" "bash <<'EOF'\nx\nEOF"
check $blocked "node - on a later line" 'cd x\nnode - a'
check $blocked "bare python" 'cd x; python'
check $blocked "bare python on a later line" 'cd x\ntimeout 60 python3\nls'
check $allowed "which python" 'which python; command -v python3'
check $allowed "python fed by a pipe" 'cat a.py | python3'
check $allowed "py -3 script" 'py -3 tools/x.py'
check $allowed "python -I script" 'cd x\npython -I tools/x.py - b'
check $allowed "python -m on a later line" 'cd x\npython -m json.tool < a.json'
check $allowed "python -c on a later line" 'cd x\npython -c \"print(1)\"'
check $allowed "python --version" 'python --version'
check $allowed "python -X utf8 script" 'python -X utf8 tools/x.py'
check $allowed "bash script" 'bash tools/x.sh -v\nsh y.sh'
check $allowed "heredoc into a file named after an interpreter" "cat > x.py <<'E'\nprint(1)\nE\npython x.py"
check $allowed "ssh, .sh and .py with a dash" 'ls a.py - b.sh - ; git log -- a.sh'
check $allowed "node -e" 'node -e \"1\"'
check $allowed "script file" 'python3 tools/x.py --flag'
check $allowed "python -c" 'python3 -c \"print(1)\"'
check $allowed "python -m" 'python3 -m json.tool a.json'
check $allowed "no python" 'dotnet build Gum.slnx'
check $allowed "a flag named like python" 'echo --python-flag - x'
check $allowed "pip install" 'pip install -r requirements.txt'
check $blocked "bare cat > file after cd" 'cd x && cat > /tmp/null 2>&1; node -v'
check $blocked "bare cat > file at start" 'cat > out.txt'
check $blocked "bare cat >> file" 'cd x; cat >> out.txt'
check $blocked "bare cat > var path with stderr redirect, a < later in the command" 'cd x && cat > $TMP/fix.txt 2>/dev/null; sed -i \"64a <Compile Include=a />\" b.csproj'
check $allowed "cat with heredoc" "cat > out.txt <<'EOF'\nhello\nEOF"
check $allowed "cat file to file" 'cat a.txt > out.txt'
check $allowed "piped into cat" 'echo hi | cat > out.txt'
check $allowed "cat with input redirect" 'cat > out.txt < in.txt'
check $allowed "cat only" 'cat a.txt b.txt'

if [ "$failures" -ne 0 ]; then
  echo "$failures failure(s)"
  exit 1
fi
echo "all passed"
