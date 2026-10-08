#!/usr/bin/env python3
"""Builds the AnimationEditor once and runs every AE test project, in parallel.

Usage:
  scripts/test-ae.py                       # all Core, Views, App and DocScreenshots tests
  scripts/test-ae.py --filter "FullyQualifiedName~Dogfood"
  scripts/test-ae.py --no-build ...        # skip the build (you just built)

Works from any worktree: paths resolve from this file, not the current directory. App.Tests is
split into shards run as separate processes, because Avalonia headless runs every [AvaloniaFact]
on one UI thread. The last shard is "everything not in another shard", so no test class can be
dropped. Logs go to a fresh temp folder, printed at the end. Exit code is 0 only if every test
passed.
"""
import argparse, os, re, subprocess, sys, tempfile, time
from concurrent.futures import ThreadPoolExecutor

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
AE = os.path.join(ROOT, "tools", "AnimationEditorAvalonia")
TESTS = os.path.join(AE, "tests")
PROJECTS = ["AnimationEditor.Core.Tests", "AnimationEditor.Views.Tests", "AnimationEditor.DocScreenshots",
            "AnimationEditor.App.Tests"]
APP_SHARDS = 6
# A test that parks on a real dialog never returns; fail it instead of hanging the run.
HANG_TIMEOUT = "60s"


def app_shard_filters():
    """Splits App.Tests test classes (one per file) into shards balanced by file size."""
    app = os.path.join(TESTS, "AnimationEditor.App.Tests")
    files = []
    for folder, _, names in os.walk(app):
        if os.sep + "bin" in folder or os.sep + "obj" in folder:
            continue
        for name in names:
            if name.endswith("Tests.cs"):
                files.append((os.path.getsize(os.path.join(folder, name)), name[:-3]))
    buckets = [[0, []] for _ in range(APP_SHARDS)]
    for size, cls in sorted(files, reverse=True):
        bucket = min(buckets, key=lambda b: b[0])
        bucket[0] += size
        bucket[1].append(cls)
    explicit = buckets[:-1]
    filters = ["|".join(f"FullyQualifiedName~.{c}." for c in b[1]) for b in explicit]
    listed = [c for b in explicit for c in b[1]]
    filters.append("&".join(f"FullyQualifiedName!~.{c}." for c in listed))
    return filters


def run(label, project, test_filter, log_dir):
    log = os.path.join(log_dir, label + ".log")
    cmd = ["dotnet", "test", os.path.join(TESTS, project), "--no-build",
           "--blame-hang-timeout", HANG_TIMEOUT]
    if test_filter:
        cmd += ["--filter", test_filter]
    start = time.time()
    with open(log, "w") as out:
        code = subprocess.run(cmd, stdout=out, stderr=subprocess.STDOUT, cwd=AE).returncode
    text = open(log, errors="replace").read()
    summary = re.findall(r"(?:Passed|Failed)!\s+- Failed:\s+(\d+), Passed:\s+(\d+)", text)
    failed = sum(int(f) for f, _ in summary)
    passed = sum(int(p) for _, p in summary)
    ok = code == 0 and failed == 0
    return label, ok, passed, failed, time.time() - start, log, text


def failure_details(text):
    # Each failure block starts at "  Failed <name> [..]" and runs to the next failure, xUnit
    # progress line or run summary. Assertion bodies ("Expected: ...") are not indented.
    blocks = re.findall(r"^  Failed .*?(?=^  Failed |^\[xUnit|^(?:Passed|Failed)!|\Z)", text, re.M | re.S)
    return ["\n".join(b.rstrip().splitlines()[:40]) for b in blocks]


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--filter", help="dotnet test --filter expression, applied to every project")
    parser.add_argument("--no-build", action="store_true", help="skip building the solution first")
    args = parser.parse_args()

    if not args.no_build:
        print("Building AnimationEditorAvalonia.slnx ...", flush=True)
        build = subprocess.run(["dotnet", "build", "AnimationEditorAvalonia.slnx", "-v", "q", "-nologo"],
                               cwd=AE, capture_output=True, text=True)
        if build.returncode != 0:
            print(build.stdout + build.stderr)
            print("BUILD FAILED")
            return 1

    jobs = []
    for project in PROJECTS:
        short = project.replace("AnimationEditor.", "")
        if project == "AnimationEditor.App.Tests":
            for i, shard in enumerate(app_shard_filters(), 1):
                combined = f"({args.filter})&({shard})" if args.filter else shard
                jobs.append((f"{short}-{i}", project, combined))
        else:
            jobs.append((short, project, args.filter))

    log_dir = tempfile.mkdtemp(prefix="ae-tests-")
    start = time.time()
    with ThreadPoolExecutor(max_workers=len(jobs)) as pool:
        results = list(pool.map(lambda j: run(j[0], j[1], j[2], log_dir), jobs))

    all_ok = all(r[1] for r in results)
    for label, ok, passed, failed, secs, log, text in results:
        print(f"{'ok  ' if ok else 'FAIL'} {label:<22} passed {passed:>5}  failed {failed:>3}  {secs:5.0f}s")
    for label, ok, passed, failed, secs, log, text in results:
        if ok:
            continue
        print(f"\n===== {label} ({log})")
        details = failure_details(text)
        print("\n".join(details) if details else text[-4000:])
    total = sum(r[2] for r in results)
    failures = sum(r[3] for r in results)
    status = "NO TESTS RAN" if total + failures == 0 else "PASSED" if all_ok else "FAILED"
    print(f"\n{status}: {total} passed, {failures} failed, "
          f"{time.time() - start:.0f}s. Logs: {log_dir}")
    if total + failures == 0:
        print("Check the --filter expression.")
        return 1
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())
