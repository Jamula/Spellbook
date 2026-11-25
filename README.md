# Spellbook · Magic Deck Lab

A **local-first .NET 10 Blazor WebAssembly** app for Magic: The Gathering deck management. Designed to run in mobile Safari and desktop browsers, with **no required paid services**.

## Features
- Create, rename and delete decks; choose a format, including casual and Two-Headed Giant.
- Add cards by name; adjust quantities.
- Autosave deck data in browser `localStorage`.
- Import and export complete collections as JSON files.
- Select photos and videos for local card recognition and editable review; use Scryfall to verify names. Media is not uploaded by the scanner.
- Responsive mobile layout, list and visual card modes, mana symbols, and web app manifest.

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
dotnet test --project Spellbook.Tests/Spellbook.Tests.csproj -c Release --results-directory coverage --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --coverage-settings coverage.runsettings --report-trx --report-trx-filename tests.trx
cp coverage/coverage.cobertura.xml coverage.cobertura.xml
dotnet run --project .github/scripts/CoverageChecker/CoverageChecker.csproj -c Release -- coverage.cobertura.xml
```

Use a fresh `coverage` directory for each run. Coverage measures the application assembly, including UI code; test code and generated build files are excluded. Component tests exercise deck/card editing, restoration, import/export and media workflows using a fake browser storage boundary. They do not measure JavaScript or replace browser testing.

GitHub Actions runs the tests, enforces a 97% minimum application line coverage, saves a downloadable Cobertura report, and uploads it directly to GitHub's built-in coverage service. No separate service, secret, or paid API is required. Fork PRs run tests and retain the report without requesting write access to the base repository. The `Test coverage` check should be required in branch rules so merging waits for coverage upload. Coverage on `main` establishes the baseline after merge.

## Cost & privacy safeguards
- No Azure resources, paid APIs, or external services are required.
- Manual entry remains available offline; optional Scryfall card search sends search text, not saved decks or media.
- Data exists **only in the current browser** unless exported. Clearing browser data can erase it. **Export backups regularly.**
- JSON imports are merged as new decks; duplicate names are allowed.
- Card media remains on your device; the picker does not send files anywhere.
- The app is deployed at [Spellbook](https://spellbook-ag4.pages.dev/). Physical iPhone installation and offline acceptance are tracked in [#4](https://github.com/Jamula/Spellbook/issues/4).

## Roadmap and repository

See the [roadmap](https://github.com/Jamula/Spellbook/issues/1) and [open issues](https://github.com/Jamula/Spellbook/issues) for current delivery scope, acceptance gates and dependencies. Authentication and private cross-device persistence remain future work; no account sync is enabled.

## Moxfield / Archidekt deck lists

Open **Settings > Import > Deck-list import and export**. Paste an export or choose a `.txt`/`.csv` file, give the new deck a name, and select **Review deck list**. Check every card name, quantity, and zone, acknowledge the review, then import. This creates a new deck and leaves existing decks intact. There is no account connection, URL scraping, automatic card-name lookup, or remote upload.

Supported interchange formats:

- Plain text: `4 Lightning Bolt`, `1x Sol Ring`, optional `(SET) collector-number` printing suffixes, and headings `Mainboard`, `Sideboard`, `Commander`, `Maybeboard` (optional colon/brackets). `SB: 2 Negate` is also accepted. This quantity/name syntax supports Scryfall-style pasted lists; no Scryfall account, URL, or API is used. Split card names such as `Fire // Ice` are preserved.
- Archidekt-style text: `1x Sol Ring (CMM) 410 [Mainboard]`. Recognized board tags set the zone. Custom categories such as `[Ramp]` are omitted with a review warning.
- Comma-delimited CSV: `Quantity,Name,Board`, with aliases `Count`/`Qty`, `Card Name`/`Card`, and `Section`/`Categories`/`Category`. Quoted names containing commas or quotes are supported. A `Commander` column of `true` or `1` marks the command zone. Unknown categories default to Main with a warning; conflicting board categories are rejected.

Quantities must be 1–250; duplicate names in the same zone are combined only when their total remains within that limit. The same card can be present in different zones. Lists are limited to 1,000 distinct entries and files to 2 MB. Invalid input is rejected without saving a partial deck. Names are only syntax-checked: Spellbook cannot confirm that a name is a real Magic card, so review is required.

**Export deck TXT/CSV** downloads the selected deck in normalized common interchange syntax. Select a matching text or CSV import option at the destination and review it there; site-specific custom CSV schemas may need column mapping. Quantities and supported zones round-trip through Spellbook. Card printings, foil choices, prices, custom tags/categories, and deck metadata are not included. Custom zones or unsafe names are rejected on deck-list export; use **Export JSON** for lossless Spellbook collection backups.

The small representative examples in [`samples/decklists`](samples/decklists) cover Moxfield-style text, Archidekt-style text/CSV, and Scryfall-compatible quantity/name text. They are illustrative exports, not downloads from an account. Automated tests import each example and round-trip every card's name, quantity, and zone through both output formats.

## Deck statistics and local card details

Deck statistics run locally and recompute when you edit the selected deck. Open
**Review local card details** to enter printed mana values, type lines and card
colors (`WUBRG`, or `C` for colorless), and explicitly review draw/ramp/removal
roles. Counts are weighted by quantity and include only the `Main` section;
other sections are reported separately. Multicolor cards contribute to every
listed color. The curve excludes known lands and groups mana values of 7 or
more; fractional values round down. A blank detail is unknown, not zero. Split/multiface type lines remain
unknown because a single combined type line cannot establish which face is played. Role
counts are user-confirmed labels, can overlap, and do not prove a card's rules
behavior. Advice explains its basis and is for playtesting, not legality rulings.

Optional card details survive local storage and JSON export/import. Legacy
backups still load; malformed detail values are rejected through the existing
backup-recovery flow without overwriting saved data. The statistics calculation makes no network calls. Scryfall lookups can prefill known card metadata; roles still require user review.
