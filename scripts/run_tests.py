#!/usr/bin/env python3
"""Runs exactly the named test classes: finds the owning test project, builds only it, runs it once.

Usage:
  python scripts/run_tests.py ButtonTests
  python scripts/run_tests.py CircleTests,AARectTests          # several classes, comma or space separated
  python scripts/run_tests.py CircleTests.Radius_Grows          # one method
  python scripts/run_tests.py AnimationEditor.App.Tests:GridTests   # Project:Class when a class is in several projects
  python scripts/run_tests.py --list CircleTests                # show project and filter, run nothing
  python scripts/run_tests.py --no-build CircleTests            # skip the build (already built)

Candidates are the test projects under tests/ and tools/AnimationEditorAvalonia/tests/. The Windows
Sandbox and GoldenImage projects run only when named as Project:Class. A name that is not a test class
(a namespace, a folder, a typo) is refused: CI runs the broad suites, a local run covers the code under
work. A class found in several projects runs in each. Each build and test step has a hard time limit; a
step that exceeds it is killed (process tree) and reported TIMEDOUT, which must not be rerun unchanged.
Works from any worktree: paths resolve from this file. Exit code 0 only if every step passed.
"""
import argparse, os, re, subprocess, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TEST_ROOTS = ["tests", "tools/AnimationEditorAvalonia/tests"]
# Only run when named as Project:Class. Sandbox boots a Windows Sandbox per scenario; GoldenImage needs a display.
OPT_IN_ONLY = {"AnimationEditor.Sandbox.Tests", "FlatRedBall2.GoldenImage.Tests"}
# A test that parks on a real dialog never returns; fail it instead of hanging the run.
HANG_TIMEOUT = "60s"
PASS_RE = re.compile(r"(?:Passed|Failed)!\s+- Failed:\s+(\d+), Passed:\s+(\d+)")


def find_test_projects():
    """Returns {project name: repo-relative csproj path} for every test project under TEST_ROOTS."""
    projects = {}
    for root in TEST_ROOTS:
        base = os.path.join(ROOT, *root.split("/"))
        if not os.path.isdir(base):
            continue
        for name in sorted(os.listdir(base)):
            csproj = os.path.join(base, name, name + ".csproj")
            if os.path.isfile(csproj):
                projects[name] = root + "/" + name + "/" + name + ".csproj"
    return projects


def parse_requests(raw):
    """Splits 'A,B C.Method Proj:D' into (project or None, class, method or None) tuples."""
    requests = []
    for token in re.split(r"[,\s]+", " ".join(raw)):
        if not token:
            continue
        project = None
        if ":" in token:
            project, token = token.split(":", 1)
        cls, _, method = token.partition(".")
        requests.append((project, cls, method or None))
    return requests


def find_class_files(class_names):
    """Maps class name -> repo-relative .cs files declaring it (git grep, so new untracked files count)."""
    pattern = r"class[[:space:]]+(" + "|".join(re.escape(c) for c in class_names) + r")([^A-Za-z0-9_]|$)"
    out = subprocess.run(["git", "grep", "--untracked", "-nIE", pattern, "--", "*.cs"],
                         cwd=ROOT, capture_output=True, text=True, errors="replace").stdout
    found = {}
    for line in out.splitlines():
        m = re.match(r"^([^:]+):\d+:.*\bclass\s+(\w+)", line)
        if m and m.group(2) in class_names:
            found.setdefault(m.group(2), set()).add(m.group(1))
    return found


def namespace_of(rel_path):
    with open(os.path.join(ROOT, rel_path), errors="replace") as f:
        m = re.search(r"^\s*namespace\s+([\w.]+)", f.read(), re.M)
    return m.group(1) if m else ""


def resolve(requests, projects):
    """Returns ({project name: set of filter terms}, [unresolved request strings])."""
    class_files = find_class_files({cls for _, cls, _ in requests})
    candidates = {name: path for name, path in projects.items() if name not in OPT_IN_ONLY}
    filters, unresolved = {}, []
    for project, cls, method in requests:
        pool = projects if project else candidates
        owners = {}  # project name -> namespace
        for rel in sorted(class_files.get(cls, ())):
            owner = max((n for n, p in pool.items() if rel.startswith(os.path.dirname(p) + "/")),
                        key=lambda n: len(pool[n]), default=None)
            if owner and (project is None or owner == project):
                owners.setdefault(owner, namespace_of(rel))
        if not owners:
            unresolved.append(("%s:" % project if project else "") + cls + ("." + method if method else ""))
            continue
        for owner, ns in owners.items():
            qualified = (ns + "." if ns else "") + cls
            terms = ["FullyQualifiedName~%s.%s" % (qualified, method)] if method else \
                    ["FullyQualifiedName~%s." % qualified, "FullyQualifiedName~%s+" % qualified]
            filters.setdefault(owner, set()).update(terms)
    return filters, unresolved


def kill_tree(proc):
    if os.name == "nt":
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)], capture_output=True)
    else:
        import signal
        try:
            os.killpg(proc.pid, signal.SIGKILL)
        except OSError:
            proc.kill()


def run_step(cmd, timeout):
    """Runs one dotnet command with a hard time limit. Returns (exit code or None if timed out, output, seconds)."""
    env = dict(os.environ, MSBUILDTERMINALLOGGER="off")
    start = time.time()
    kwargs = {} if os.name == "nt" else {"start_new_session": True}
    proc = subprocess.Popen(cmd, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            text=True, errors="replace", **kwargs)
    try:
        output, _ = proc.communicate(timeout=timeout)
        return proc.returncode, output, time.time() - start
    except subprocess.TimeoutExpired:
        kill_tree(proc)
        output, _ = proc.communicate()
        return None, output or "", time.time() - start


def failure_details(text):
    # Each failure block starts at "  Failed <name> [..]" and runs to the next failure, xUnit
    # progress line or run summary. Assertion bodies ("Expected: ...") are not indented.
    blocks = re.findall(r"^  Failed .*?(?=^  Failed |^\[xUnit|^(?:Passed|Failed)!|\Z)", text, re.M | re.S)
    return ["\n".join(b.rstrip().splitlines()[:40]) for b in blocks]


def report(name, code, output, secs, expects_summary, timeout):
    summary = PASS_RE.findall(output)
    if code is None:
        status = "TIMEDOUT"
    elif code != 0:
        status = "FAILED"
    elif expects_summary and not summary:
        status = "NOTESTS"
    else:
        status = "ok"
    print("%-8s %4.0fs  %s" % (status, secs, name))
    if status == "TIMEDOUT":
        print("  killed after %ds with no result. Do not rerun it unchanged: narrow the class list or look for a hung test." % timeout)
    if status == "NOTESTS":
        print("  no test summary in the output (the filter matched no test, or the output format changed). Last lines:")
        for line in [l for l in output.splitlines() if l.strip()][-15:]:
            print("    " + line)
    for line in sorted({l.strip() for l in output.splitlines() if ": error " in l}):
        print("  " + line)
    for s in summary:
        print("  passed %s, failed %s" % (s[1], s[0]))
    if status == "FAILED":
        details = failure_details(output)
        print("\n".join(details) if details else output[-3000:])
    return status


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("tests", nargs="*", help="Class, Class.Method or Project:Class names, comma or space separated")
    parser.add_argument("--list", action="store_true", help="print the projects and filters that would run, then stop")
    parser.add_argument("--no-build", action="store_true", help="skip the build step")
    parser.add_argument("--build-timeout", type=int, default=300, help="seconds per build (default 300)")
    parser.add_argument("--test-timeout", type=int, default=240, help="seconds per test run (default 240)")
    args = parser.parse_args()

    requests = parse_requests(args.tests)
    if not requests:
        parser.print_usage()
        print("REFUSED: name at least one test class. This runner never runs a whole project; CI runs the full suites.")
        return 2

    projects = find_test_projects()
    for project, _, _ in requests:
        if project and project not in projects:
            print("REFUSED: unknown test project '%s'. Known: %s" % (project, ", ".join(sorted(projects))))
            return 2
    filters, unresolved = resolve(requests, projects)
    if unresolved:
        print("REFUSED: not a test class in any test project: " + ", ".join(unresolved))
        print("Name test classes (optionally Project:Class or Class.Method), never a namespace, a folder or a whole project.")
        return 2

    if args.list:
        for name in sorted(filters):
            print("test  %s  %s" % (projects[name], "|".join(sorted(filters[name]))))
        return 0

    statuses = []
    for name in sorted(filters):
        csproj = os.path.join(ROOT, *projects[name].split("/"))
        if not args.no_build:
            code, output, secs = run_step(["dotnet", "build", csproj, "-nologo", "-v:q"], args.build_timeout)
            status = report("build " + name, code, output, secs, False, args.build_timeout)
            statuses.append("%-8s build %s" % (status, name))
            if status != "ok":
                print("skipped        test  %s: its build failed" % name)
                continue
        test_filter = "|".join(sorted(filters[name]))
        cmd = ["dotnet", "test", csproj, "--no-build", "-nologo", "-v:m", "--blame-hang-timeout", HANG_TIMEOUT,
               "--filter", test_filter]
        code, output, secs = run_step(cmd, args.test_timeout)
        status = report("test  " + name, code, output, secs, True, args.test_timeout)
        statuses.append("%-8s test  %s" % (status, name))

    if len(statuses) > 2:
        print("\nResults:")
        for s in statuses:
            print("  " + s)
    ok = all(s.startswith("ok") for s in statuses)
    print("\nRUN-TESTS " + ("OK" if ok else "FAILED"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
