# Spellbook development guidelines

- Track work in GitHub issues; link each PR to an issue and update the issue when delivered.
- Use .NET 10 and Blazor WebAssembly. Keep iPhone Safari a first-class target.
- Prefer .NET first, Node.js/JavaScript/TypeScript second, Rust third, and Python last.
- Core functionality must have **$0 recurring infrastructure cost**. Do not provision billable services, enable paid APIs, or commit cloud credentials without explicit owner approval.
- Prefer browser-local deck data plus user-controlled JSON export; explain that browser storage does not sync across devices.
- Do not upload user photos/videos automatically. Require clear consent before any remote processing.
- Before a PR, run `dotnet restore Spellbook/Spellbook.csproj`, `dotnet build Spellbook/Spellbook.csproj -c Release`, and `dotnet publish Spellbook/Spellbook.csproj -c Release`.
- Run the MSTest suite with .NET 10 using `dotnet test --project Spellbook.Tests/Spellbook.Tests.csproj -c Release`.
- Reference the roadmap in #1 and use focused, testable tasks (#2-#8).

## Autonomous delivery standard (#103)

Follow [CONTRIBUTING.md](CONTRIBUTING.md). Keep the existing 97% measured C# application line coverage gate; adopt 90% measured branch coverage after baseline/remediation and verification. Test meaningful success, invalid input, exception, authorization, cancellation, recovery and relevant concurrency paths. Do not weaken gates or fabricate results. Use synthetic data and mocked providers.

Use one approved, non-overlapping issue scope per branch/PR; inspect current main and open PRs before working. Keep incomplete work draft. Batch fixes, run focused checks locally and complete pinned CI before merge. Require independent approval of the exact final head, resolved review conversations and all required current checks before enrollment in native auto-merge. A review job completing is not itself approval. Verify actual ruleset enforcement; never bypass missing review or checks. Preserve production, privacy, cost and rollout controls.

Retain the existing two-agent cap in docs/ai-usage.md until verified included quotas, cancellation, isolation and successful execution support scaling. Target four to six independent lanes only after that evidence. Serialize shared/protected edits. No additional spend or duplicate coordinator schedules. Report significant evidence and blockers, not routine no-op updates.
