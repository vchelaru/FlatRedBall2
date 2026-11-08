#!/bin/bash
# PreToolUse(Bash|PowerShell): refuses broad test runs. Agents run only the tests for the code they
# changed (`python scripts/run_tests.py <Class>`); CI runs the full suites. Blocked:
#  - `dotnet test` without --filter, or whose --filter names a namespace or bare word instead of a test class
#  - `scripts/test-ae.py` (the whole-AE-suite runner) without such a class --filter
# A command prefixed with FRB2_ALLOW_FULL_TESTS=1 passes, for the rare case the user asks for a full run.
#
# Only a command that runs counts: at the start of the command, after ; & | or a newline, or after
# NAME=value prefixes. Text inside quoted strings and heredoc bodies is ignored. Tests:
# block-full-test-runs.test.sh. Parses the hook JSON with awk so it needs no jq (portable to macOS
# bash 3.2 / BSD awk and Git Bash on Windows).
input=$(cat)

# Fast path: most commands never mention dotnet or test-ae.
case "$input" in
  *dotnet*|*test-ae*) ;;
  *) exit 0 ;;
esac

printf '%s' "$input" | awk -v sq="'" '
{ buf = buf (NR > 1 ? "\n" : "") $0 }

END {
  # Pull the tool_input.command JSON string out and decode its escapes.
  i = index(buf, "\"command\"")
  if (i == 0) exit 0
  rest = substr(buf, i + 9)
  if (!match(rest, /^[ \t\r\n]*:[ \t\r\n]*"/)) exit 0
  rest = substr(rest, RLENGTH + 1)
  n = length(rest)
  cmd = ""
  for (p = 1; p <= n; p++) {
    c = substr(rest, p, 1)
    if (c == "\"") break
    if (c == "\\") {
      p++
      e = substr(rest, p, 1)
      if (e == "n") cmd = cmd "\n"
      else if (e == "t") cmd = cmd "\t"
      else if (e == "r") cmd = cmd "\r"
      else if (e == "u") { cmd = cmd "?"; p += 4 }
      else cmd = cmd e
    } else cmd = cmd c
  }

  # Drop quoted strings and heredoc bodies, keep everything else. Each quoted string leaves a
  # \001N\002 token behind and its text is kept in qv[N], so a quoted --filter value or script path can
  # still be read.
  out = ""
  state = "normal"
  delim = ""
  nq = 0
  qbuf = ""
  n = length(cmd)
  for (p = 1; p <= n; p++) {
    c = substr(cmd, p, 1)
    if (state == "squote") {
      if (c == sq) { state = "normal"; qv[++nq] = qbuf; out = out "\001" nq "\002" }
      else qbuf = qbuf c
      continue
    }
    if (state == "dquote") {
      if (c == "\\") { p++; qbuf = qbuf substr(cmd, p, 1) }
      else if (c == "\"") { state = "normal"; qv[++nq] = qbuf; out = out "\001" nq "\002" }
      else qbuf = qbuf c
      continue
    }
    if (c == "\\") {
      # Before a letter or digit it is a Windows path separator; otherwise it escapes the next character.
      if (substr(cmd, p + 1, 1) ~ /[A-Za-z0-9_.]/) { out = out "/"; continue }
      p++; out = out "_"; continue
    }
    if (c == sq) { state = "squote"; qbuf = ""; continue }
    if (c == "\"") { state = "dquote"; qbuf = ""; continue }
    if (substr(cmd, p, 2) == "<<" && substr(cmd, p + 2, 1) != "<") {
      # Heredoc operator: read the delimiter word, minus any quoting.
      q = p + 2
      if (substr(cmd, q, 1) == "-") q++
      while (substr(cmd, q, 1) == " " || substr(cmd, q, 1) == "\t") q++
      d = ""
      while (q <= n) {
        dc = substr(cmd, q, 1)
        if (dc ~ /[ \t\n;&|<>()]/) break
        if (dc != sq && dc != "\"" && dc != "\\") d = d dc
        q++
      }
      if (d != "") delim = d
      p = q - 1
      out = out " "
      continue
    }
    if (c == "\n" && delim != "") {
      # Skip the heredoc body through its terminator line.
      p++
      while (p <= n) {
        e = index(substr(cmd, p), "\n")
        line = (e == 0) ? substr(cmd, p) : substr(cmd, p, e - 1)
        p = (e == 0) ? n + 1 : p + e
        sub(/^[ \t]+/, "", line)
        if (line == delim) break
      }
      p--
      delim = ""
      out = out "\n"
      continue
    }
    out = out c
  }

  # Each segment between ; & | and newlines is one command.
  segCount = split(out, segs, /[;&|\n]/)
  for (s = 1; s <= segCount; s++) {
    seg = segs[s]
    sub(/^[ \t\r({!]+/, "", seg)
    allowed = 0
    while (match(seg, /^[A-Za-z_][A-Za-z0-9_]*=[^ \t]*[ \t]+/)) {
      if (substr(seg, 1, RLENGTH) ~ /^FRB2_ALLOW_FULL_TESTS=1[ \t]/) allowed = 1
      seg = substr(seg, RLENGTH + 1)
    }

    kind = ""
    if (seg ~ /^dotnet[ \t]+test([ \t)]|$)/) {
      kind = "dotnet"
    } else {
      # scripts/test-ae.py run directly or through python / python3 / py (flags allowed before the script).
      tail = seg
      if (match(tail, /^(python3?(\.[0-9]+)?(\.exe)?|py(\.exe)?)([ \t]+-[A-Za-z]+)*[ \t]+/)) tail = substr(tail, RLENGTH + 1)
      if (substr(tail, 1, 1) == "\001") {
        script = qv[substr(tail, 2, index(tail, "\002") - 2) + 0]
        tail = substr(tail, index(tail, "\002") + 1)
      } else {
        match(tail, /^[^ \t]*/)
        script = substr(tail, 1, RLENGTH)
        tail = substr(tail, RLENGTH + 1)
      }
      if (script ~ /test-ae\.py$/) { kind = "ae"; seg = tail }
    }
    if (kind == "") continue
    if (allowed) continue
    noFilter = (kind == "dotnet") ? 2 : 4
    badFilter = (kind == "dotnet") ? 3 : 5
    if (!match(seg, /[ \t]--filter([ \t=]+|$)/)) exit noFilter
    # A namespace or bare-word filter (FlatRedBall2.Tests.Collision, Dogfood) runs thousands of tests that CI
    # already runs. A name term must contain a class segment (name ending in Test or Tests) or be a
    # method name with an underscore; Ns.Class, Ns.Class.Method, ButtonTests and Click_Raises pass.
    # Filters on other properties (Category=, Trait) are not names and pass.
    rest = substr(seg, RSTART + RLENGTH)
    if (substr(rest, 1, 1) == "\001") {
      value = qv[substr(rest, 2, index(rest, "\002") - 2) + 0]
    } else {
      match(rest, /^[^ \t]*/)
      value = substr(rest, 1, RLENGTH)
    }
    termCount = split(value, terms, /[|&()]/)
    for (t = 1; t <= termCount; t++) {
      term = terms[t]
      gsub(/^[ \t]+|[ \t]+$/, "", term)
      if (match(term, /^[A-Za-z]+(~|=|!=|!~)/)) {
        prop = substr(term, 1, RLENGTH)
        term = substr(term, RLENGTH + 1)
        if (prop !~ /^(FullyQualifiedName|Name|DisplayName|ClassName)/) continue
      }
      if (term == "") continue
      if (term !~ /(^|\.)[A-Za-z0-9_]+Tests?(\.|\+|$)/ && index(term, "_") == 0) exit badFilter
    }
  }
  exit 0
}'

status=$?
case $status in
  2) echo "Blocked: 'dotnet test' without --filter runs a whole test project. Run only the tests for your change: python scripts/run_tests.py <Class>[,<Class>]. CI runs the full suites." >&2 ;;
  3) echo "Blocked: this --filter names a namespace or bare word, not a test class. Run: python scripts/run_tests.py <Class>[,<Class>] (or Class.Method). CI runs the broad suites." >&2 ;;
  4|5) echo "Blocked: scripts/test-ae.py runs the whole AE suite (CI-parity, for the user). Run only the tests for your change: python scripts/run_tests.py <Class>[,<Class>]." >&2 ;;
  *) exit 0 ;;
esac
echo "If the user asked for a full run, prefix the command with FRB2_ALLOW_FULL_TESTS=1." >&2
exit 2
