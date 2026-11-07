"""Fail-closed release contracts; standard library only, no hosting writes."""
import argparse
import hashlib
import json
import re
import stat
import zipfile
from pathlib import Path, PurePosixPath


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha256(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def package(site, output, commit, run_id, app_version):
    site, output = Path(site), Path(output)
    require(re.fullmatch(r"[a-f0-9]{40}", commit), "Expected full commit SHA")
    require(str(run_id).isdigit(), "Expected numeric CI run ID")
    require(re.fullmatch(r"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)", app_version), "Expected app MAJOR.MINOR.PATCH version")
    require((site / "index.html").is_file(), "Missing index.html")
    require((site / "_framework/blazor.webassembly.js").is_file(), "Missing Blazor runtime")
    files = sorted(p for p in site.rglob("*") if p.is_file())
    require(not any(p.is_symlink() for p in site.rglob("*")), "Symlinks are forbidden")
    manifest = {"schema": 1, "commit": commit, "run_id": str(run_id), "app_version": app_version,
                "files": {p.relative_to(site).as_posix(): sha256(p) for p in files}}
    require("release-manifest.json" not in manifest["files"], "Reserved manifest filename")
    output.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output / "spellbook-site.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for p in files:
            archive.write(p, p.relative_to(site).as_posix())
        archive.writestr("release-manifest.json", json.dumps(manifest, sort_keys=True, indent=2))
    digest = sha256(output / "spellbook-site.zip")
    (output / "SHA256SUMS").write_text(f"{digest}  spellbook-site.zip\n")


def verify(archive_path, expected_sha, expected_digest=None, destination=None, version=None):
    if expected_digest:
        require(sha256(archive_path) == expected_digest.removeprefix("sha256:"), "Archive checksum mismatch")
    with zipfile.ZipFile(archive_path) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "Duplicate archive paths")
        require(len(names) < 10000 and sum(i.file_size for i in archive.infolist()) < 500_000_000,
                "Archive exceeds static site limits")
        for info in archive.infolist():
            p = PurePosixPath(info.filename)
            require(not p.is_absolute() and ".." not in p.parts and "\\" not in info.filename,
                    "Unsafe archive path")
            require(stat.S_IFMT(info.external_attr >> 16) != stat.S_IFLNK, "Archive symlink")
        manifest = json.loads(archive.read("release-manifest.json"))
        require(manifest["schema"] == 1 and manifest["commit"] == expected_sha, "Manifest commit/schema mismatch")
        require(str(manifest["run_id"]).isdigit(), "Missing source CI run")
        require(re.fullmatch(r"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)", manifest["app_version"]), "Missing app version")
        if version:
            require(re.fullmatch(r"v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-rc\.[1-9]\d*)?", version), "Invalid release version")
            require(version.removeprefix("v").split("-rc.")[0] == manifest["app_version"], "Release tag must match compiled app version")
        require(set(names) == set(manifest["files"]) | {"release-manifest.json"}, "Untracked/missing files")
        require({"index.html", "_framework/blazor.webassembly.js"} <= set(manifest["files"]), "Missing app entry points")
        for name, digest in manifest["files"].items():
            require(hashlib.sha256(archive.read(name)).hexdigest() == digest, f"File checksum mismatch: {name}")
        if destination:
            destination = Path(destination)
            require(not destination.exists(), "Extraction destination must be new")
            archive.extractall(destination)
    return manifest


def candidate(run, checks, version):
    require(re.fullmatch(r"v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-rc\.[1-9]\d*)?", version), "Use vMAJOR.MINOR.PATCH or -rc.N")
    require(run["name"] == "Build Spellbook" and run["path"] == ".github/workflows/build.yml", "Wrong build workflow")
    require(run["head_branch"] == "main" and run["event"] in {"push", "workflow_dispatch"}, "Candidate must be a real main build")
    require(run["head_repository"]["full_name"] == run["repository"]["full_name"], "Fork build is not a release source")
    require(run["status"] == "completed" and run["conclusion"] == "success", "Build/tests must pass")
    require(re.fullmatch(r"[a-f0-9]{40}", run["head_sha"]), "Missing commit SHA")
    # GitHub's aggregate can be skipped/absent while its dynamic security
    # workflow succeeds. Verify that workflow's actual analysis provenance;
    # Code Quality has similarly named jobs and must not substitute for it.
    security = [c for c in checks["check_runs"] if c["name"] == "CodeQL" and c["app"]["id"] == 57789]
    require(all(c["status"] == "completed" and c["conclusion"] in {"success", "skipped"} for c in security), "CodeQL summary is failing/pending")
    analysis = checks["security_run"]
    require(analysis["path"] == "dynamic/github-code-scanning/codeql" and
            analysis["head_branch"] == "main" and analysis["head_sha"] == run["head_sha"] and
            analysis["repository"]["full_name"] == run["repository"]["full_name"] and
            analysis["head_repository"]["full_name"] == run["repository"]["full_name"] and
            analysis["status"] == "completed" and analysis["conclusion"] == "success", "Trusted main security workflow must pass")
    jobs = checks["security_jobs"]["jobs"]
    required = {"Analyze (csharp)", "Analyze (javascript-typescript)", "Analyze (actions)"}
    require(required <= {j["name"] for j in jobs} and
            all(j["status"] == "completed" and j["conclusion"] == "success" for j in jobs),
            "All security language analyses must pass")
    return run["head_sha"]


def production_environment(environment, policies):
    approvals = [r for r in environment["protection_rules"] if r["type"] == "required_reviewers"]
    require(any(r.get("prevent_self_review") and r.get("reviewers") for r in approvals), "Production requires an independent reviewer")
    require(environment.get("deployment_branch_policy", {}).get("custom_branch_policies"), "Production must restrict deployment branches")
    require([(p["name"], p.get("type", "branch")) for p in policies["branch_policies"]] == [("main", "branch")], "Only main may deploy to production")


def hosting(project):
    require(project.get("success") is True, "Cannot verify Cloudflare project")
    project = project["result"]
    require(project["production_branch"] == "release-production", "Production branch must be release-production")
    require(project["source"]["config"]["production_deployments_enabled"] is False,
            "Disable Git production auto-deploy before promotion")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("package")
    for name in ("site", "output", "commit", "run_id", "app_version"):
        p.add_argument(name)
    p = sub.add_parser("verify")
    p.add_argument("archive"); p.add_argument("commit")
    p.add_argument("--digest"); p.add_argument("--destination")
    p.add_argument("--version")
    p = sub.add_parser("candidate")
    p.add_argument("run"); p.add_argument("checks"); p.add_argument("version")
    p = sub.add_parser("environment")
    p.add_argument("environment"); p.add_argument("policies")
    p = sub.add_parser("hosting"); p.add_argument("project")
    args = vars(parser.parse_args()); command = args.pop("command")
    if command == "package":
        package(**args)
    elif command == "verify":
        print(json.dumps(verify(args["archive"], args["commit"], args["digest"], args["destination"], args["version"])))
    elif command == "candidate":
        print(candidate(json.loads(Path(args["run"]).read_text()), json.loads(Path(args["checks"]).read_text()), args["version"]))
    elif command == "environment":
        production_environment(json.loads(Path(args["environment"]).read_text()), json.loads(Path(args["policies"]).read_text()))
    else:
        hosting(json.loads(Path(args["project"]).read_text()))


if __name__ == "__main__":
    main()
