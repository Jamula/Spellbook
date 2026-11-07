# Spellbook · Magic Deck Lab

A **local-first .NET 10 Blazor WebAssembly** starter for Magic: The Gathering deck management. Designed to run in mobile Safari and desktop browsers, with **no required paid services**.

## Features in this starter
- Create, rename and delete decks; choose a format, including casual and Two-Headed Giant.
- Add cards by name; adjust quantities.
- Autosave deck data in browser `localStorage`.
- Import and export complete collections as JSON files.
- Select images or videos from the iPhone photo library (names only; no file upload or recognition in this release).
- Responsive mobile layout and web app manifest.

## Run locally

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet restore Spellbook/Spellbook.csproj
dotnet run --project Spellbook/Spellbook.csproj
```

Use the URL shown in the terminal. To publish static assets for Cloudflare Pages:

```bash
dotnet publish Spellbook/Spellbook.csproj -c Release -o publish
```

The deployable static site is `publish/wwwroot`. Cloudflare Pages builds can run in CI where .NET 10 is installed, or you can upload the published directory as an artifact.

## Tests and code coverage

Run the component workflow, persistence, import validation, export, and card-count tests with:

```bash
dotnet test Spellbook.Tests/Spellbook.Tests.csproj -c Release --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory coverage
cp coverage/*/coverage.cobertura.xml coverage.cobertura.xml
python3 .github/scripts/check-coverage.py coverage.cobertura.xml
```

Use a fresh `coverage` directory for each run. Coverage measures the application assembly, including UI code; test code and generated build files are excluded. Two bUnit component tests exercise the rendered deck editor: deck/card editing and restoration/import/export/media selection. The tests use a fake browser storage boundary. They do not measure JavaScript or replace browser testing.

GitHub Actions runs the tests, enforces an 80% minimum application line coverage, saves a downloadable Cobertura report, and uploads it directly to GitHub's built-in coverage service. No separate service, secret, or paid API is required. Fork PRs run tests and retain the report without requesting write access to the base repository. The `Test coverage` check should be required in branch rules so merging waits for coverage upload. Coverage on `main` establishes the baseline after merge.

## Cost & privacy safeguards
- No Azure resources, paid APIs, or external services are required.
- Cards are entered manually; no automatic Scryfall lookup or AI usage yet.
- Data exists **only in the current browser** unless exported. Clearing browser data can erase it. **Export backups regularly.**
- JSON imports are merged as new decks; duplicate names are allowed.
- Card media remains on your device; the picker does not send files anywhere.
- This is starter source, **not yet compiled or deployed**. CI will validate builds when connected to a repository.

## Next milestones
1. Add Scryfall name validation and card metadata (respect fair-use limits and cache appropriately).
2. Support common decklist text formats (Moxfield and Archidekt exports).
3. Add photo review and on-device frame extraction; ensure all automatic recognition is optional and cost-gated.
4. Optional GitHub synchronization with safe OAuth, version history, and backup recovery.
5. Deck synergies, 2HG analysis, and offline support.

## Proposed repository
`jamula/spellbook` (private). No GitHub repository is created by this archive.
