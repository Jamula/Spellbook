import tempfile
import unittest
import warnings
import zipfile
from pathlib import Path
from release import candidate, hosting, package, production_environment, sha256, verify

SHA = "a" * 40


class ReleaseContracts(unittest.TestCase):
    def test_exact_artifact_survives_promotion_and_rollback_and_rejects_tampering(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); site = root / "site"
            (site / "_framework").mkdir(parents=True)
            (site / "index.html").write_text("old known-good UI")
            (site / "_framework/blazor.webassembly.js").write_text("runtime")
            package(site, root / "release", SHA, 123, "0.1.0")
            archive = root / "release/spellbook-site.zip"; digest = sha256(archive)
            for operation in ("promotion", "rollback"):
                manifest = verify(archive, SHA, digest, root / operation, "v0.1.0-rc.1")
                self.assertEqual("123", manifest["run_id"])
                self.assertEqual("0.1.0", manifest["app_version"])
                self.assertEqual("old known-good UI", (root / operation / "index.html").read_text())
            with self.assertRaises(ValueError): verify(archive, "b" * 40)
            with self.assertRaises(ValueError): verify(archive, SHA, "0" * 64)
            with self.assertRaises(ValueError): verify(archive, SHA, version="v0.2.0")
            original = archive.read_bytes()
            for corruption in ("checksum", "symlink", "duplicate"):
                altered = root / f"{corruption}.zip"
                with zipfile.ZipFile(archive) as source, zipfile.ZipFile(altered, "w") as target:
                    for info in source.infolist():
                        payload = source.read(info.filename)
                        if info.filename == "index.html":
                            if corruption == "checksum": payload = b"changed UI"
                            if corruption == "symlink": info.external_attr = 0o120777 << 16
                        target.writestr(info, payload)
                    if corruption == "duplicate":
                        with warnings.catch_warnings():
                            warnings.simplefilter("ignore", UserWarning)
                            target.writestr("index.html", "duplicate")
                with self.subTest(corruption=corruption), self.assertRaises(ValueError): verify(altered, SHA)
            self.assertEqual(original, archive.read_bytes())
            with zipfile.ZipFile(archive, "a") as z: z.writestr("../escape", "evil")
            with self.assertRaises(ValueError): verify(archive, SHA)
            self.assertFalse((root / "escape").exists())

    def test_failed_or_untrusted_gates_never_qualify_candidate(self):
        run = dict(name="Build Spellbook", path=".github/workflows/build.yml", head_branch="main", event="push", status="completed", conclusion="success", head_sha=SHA, repository={"full_name": "Jamula/Spellbook"}, head_repository={"full_name": "Jamula/Spellbook"})
        analysis = run | {"path": "dynamic/github-code-scanning/codeql"}
        jobs = {"jobs": [dict(name=n, status="completed", conclusion="success") for n in ("Analyze (csharp)", "Analyze (actions)", "Analyze (javascript-typescript)")]}
        checks = {"check_runs": [dict(name="CodeQL", app={"id": 57789}, status="completed", conclusion="success")], "security_run": analysis, "security_jobs": jobs}
        self.assertEqual(SHA, candidate(run, checks, "v1.2.3-rc.1"))
        for field, value in (("event", "pull_request"), ("conclusion", "failure"), ("status", "in_progress"), ("head_branch", "feature"), ("path", "other.yml"), ("head_repository", {"full_name": "fork/Spellbook"})):
            with self.subTest(field=field), self.assertRaises(ValueError): candidate(run | {field: value}, checks, "v1.2.3")
        for version in ("v01.2.3", "main", "v1.2.3/evil", "v1.2.3-rc.0"):
            with self.subTest(version=version), self.assertRaises(ValueError): candidate(run, checks, version)
        # Skipped summary is acceptable ONLY with successful actual analyses.
        self.assertEqual(SHA, candidate(run, checks | {"check_runs": [checks["check_runs"][0] | {"conclusion": "skipped"}]}, "v1.2.3"))
        for bad in ({"check_runs": [checks["check_runs"][0] | {"conclusion": "failure"}]}, {"security_jobs": {"jobs": []}}, {"security_jobs": {"jobs": [jobs["jobs"][0] | {"conclusion": "skipped"}]}}):
            with self.subTest(bad=bad), self.assertRaises(ValueError): candidate(run, checks | bad, "v1.2.3")
        for field, value in (("head_sha", "b" * 40), ("head_branch", "feature"), ("path", "dynamic/github-code-quality/codeql"), ("repository", {"full_name": "fork/Spellbook"}), ("head_repository", {"full_name": "fork/Spellbook"}), ("conclusion", "failure")):
            with self.subTest(field=field), self.assertRaises(ValueError): candidate(run, checks | {"security_run": analysis | {field: value}}, "v1.2.3")

    def test_production_requires_manual_review_and_no_competing_git_deploy(self):
        env = dict(protection_rules=[dict(type="required_reviewers", prevent_self_review=False, reviewers=[{"id": 1, "type": "User"}])], deployment_branch_policy={"custom_branch_policies": True})
        policies = {"branch_policies": [{"name": "main", "type": "branch"}]}
        production_environment(env, policies)
        for bad in (env | {"protection_rules": []}, env | {"deployment_branch_policy": {}}, env | {"protection_rules": [dict(type="required_reviewers", reviewers=[])]}, env | {"protection_rules": [dict(type="required_reviewers", reviewers=[{"id": 1, "type": "Bot"}])]}):
            with self.subTest(env=bad), self.assertRaises(ValueError): production_environment(bad, policies)
        with self.assertRaises(ValueError): production_environment(env, {"branch_policies": [{"name": "*"}]})
        project = {"success": True, "result": {"production_branch": "release-production", "source": {"config": {"production_deployments_enabled": False}}}}
        hosting(project)
        for bad in ({"success": False}, {"success": True, "result": project["result"] | {"production_branch": "main"}}):
            with self.assertRaises(ValueError): hosting(bad)
        project["result"]["source"]["config"]["production_deployments_enabled"] = True
        with self.assertRaises(ValueError): hosting(project)


if __name__ == "__main__": unittest.main()
