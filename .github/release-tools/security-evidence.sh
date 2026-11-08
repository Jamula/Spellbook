#!/usr/bin/env bash
# Metadata only. Resolve GitHub-managed SECURITY workflow, never Code Quality.
set -euo pipefail
sha=$1
[[ "$sha" =~ ^[a-f0-9]{40}$ ]]
gh api "repos/$GH_REPO/commits/$sha/check-runs?per_page=100" > checks.json
gh api "repos/$GH_REPO/actions/runs?head_sha=$sha&per_page=100" > security-runs.json
security_id=$(jq -er '[.workflow_runs[] | select(.path == "dynamic/github-code-scanning/codeql" and .head_branch == "main")] | sort_by(.run_number) | last | .id // empty' security-runs.json)
gh api "repos/$GH_REPO/actions/runs/$security_id" > security-run.json
gh api "repos/$GH_REPO/actions/runs/$security_id/jobs?per_page=100" > security-jobs.json
jq --slurpfile run security-run.json --slurpfile jobs security-jobs.json \
  '. + {security_run: $run[0], security_jobs: $jobs[0]}' checks.json > security-evidence.json
