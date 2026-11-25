#!/bin/bash
# PreToolUse(Bash|PowerShell): refuses commands that read their script or input from a stdin nothing feeds.
#  1. `python -` / `python3 -` or another interpreter (a script read from stdin, usually a heredoc): a heredoc that
#     loses its terminator, or a script piped from a command that never closes, leaves it waiting forever.
#  2. A bare `cat > file` / `cat >> file` that starts a command with no file argument, no heredoc and no
#     input redirect: cat waits on stdin forever.
# Both leave an orphan process the app lists as a background task for hours. Write the file with the Write
# tool (in the scratchpad) and run interpreters on a file path instead.
# A plain regex over the raw hook JSON, so text that only mentions a pattern in a quoted string is refused
# too; reword it or write it to a file. Tests: block-python-stdin.test.sh.
input=$(cat)

case "$input" in
  *py*|*node*|*perl*|*ruby*|*sh*|*cat*) ;;
  *) exit 0 ;;
esac

# The command's lines as lines: in the JSON a line break is \n, whose n would read as a letter in front of the
# interpreter. A doubled backslash is a path separator.
lines=${input//\\\\/\/}
lines=${lines//\\r/}
lines=${lines//\\n/$'\n'}
lines=${lines//\\\"/\"}

# `python -`, `python3 -`, `python3.14 -`, `python.exe -`, `py -`, with a path in front or flags before the dash
# (`python3 -I -`, `python -X utf8 -`), a heredoc straight after the interpreter (`python3 <<EOF`), and the same
# for the other interpreters.
interpreter='(python[0-9.]*|py|pypy[0-9.]*|node|perl|ruby|bash|sh|pwsh|powershell)(\.exe)?["'\'']?'
flags='( +(-[XW] +[^ <-][^ ]*|--?[[:alnum:]][^ <]*))*'
if printf '%s\n' "$lines" | grep -Eq "(^|[^[:alnum:]_.-])${interpreter}${flags}( +-([] ;&|)<\"'\`]|\$)| *<<)"; then
  echo "Refused: an interpreter reading its script from stdin (python -, or a heredoc) can hang forever waiting on stdin. Write the script to a file with the Write tool and run python3 <file>." >&2
  exit 2
fi

# An interpreter alone in a command that is not fed by a pipe reads its script from stdin too.
if printf '%s\n' "$lines" | grep -Eq "(^|[;&(]|\"command\": *\")[ ]*(timeout +[0-9smh]+ +)?${interpreter}[ ]*([;&)]|\|\||\",\"|\$)"; then
  echo "Refused: an interpreter with no script reads it from stdin and hangs forever. Write the script to a file with the Write tool and run python3 <file>." >&2
  exit 2
fi

# `cat > file` or `cat >> file` where cat starts a command (start of the command, after ; & ( or a newline,
# not after a pipe) and that same command ends right after the target (optionally a `2>...` redirect), so
# nothing feeds it: no heredoc (<<) and no input redirect (< file) in that command. Judged per command, not
# over the whole input, so a `<` elsewhere (an XML tag in a sed argument) cannot excuse it.
if printf '%s' "$input" | grep -Eq '(^|[;&(]|\\n|")[ ]*cat +>{1,2} *[^ <;&|"\\]+( +2>(&1|[^ ;&|"\\]*))?[ ]*(;|&|\\n|"|\)|$)'; then
  echo "Refused: a bare 'cat > file' reads stdin and hangs forever when nothing feeds it. Write the file with the Write tool (or use a heredoc, then 'cat > file <<EOF')." >&2
  exit 2
fi
exit 0
