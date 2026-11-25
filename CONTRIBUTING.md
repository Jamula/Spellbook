# Contributing to Spellbook

Read [AGENTS.md](AGENTS.md), [roadmap #1](https://github.com/Jamula/Spellbook/issues/1), and [engineering issue #103](https://github.com/Jamula/Spellbook/issues/103).

## Work lifecycle
1. Select one approved issue or independently testable slice. Inspect main, open PRs, dependencies and overlapping files. Use separate branches; serialize shared edits.
2. Run targeted tests locally, including failure and exception paths; batch related fixes. Before merge run the full pinned build/test/coverage/browser/security gates. Report actual commands, results and limitations.
3. Keep work draft until ready. Obtain independent exact-head approval, resolve review conversations and verify required CI/security checks for the final head. Any new commit invalidates earlier evidence; refresh only when needed and review the resulting head. Use native squash auto-merge only under verified required protections. Do not use administrator bypass.
4. Track planned, built, tested, reviewed/merged, deployed and accepted separately. A merged foundation or green build does not establish functional acceptance. Record remaining criteria on the issue.

## Quality
- Preserve **97% measured C# application line coverage**. Adopt **90% branch coverage** as the target; first measure an authentic baseline, remediate shortfalls and verify fail-closed enforcement. No missing/empty/malformed coverage report counts as a pass. Do not claim the branch gate is active before validation.
- Use maintainable behavior-driven tests for success, invalid data, exceptions, authorization, storage/provider failure, retries, idempotency, timeout, cancellation, concurrency and recovery where applicable. Do not delete meaningful regressions to improve metrics. Keep generated/vendor exclusions justified.
- Keep separate Node boundary tests, WebKit mobile checks, randomized usage coverage and CodeQL. Native coverage reports must correspond to the tested commit. Physical iPhone acceptance remains separate.

## PR velocity and agent limits
- Targets: 1–2 validation cycles per ready PR, <5 minutes from fully eligible to merge, investigate open PRs aged 10 hours or more. Measure queue time, execution time, review wait and remediation separately. A 30-second focused CI target is a stretch goal, not a deadline for full .NET publish/WebKit/security.
- Keep existing two-agent cap until an evidenced pilot, verified included capacity, cancellation and isolation support increasing it. Target 4–6 independent lanes eventually, never exceed authorized quotas or incur incremental spend. Do not treat branches or drafts as live agents.
- Reuse the existing hourly engineering coordinator; no duplicate automation. Propose an eight-hour retrospective in that coordinator's existing scope: aging PRs, repeat failures, test time, review cycles, actual merges, actionable process improvements. Do not claim scheduling changes without verification.

## Security and releases
Use synthetic decks and non-sensitive media; no production credentials or private user content. Preserve the local-first model, existing versioning, free-tier limits and explicit production activation rules. Never weaken required checks for velocity. See [CI](docs/ci.md), [auto-merge](docs/auto-merge.md), [AI usage](docs/ai-usage.md), and [releases](docs/releases.md).
