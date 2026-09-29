"""Uploads PR screenshots to the orphan pr-assets branch and prints their embeddable URLs.

Usage: python scripts/push-pr-screenshots.py <pr-number> <folder-of-pngs> [owner/repo]

Every file in the folder lands at pr-assets:<pr-number>/<file name>. Uses the GitHub API through
`gh`, so it never touches the local checkout or its branches.
"""
import base64, json, os, subprocess, sys

pr, folder = sys.argv[1], sys.argv[2]
repo = sys.argv[3] if len(sys.argv) > 3 else "vchelaru/FlatRedBall2"

def api(method, path, body=None):
    args = ["gh", "api", "-X", method, f"repos/{repo}/{path}"]
    r = subprocess.run(args + (["--input", "-"] if body else []), input=json.dumps(body) if body else None,
                       capture_output=True, text=True)
    return r.returncode, (json.loads(r.stdout) if r.stdout.strip() else None)

code, ref = api("GET", "git/ref/heads/pr-assets")
parent = ref["object"]["sha"] if code == 0 else None
base_tree = api("GET", f"git/commits/{parent}")[1]["tree"]["sha"] if parent else None

tree = []
for name in sorted(os.listdir(folder)):
    data = base64.b64encode(open(os.path.join(folder, name), "rb").read()).decode()
    blob = api("POST", "git/blobs", {"content": data, "encoding": "base64"})[1]["sha"]
    tree.append({"path": f"{pr}/{name}", "mode": "100644", "type": "blob", "sha": blob})

body = {"tree": tree}
if base_tree: body["base_tree"] = base_tree
tree_sha = api("POST", "git/trees", body)[1]["sha"]
commit = api("POST", "git/commits", {"message": f"Screenshots for PR #{pr}", "tree": tree_sha,
                                     "parents": [parent] if parent else []})[1]["sha"]
if parent:
    api("PATCH", "git/refs/heads/pr-assets", {"sha": commit})
else:
    api("POST", "git/refs", {"ref": "refs/heads/pr-assets", "sha": commit})
for t in tree:
    print(f"https://raw.githubusercontent.com/{repo}/pr-assets/{t['path']}")
