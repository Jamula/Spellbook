# External deck import contract (draft)
Tracking #32, #37, #85. Existing Settings text/CSV and JSON interchange remains the fallback. Direct service availability is not assumed.

## Adapter design
Accept an explicitly supplied public deck URL or ID for a selected supported provider. Validate exact HTTPS host and path against a provider allowlist, reject credentials, unexpected ports, local/private addresses and unsupported URL shapes. Never fetch arbitrary client-supplied destinations; construct outbound provider API URLs from validated IDs. Block redirects to other hosts, limit request time/response size and rate, and do not forward browser credentials. No authenticated scraping.
Normalize provider entries to the existing card name, quantity, and supported zone model. Record warnings for unknown categories/printings and preserve user review. Preview before writing; import as a new deck by default; replacing requires explicit confirmation and a recovery export. Failed requests and malformed responses must not alter saved decks.
Only use documented provider-supported endpoints and official terms. Investigate Moxfield, Archidekt, Scryfall separately: public deck API permission, quotas, format, attribution, CORS/server requirements, and cost. If no permitted interface is established, show the text-import fallback rather than reverse-engineering an endpoint. Private connections are deferred until #29/#30 and explicit authorization.

## Acceptance matrix
- Supported example public deck produces correct sections and quantities; no saved-state write before confirmation.
- Invalid host/URL, redirect, inaccessible/private deck, timeout, 429, malformed payload and oversized response produce actionable errors and preserve saved decks.
- Quoted commas, split cards, duplicate names across zones and unsupported categories follow existing parser behavior.
- Scryfall paste/export tested in actual deck builder with canonical Avacyn, Angel of Hope, and commander/main/sideboard examples; document date and limitations.
- Native Files picker supports user-controlled locations only; no claim of automatic cloud sync.
- Confirm $0 additional cost, rate limits, no credential leakage and desktop/iPhone accessibility before release.
