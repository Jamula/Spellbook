# Default pull request auto-merge

Ready pull requests targeting the default branch automatically opt into GitHub's
native squash auto-merge when opened, reopened, marked ready, or updated. Keep a
PR as a draft while it needs more work. GitHub waits for branch requirements;
this workflow never uses an administrator bypass or directly merges a PR.

The workflow must be merged into the default branch before it can run for future
pull requests. Existing PRs can be enrolled once with:

```sh
gh pr merge NUMBER --repo Jamula/Spellbook --auto --squash
```

Repository settings must allow auto-merge and squash merging. An active ruleset
on the default branch must require pull requests and the GitHub Actions checks
`build` and `Test coverage`. Preserve all existing code-quality and security
rules. Without required branch rules, GitHub rejects auto-merge requests; the
workflow reports that error rather than merging without gates.

The `pull_request_target` workflow has write permissions only to request native
auto-merge. It does not check out code, install dependencies, evaluate PR titles,
or execute scripts from a PR. It passes the PR node ID as a quoted API variable.
Draft and closed PRs are skipped; already-enabled PRs are handled idempotently.

CI, coverage uploads, previews, and deployment retain their existing behavior.
No paid services or additional credentials are needed.
