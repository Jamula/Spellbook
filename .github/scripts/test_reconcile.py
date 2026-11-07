import datetime
import unittest
from unittest.mock import patch
from reconcile import reconcile


class MainReconciliation(unittest.TestCase):
    def test_missing_bot_ci_dispatches_but_human_runs_and_races_do_not(self):
        now = datetime.datetime(2026, 11, 7, 12, tzinfo=datetime.timezone.utc)
        commit = {"sha": "a" * 40, "commit": {"committer": {"date": "2026-11-07T11:00:00Z"}}}
        existing = {"head_sha": "a" * 40, "head_branch": "main", "event": "push"}
        with patch("reconcile.api", side_effect=[commit, {"workflow_runs": []}, commit, None]) as api:
            self.assertIn("Dispatched real main", reconcile(now))
            self.assertEqual(("actions/workflows/build.yml/dispatches", {"ref": "main"}), api.call_args.args)
        for event in ("push", "workflow_dispatch"):
            with self.subTest(event=event), patch("reconcile.api", side_effect=[commit, {"workflow_runs": [existing | {"event": event}]}]) as api:
                self.assertIn("already has CI", reconcile(now)); self.assertEqual(2, api.call_count)
        with patch("reconcile.api", return_value=commit) as api:
            self.assertIn("recent", reconcile(now - datetime.timedelta(hours=1)))
            self.assertEqual(1, api.call_count)
        with patch("reconcile.api", side_effect=[commit, {"workflow_runs": []}, commit | {"sha": "b" * 40}]) as api:
            self.assertIn("advanced", reconcile(now)); self.assertEqual(3, api.call_count)
        # A PR run for the commit is never reported as a default-branch baseline.
        with patch("reconcile.api", side_effect=[commit, {"workflow_runs": [existing | {"event": "pull_request"}]}, commit, None]):
            self.assertIn("Dispatched real main", reconcile(now))


if __name__ == "__main__": unittest.main()
