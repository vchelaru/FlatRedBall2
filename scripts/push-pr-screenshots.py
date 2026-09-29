#!/usr/bin/env python3
"""Uploads PR screenshots to the orphan pr-assets branch and prints markdown to embed them.

Usage: scripts/push-pr-screenshots.py <pr-number> <folder> [owner/repo]

Every image directly in <folder> (png, jpg, jpeg, gif, webp) lands at pr-assets:<pr-number>/<name>;
other files and subfolders are skipped. Uses the GitHub API through `gh`, so it never touches the
local checkout. Safe to run concurrently: a lost race for the branch is rebuilt on the new head and
retried. Each printed URL is pinned to the upload's commit, so re-uploading a file with the same
name never serves a stale copy, and is checked to serve before the script exits 0.
"""
import base64, json, os, random, subprocess, sys, time, urllib.error, urllib.request

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".gif", ".webp"}
BRANCH = "pr-assets"


class ApiError(Exception):
    def __init__(self, method, path, status, message):
        super().__init__(f"gh api {method} {path} failed ({status}): {message}")
        self.status = status


def api(repo, method, path, body=None):
    args = ["gh", "api", "-X", method, f"repos/{repo}/{path}", "--include"]
    r = subprocess.run(args + (["--input", "-"] if body is not None else []),
                       input=json.dumps(body) if body is not None else None, capture_output=True, text=True)
    # --include puts the HTTP status line and headers before the JSON body.
    head, _, payload = r.stdout.partition("\r\n\r\n")
    if not payload and "\n\n" in r.stdout:
        head, _, payload = r.stdout.partition("\n\n")
    status_line = head.splitlines()[0] if head else ""
    status = int(status_line.split()[1]) if len(status_line.split()) > 1 and status_line.split()[1].isdigit() else 0
    if r.returncode != 0:
        raise ApiError(method, path, status, (payload.strip() or r.stderr.strip())[:500])
    return json.loads(payload) if payload.strip() else None


def branch_head(repo):
    try:
        return api(repo, "GET", f"git/ref/heads/{BRANCH}")["object"]["sha"]
    except ApiError as e:
        if e.status == 404:
            return None
        raise


def publish(repo, pr, entries):
    """Commits `entries` onto the branch head; returns the commit sha. Retries lost races."""
    for attempt in range(8):
        parent = branch_head(repo)
        body = {"tree": entries}
        if parent:
            body["base_tree"] = api(repo, "GET", f"git/commits/{parent}")["tree"]["sha"]
        tree = api(repo, "POST", "git/trees", body)["sha"]
        commit = api(repo, "POST", "git/commits", {"message": f"Screenshots for PR #{pr}", "tree": tree,
                                                  "parents": [parent] if parent else []})["sha"]
        try:
            if parent:
                api(repo, "PATCH", f"git/refs/heads/{BRANCH}", {"sha": commit, "force": False})
            else:
                api(repo, "POST", "git/refs", {"ref": f"refs/heads/{BRANCH}", "sha": commit})
            return commit
        except ApiError as e:
            # 422: someone else moved the branch (not a fast-forward) or created it first.
            if e.status != 422:
                raise
            delay = min(8, 0.5 * 2 ** attempt) + random.random()
            print(f"{BRANCH} moved underneath this upload; retrying in {delay:.1f}s", file=sys.stderr)
            time.sleep(delay)
    raise SystemExit(f"Gave up after repeated races for {BRANCH}.")


def serves(url, timeout_s=60):
    deadline = time.time() + timeout_s
    while True:
        try:
            # The query string bypasses any cached 404 from the raw CDN.
            with urllib.request.urlopen(f"{url}?check={time.time_ns()}", timeout=15) as response:
                if response.status == 200:
                    return True
        except urllib.error.URLError:
            pass
        if time.time() > deadline:
            return False
        time.sleep(3)


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    pr, folder = sys.argv[1], sys.argv[2]
    repo = sys.argv[3] if len(sys.argv) > 3 else "vchelaru/FlatRedBall2"

    names = sorted(n for n in os.listdir(folder)
                   if os.path.isfile(os.path.join(folder, n)) and os.path.splitext(n)[1].lower() in IMAGE_EXTENSIONS)
    skipped = sorted(set(os.listdir(folder)) - set(names))
    if skipped:
        print(f"Skipping (not an image, or a folder): {', '.join(skipped)}", file=sys.stderr)
    if not names:
        print(f"No images found in {folder}.", file=sys.stderr)
        return 1

    entries = []
    for name in names:
        with open(os.path.join(folder, name), "rb") as f:
            data = base64.b64encode(f.read()).decode()
        blob = api(repo, "POST", "git/blobs", {"content": data, "encoding": "base64"})["sha"]
        entries.append({"path": f"{pr}/{name}", "mode": "100644", "type": "blob", "sha": blob})

    commit = publish(repo, pr, entries)
    urls = [(e["path"].split("/", 1)[1], f"https://raw.githubusercontent.com/{repo}/{commit}/{e['path']}")
            for e in entries]
    broken = [url for _, url in urls if not serves(url)]
    if broken:
        print("Uploaded, but these URLs do not serve:\n" + "\n".join(broken), file=sys.stderr)
        return 1
    for name, url in urls:
        print(f"![{os.path.splitext(name)[0]}]({url})")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except ApiError as e:
        print(e, file=sys.stderr)
        sys.exit(1)
