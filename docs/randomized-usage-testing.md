# Randomized usage testing

Spellbook includes a seeded Playwright runner for repeatable iPhone Safari-style usage sessions.

## Local run

1. Build and publish:

```bash
dotnet restore Spellbook/Spellbook.csproj
dotnet build Spellbook/Spellbook.csproj -c Release
dotnet publish Spellbook/Spellbook.csproj -c Release -o publish
```

2. Install browser dependency:

```bash
npm install --prefix tests/browser --no-save --no-package-lock --no-audit --no-fund playwright@1.56.1
./tests/browser/node_modules/.bin/playwright install --with-deps webkit
```

3. Serve static output:

```bash
python3 -m http.server 4173 --bind 127.0.0.1 --directory publish/wwwroot
```

4. Run randomized usage:

```bash
node tests/browser/randomized-usage.runner.mjs --iterations 3 --seed local-baseline --report /tmp/spellbook-randomized-usage-report.json
```

## Reproducibility and minimization

The runner emits deterministic iteration seeds in this format:

`<base-seed>:iteration:<number>`

On failure:

1. Reproduce a single iteration:
   - `--seed <base-seed> --start-iteration <n> --iterations 1`
2. Minimize with isolated flows:
   - `--flows deck`
   - `--flows importExport`
   - `--flows settings`
   - `--flows cardIntake`
3. Attach the report JSON and failing seed/step to the linked issue before implementing a fix.

## Safety constraints

- Use synthetic data only.
- The runner blocks non-local network requests so flows stay local-first.
- No paid service provisioning or cloud credentials are required.
