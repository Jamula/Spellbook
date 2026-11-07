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
