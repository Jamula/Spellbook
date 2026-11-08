---
name: Randomized Usage Testing
description: Run reproducible Playwright simulations of Spellbook user flows, investigate failures, and turn confirmed product defects into tracked fixes with regression coverage.
---

# Randomized Usage Testing

Use this skill when validating Spellbook through realistic, varied browser sessions. Keep runs local, use synthetic decks/cards only, and block outbound card APIs unless a scenario explicitly tests them.

## Run

1. Build and publish the app locally:

   ```sh
   dotnet restore Spellbook/Spellbook.csproj --locked-mode
   dotnet publish Spellbook/Spellbook.csproj -c Release --no-restore -o publish
   SPELLBOOK_STATIC_ROOT=publish/wwwroot node tests/browser/serve-static.mjs
   ```

2. In another terminal, install the pinned Playwright runner dependency if needed, then run ten seeded sessions:

   ```sh
   npm install --prefix tests/browser --no-save --no-package-lock --no-audit --no-fund playwright@1.56.1
   RUT_ITERATIONS=10 RUT_SEED=20261108 node --test tests/browser/randomized-usage.test.mjs
   ```

3. Change `RUT_ITERATIONS` to set N. Change `RUT_SEED` to reproduce the same sequence. Every test reports its seed; failures also report the current step, URL, browser errors, and save a screenshot in `test-results/randomized-usage/`.

## Triage

- Re-run a failing seed by setting `RUT_ITERATIONS=1` and `RUT_SEED` to the printed seed.
- Reduce the failure to the shortest user sequence and confirm it in a fresh browser context.
- For a confirmed product defect, create a GitHub issue with the seed, steps, expected and actual behavior, and screenshot evidence. Fix it on a focused branch and add a regression test in the appropriate existing test suite. Use MSTest for .NET tests; do not add xUnit.
- Do not file issues for test harness mistakes, network restrictions, or known service-configuration blockers. Record those as harness/setup results instead.
- Re-run the seed and the full N-session suite after the fix. Report the seed, N, browser, test/build result, created issue, and PR.

## Safety and cost

Use only synthetic deck names and card titles. The runner must not submit feedback, create production data, call paid services, or upload photos/videos. Block external card search endpoints by default. Do not automatically create GitHub issues for flaky or unconfirmed failures; reproduce first.
