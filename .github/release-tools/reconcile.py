"""Reconcile missing latest-main CI using repository metadata, never PR code."""
import datetime
import json
import os
import urllib.request


def api(path, payload=None):
    request = urllib.request.Request(
        f"https://api.github.com/repos/{os.environ['GITHUB_REPOSITORY']}/{path}",
        data=json.dumps(payload).encode() if payload is not None else None,
        headers={"Authorization": f"Bearer {os.environ['GH_TOKEN']}",
                 "Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28"})
    with urllib.request.urlopen(request, timeout=30) as response:
        body = response.read()
        return json.loads(body) if body else None


def reconcile(now=None):
    now = now or datetime.datetime.now(datetime.timezone.utc)
    commit = api("commits/main")
    sha = commit["sha"]
    committed = datetime.datetime.fromisoformat(commit["commit"]["committer"]["date"].replace("Z", "+00:00"))
    # Give ordinary push workflows time to appear before attempting recovery.
    if (now - committed).total_seconds() < 180:
        return "Main is recent; let push CI start first."
    runs = api(f"actions/workflows/build.yml/runs?head_sha={sha}&branch=main&per_page=100")["workflow_runs"]
    if any(r["head_sha"] == sha and r["head_branch"] == "main" and r["event"] in {"push", "workflow_dispatch"} for r in runs):
        return "Main already has CI; failures require an explicit rerun."
    # Re-read the branch immediately before dispatch; never mislabel another SHA.
    if api("commits/main")["sha"] != sha:
        return "Main advanced; reconcile the new commit on the next run."
    api("actions/workflows/build.yml/dispatches", {"ref": "main"})
    return f"Dispatched real main CI after missing push run for {sha}."


if __name__ == "__main__":
    print(reconcile())
