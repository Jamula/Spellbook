# Randomized Usage Testing

Use this skill to run reproducible, seeded browser sessions on iPhone WebKit and turn failures into trackable, test-backed fixes.

## Scope and safety
- Use only synthetic deck names, card names, and deck-list text generated from the provided seed.
- Do not use real user decks, photos, videos, account data, or private feedback text.
- Keep test traffic local to the published app origin. Do not call paid APIs or upload private media.
- Treat browser storage as local-only: it does not sync across devices.

## Setup
1. Build and publish the static site:
   - `dotnet restore Spellbook/Spellbook.csproj`
   - `dotnet build Spellbook/Spellbook.csproj -c Release`
   - `dotnet publish Spellbook/Spellbook.csproj -c Release -o publish`
2. Install browser tooling:
   - `npm install --prefix tests/browser --no-save --no-package-lock --no-audit --no-fund playwright@1.56.1`
   - `./tests/browser/node_modules/.bin/playwright install --with-deps webkit`
3. Serve published output:
   - `python3 -m http.server 4173 --bind 127.0.0.1 --directory publish/wwwroot`

## Run command
Use the randomized runner from a second terminal:

`node tests/browser/randomized-usage.runner.mjs --iterations 3 --seed issue-68-baseline --report /tmp/spellbook-randomized-usage-report.json`

Supported flags:
- `--iterations <1..12>`: bounded run count (default `3`)
- `--seed <text>`: base reproducibility seed
- `--start-iteration <n>`: rerun a failing iteration number
- `--flows deck,importExport,settings,cardIntake`: isolate specific flows for minimization
- `--base-url <http(s)://...>`: app origin (default `http://127.0.0.1:4173`)
- `--report <path>`: JSON report output path

## Covered flows per iteration
Each iteration shuffles and executes supported user flows:
- Deck create/edit and card quantity edits
- JSON import/export and deck-list import/export
- Settings/privacy controls
- Local card intake from synthetic image data

Every iteration logs:
- deterministic seed (`<base>:iteration:<n>`)
- flow order
- pass/fail status
- failing step and stack when applicable

## Triage and minimization
1. Re-run the exact failing seed/iteration:
   - `--seed <base-seed> --start-iteration <n> --iterations 1`
2. Minimize by flow isolation:
   - `--flows deck` (then try `importExport`, `settings`, `cardIntake`)
3. Confirm reproducibility in at least two local reruns.
4. Open/update the linked GitHub issue with seed, iteration, failing step, and report snippet.
5. Fix the defect and add regression coverage in the most direct existing test layer (node test, component test, or browser test).
