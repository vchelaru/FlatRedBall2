#!/usr/bin/env python3
"""Captures the same AnimationEditor screenshot scenario on origin/main and on this worktree.

Usage: scripts/ae-before-after.py <capture.cs> <out-dir>

<capture.cs> is one DocScreenshots [AvaloniaFact] class that writes PNGs through
ScreenshotOutput.ResolveFeatureDir (see the animation-editor-screenshots skill). It can live
anywhere, such as a scratch folder. The script copies it into the gitignored
tests/AnimationEditor.DocScreenshots/_Local/ folder of this worktree and of a reusable detached
origin/main worktree (<this worktree's name>-before, under the main checkout's .claude/worktrees/),
runs it in both at once, and writes every PNG each run produced to <out-dir> as before-<name>.png
and after-<name>.png. Upload them with scripts/push-pr-screenshots.py.
"""
import os, re, shutil, subprocess, sys, time
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJECT = os.path.join("tools", "AnimationEditorAvalonia", "tests", "AnimationEditor.DocScreenshots")
OUT = os.path.join("tools", "AnimationEditorAvalonia", "tests", "_out")


def git(*args, cwd=HERE):
    r = subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f"git {' '.join(args)} failed:\n{r.stderr.strip()}")
    return r.stdout.strip()


def before_tree():
    """Creates or resets this worktree's detached origin/main checkout; returns its path."""
    top = git("rev-parse", "--show-toplevel")
    main_checkout = os.path.dirname(os.path.abspath(os.path.join(top, git("rev-parse", "--git-common-dir"))))
    path = os.path.join(main_checkout, ".claude", "worktrees", os.path.basename(top) + "-before")
    git("fetch", "-q", "origin", "main")
    if os.path.isdir(path):
        git("checkout", "-q", "--detach", "--force", "origin/main", cwd=path)
        # Ignored files (bin/obj) stay, so the next build is incremental.
        shutil.rmtree(os.path.join(path, PROJECT, "_Local"), ignore_errors=True)
    else:
        git("worktree", "add", "-q", "--detach", path, "origin/main")
    return path


def capture(label, tree, source, test_class, log_dir):
    local = os.path.join(tree, PROJECT, "_Local")
    os.makedirs(local, exist_ok=True)
    target = os.path.join(local, os.path.basename(source))
    copied = os.path.abspath(source) != os.path.abspath(target)
    if copied:
        shutil.copyfile(source, target)
    out_root = os.path.join(tree, OUT)
    start = time.time()
    log = os.path.join(log_dir, f"{label}.log")
    with open(log, "w") as f:
        code = subprocess.run(["dotnet", "test", os.path.join(tree, PROJECT),
                               "--filter", f"FullyQualifiedName~.{test_class}."],
                              stdout=f, stderr=subprocess.STDOUT).returncode
    if copied:
        # A leftover copy would collide with the next capture that reuses its class name.
        os.remove(target)
    shots = []
    for folder, _, names in os.walk(out_root):
        for name in names:
            path = os.path.join(folder, name)
            if name.lower().endswith(".png") and os.path.getmtime(path) >= start - 1:
                shots.append(path)
    return label, code, sorted(shots), out_root, log


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    source, out_dir = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
    match = re.search(r"\bclass\s+(\w+)", open(source).read())
    if not match:
        raise SystemExit(f"No class found in {source}.")
    test_class = match.group(1)
    os.makedirs(out_dir, exist_ok=True)
    log_dir = out_dir

    before = before_tree()
    with ThreadPoolExecutor(max_workers=2) as pool:
        results = list(pool.map(lambda job: capture(*job, source, test_class, log_dir),
                                [("before", before), ("after", HERE)]))

    failed = False
    for label, code, shots, out_root, log in results:
        if code != 0 or not shots:
            failed = True
            print(f"{label}: {'test failed' if code else 'no PNGs written'} (exit {code}); see {log}")
            continue
        for shot in shots:
            flat = os.path.relpath(shot, out_root).replace(os.sep, "-")
            dest = os.path.join(out_dir, f"{label}-{flat}")
            shutil.copyfile(shot, dest)
            print(dest)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
