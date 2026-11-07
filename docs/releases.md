# Versioned releases and rollback

Tracking: #31; post-merge CI recovery: #36. These workflows do **not** activate production, publish a release, change protection settings, or provision a service when merged. Production acceptance remains blocked until the owner completes the settings and observed deployment/rollback drill below.

## Contract

Current application and first release version: **0.1.0** (`v0.1.0` tag). Planned pre-GA phase minors are Phase1 0.1.x, Phase2 0.2.x, Phase3 0.3.x and Phase4 0.4.x. Each completed phase increments MINOR. Significant releases within a phase increment PATCH/revision (0.1.1, 0.1.2). General availability is **1.0.0**, based on readiness independently of phase number. If GA occurs earlier, subsequent phases increment the current 1.x MINOR; never revert to a 0.x version. Keep current drafts at 0.1.0; bump to 0.2.0 only when preparing the Phase2 release, not merely because Phase2 work has started. Candidates may append `-rc.N` to the same core version. Never reuse, move, or delete a published version.

The build reads the app's actual MSBuild `Version` without rebuilding and binds it as `app_version` in the immutable artifact manifest. Candidate, promotion and rollback refuse a tag whose core version differs from that artifact (for example, a 0.1.0 binary cannot become v0.2.0). Update the app version before CI when preparing a new release; do not edit an archived ZIP's metadata. Review generated notes and add preview URL, Safari results, behavior changes, known limitations, and migration/recovery instructions.

PRs retain required `build`, `Test coverage`, and GitHub `CodeQL` checks. Cloudflare PR previews remain separate from production. Main is integration, not production promotion. CI publishes once: its `spellbook-site` artifact now contains `spellbook-site.zip` and `SHA256SUMS`, rather than loose deployable files. Inside the ZIP, `release-manifest.json` records the full tested commit, CI run ID, compiled app version and SHA-256 of every static file.

**Prepare release candidate** downloads the existing artifact from a successful real main CI run, checks CodeQL for that SHA, verifies every file and runs one browser journey against the exact ZIP. It creates a **draft** GitHub Release containing ZIP, checksum and evidence. It never restores, builds or publishes .NET again. PR artifacts cannot become main candidates. Expired CI artifacts require a fresh main CI run and candidate; never reconstruct a prior release for rollback.

The owner publishes the reviewed draft with release immutability enabled. Published assets outlive the 14-day Actions artifact retention. Keep current and prior known-good releases. **Promote or roll back release** accepts only a published immutable release; it verifies GitHub's asset digest, per-file manifest, tag SHA, original main CI/security gates and prior artifact. It repeats the saved-data browser gate, then uploads those same static bytes using locked Wrangler. Promotion and rollback never rebuild application code.

## Owner activation plan — review before settings changes

These changes require verified owner access. An API failure is a blocker, not evidence that protection exists. No secret values belong in source, chat or issues.

1. Confirm the existing Cloudflare Pages project `spellbook`, currently served at `https://spellbook-ag4.pages.dev/`. Preserve custom domains and PR previews. Do not create another project, paid Worker, R2/D1 database, Azure resource or API.
2. Enable GitHub **release immutability** before publishing the first candidate. Add an active tag ruleset for `v*` preventing updates/deletion. Review tag creation permissions; any draft-workflow bypass must be narrow and owner approved. Preserve branch/security protections.
3. Configure a GitHub **production** environment with at least one required reviewer and a custom deployment branch policy allowing only the `main` branch, no tags. Preflight verifies the reviewer and branch policy before entering the environment. The current repository has one owner, so that owner may manually approve a deployment; `prevent self-review` can be enabled if an independent reviewer becomes available or the project adopts that policy. If required environment protections are unavailable on the current public repository's free plan, leave promotion disabled; do not buy a plan or remove checks.
4. Bind `CLOUDFLARE_API_TOKEN` to that environment only, with Pages edit permission scoped to the existing account; bind `CLOUDFLARE_ACCOUNT_ID` as an environment variable. Enter credentials through settings UI, never chat. Repository source access does not establish secrets or Cloudflare access.
5. Review the hosting transition before applying it: set the existing Pages production branch to `release-production`, disable automatic **production** Git deployments, and retain PR previews. Workflow preflight requires live `production_branch == release-production` and `source.config.production_deployments_enabled == false`. Verify a Git change cannot bypass promotion. Confirm direct Wrangler upload still works on this Git-connected project with automatic production builds disabled. Different API/settings are a blocker requiring review, not permission to bypass controls.
6. Confirm free-plan limits and disable paid overage in both accounts. Standard public-repository GitHub-hosted runners are free; artifact storage has plan quotas, so CI artifacts expire after 14 days. GitHub Release assets retain rollback files separately. Cloudflare Pages Git previews consume the free build allowance (currently 500/month); direct upload avoids another hosting build. Check current asset size/count, deployment and storage limits before activation. No infrastructure or paid API is provisioned by this flow.

No repository variable alone asserts these protections: workflows query GitHub and Cloudflare and fail closed. Do not dispatch production while settings/access are unverified.

## Candidate, production and rollback drill

1. After merge, select a successful main **Build Spellbook** run with retained artifact and same-SHA successful CodeQL. Reconciliation can recover a missing bot-merge push run; PR coverage never becomes a fabricated main baseline. Failed main runs require fixing or explicit rerun.
2. Dispatch **Prepare release candidate** from main with run ID and a new version. Review draft checksum, SHA, CI and browser evidence, add preview URL. Manually verify Safari/iPhone create/edit/reload/export; Chromium automation does not establish Safari behavior.
3. Deliberately select a failed or PR run: validation must fail without a release/deployment. Automated contracts also cover failed checks, corruption, path traversal, missing protections and competing automatic production deploys.
4. After owner approval of the final artifact and settings, publish the draft; verify API `immutable: true`. Dispatch **Promote or roll back release** from main with version, exact SHA, `promote`, and current known-good release as `previous_version`. `FIRST` is only for initial activation after reviewing a backup of the existing unversioned deployment, which this release workflow cannot restore.
5. A configured required reviewer approves the environment job. For the current single-owner repository, this is a manual approval by the owner. Record returned Cloudflare deployment URL, public-domain smoke results, version/SHA and prior version in release/issue. Fetch public `/release-manifest.json` to confirm expected commit/run ID; upload success alone does not establish that the public domain serves it.
6. Create/edit a deck and save a JSON backup at the public origin. Dispatch the same workflow with the prior immutable tag/SHA, `rollback`, and current tag as `previous_version`. It verifies and smoke-tests the old ZIP, then uploads it without rebuilding. Confirm public manifest changed back and saved CRUD/export work. Do not close #31 until this observed drill passes.

Cloudflare activates completed uploads atomically. Failed pre-deploy gates make no hosting write. Inspect live status after transport failure before retrying. If a completed upload fails public smoke, explicitly redeploy the prior release; do not claim production remained unchanged after an accepted upload.

## Saved data and cache compatibility

One browser journey starts with existing `spellbook.decks.v1` data, edits/reloads, changes quantity, validates JSON export, creates/deletes a separate deck and rejects browser exceptions. It runs before promotion or rollback. Unit contracts prove both operations extract identical old bytes. No service worker currently exists; hashed Blazor runtime assets handle normal cache updates. Verify public manifest and Safari reload during the drill. If introducing a service worker, add update/offline/cache-version tests first.

Keep v1 data/JSON readable in compatible releases. Future migrations need original-data backups, explicit recovery and backward compatibility. Deploying old frontend code does not undo a migration. Block rollback across incompatible schema changes until owner-reviewed recovery exists.

## Local verification

```sh
python3 -m unittest discover -s .github/release-tools -p 'test_*.py' -v
python3 .github/release-tools/release.py package publish/wwwroot release-bundle COMMIT_SHA CI_RUN_ID APP_VERSION
python3 .github/release-tools/release.py verify release-bundle/spellbook-site.zip COMMIT_SHA --destination .github/release-site
cd .github/release-tools
npm ci --ignore-scripts --no-audit --no-fund
npx playwright install chromium
npx playwright test
```

References: [immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases), [Pages Git controls](https://developers.cloudflare.com/pages/configuration/git-integration/), [direct upload](https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration/), [Pages limits](https://developers.cloudflare.com/pages/platform/limits/).
