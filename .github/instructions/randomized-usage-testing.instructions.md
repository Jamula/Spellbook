Run deterministic Spellbook randomized usage tests in read-only triage mode.

Requirements:
- Use only local synthetic data derived from the seed.
- Keep browser traffic local to the provided app origin.
- Do not open pull requests, edit files, or create issues automatically.

Process:
1. Run `node tests/browser/randomized-usage.runner.mjs --iterations <n> --seed <seed> --report <path>`.
2. If a failure occurs, rerun with `--start-iteration <failing-iteration> --iterations 1`.
3. Isolate with `--flows deck|importExport|settings|cardIntake`.
4. Return: base seed, failing iteration seed, failing step, exact command(s), and report path.
