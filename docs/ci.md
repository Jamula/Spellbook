# CI checks

`Build Spellbook` runs once per pull request update and on pushes to `main`.
Feature-branch pushes do not run a second copy of the same workflow. Pushes to
`main` still publish the deployable site artifact and establish the default-branch
coverage baseline. Superseded runs for the same PR or ref are cancelled.

The required check names are unchanged:

| Check | Purpose |
| --- | --- |
| `build` | Restore, Release build/publish, validate and retain static assets |
| `Test coverage` | Run .NET tests, enforce coverage, retain and upload Cobertura |
| `CodeQL` | GitHub's security-scanning summary required by branch rules |

GitHub also manages two distinct analysis workflows outside this repository's
workflow files:

- **CodeQL** (`dynamic/github-code-scanning/codeql`) analyzes security issues,
  including GitHub Actions workflow code.
- **CodeQL - Code Quality** (`dynamic/github-code-quality/codeql`) analyzes
  reliability and maintainability issues and supplies quality-rule findings.

Both can display jobs called `Analyze (csharp)` and `Analyze
(javascript-typescript)`. Those matching names do not mean the analyses serve
the same purpose. The CodeQL summary is an aggregate check, not another full
analysis run. Keep these protections unless replacing them with an explicitly
equivalent configuration. They are managed through repository security/quality
settings, not duplicate YAML files.

**Enable auto-merge** manages PR metadata and native merge enrollment; it does
not run the application test suite. **Cloudflare Pages** validates/deploys the
static site through the existing hosting integration. Neither duplicates the
.NET build/test checks above.

To verify a PR, inspect its Actions runs: there should be one `Build Spellbook`
run with event `pull_request` per update, not a second `push` run for the feature
branch. A merge into `main` intentionally starts a new baseline/deployment build.

## Reproducibility and performance

Both jobs restore in locked mode using committed NuGet lockfiles. When changing
dependencies, run `dotnet restore Spellbook.Tests/Spellbook.Tests.csproj
--force-evaluate`, review the lockfile changes, and commit them with the project
changes. `setup-dotnet` caches NuGet packages using those lockfiles as keys.
Package verification remains enabled. A changed dependency graph invalidates the
cache rather than silently using old versions.

`global.json` pins the SDK used by CI and local development, including the
SDK-supplied WebAssembly/ILLink dependencies in the app lockfile. Install that
SDK when setting up a development or hosting build environment. Update the SDK
pin and regenerate/review lockfiles together for SDK upgrades; a moving
`10.0.x` SDK selection could otherwise invalidate an unchanged package graph.

Build and test jobs run independently. The build job compiles once, treats
compiler warnings as errors, and publishes that Release build with `--no-build`.
The coverage job builds its instrumented test target separately; it does not
depend on downloaded build outputs or a serialized job chain.

xUnit runs independent test classes/collections concurrently, bounded by the
runner's CPU count (`maxParallelThreads: 0`) with conservative async scheduling.
Each test owns its bUnit context or fake runtime. Avoid global mutable state,
network calls, sleeps, and filesystem fixtures shared between tests. Prefer a
few behavior-focused workflows that verify outcomes and cover meaningful paths;
test count is not a quality target. Keep non-thread-safe tests isolated in a
dedicated collection rather than disabling parallel execution for the suite.

## Results and diagnostics

The test job writes TRX results and reports actual counts, skips, failures, and
duration to the Actions summary. A missing report, zero executed tests, or an
unsuccessful run fails validation. VSTest interrupts a test that hangs for two
minutes; process dumps are disabled, and the job has a separate 15-minute limit.

`spellbook-coverage` retains raw Cobertura and TRX diagnostics for 14 days even
if the test or coverage gate fails, provided files were produced. The original
test command's failed status is preserved. The static site artifact also lasts
14 days; longer-lived release artifacts belong to the Phase2 release process.
The existing coverage threshold and GitHub coverage upload remain enforced.
