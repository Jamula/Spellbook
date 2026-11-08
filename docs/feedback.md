# Spellbook public feedback endpoint (P0 #38)

The in-app modal is the UI for **public user-created GitHub issues**. Cloudflare Pages Functions provides `GET /api/feedback` (public availability and Turnstile site key) and `POST /api/feedback` (server-side validation and issue creation). The browser never receives the GitHub credential.

## Required free-tier configuration (not provisioned by this code PR)

1. Enable free Cloudflare Turnstile for the exact production hostname `spellbook-ag4.pages.dev`. Add the public **TURNSTILE_SITE_KEY** as a Pages environment variable and **TURNSTILE_SECRET** as a Pages secret.
2. Create a GitHub App installation token mechanism or a repo-scoped fine-grained token authorized **only** to create issues in `Jamula/Spellbook`. Store it solely as a Cloudflare Pages secret named **GITHUB_FEEDBACK_TOKEN**. Prefer a GitHub App with short-lived credentials; if using a fine-grained token, rotate/revoke it and restrict scope to this repository and Issues write. Never paste this secret in issues, code, test fixtures, or a browser setting.
3. Optionally set **FEEDBACK_ORIGIN** to the exact HTTPS origin; the default is `https://spellbook-ag4.pages.dev`. Preview domains are denied until separately authorized.
4. Confirm Cloudflare Pages is deploying the root `functions/api/feedback.js` Functions directory along with the Blazor published `wwwroot` output. A static-only deployment will not expose the endpoint, in which case the modal truthfully reports that submission is unavailable.
5. Verify with a harmless, explicitly reviewed test report from an actual browser. Check that it produces one GitHub issue and that no deck data, token, or user identity appears in its body. Check error and retry behavior. No screenshots are uploaded automatically.

## Security properties

- Native dialog; explicit public-content consent; no stored decks, photo, logs or browser telemetry included.
- Strict category/length/content-type and same-origin checks.
- Each submission requires a one-time server-verified Cloudflare Turnstile token with the expected hostname and action; attempts without valid verification are blocked.
- Issues are posted to a single hardcoded repository, never to a caller-provided path.
- GitHub auth is accessed only server-side; failures return generic errors without leaking credentials.
- Frontend preserves user input after a failed submission.
- This does not yet implement screenshot attachments; a separate explicit opt-in upload design is needed.

## Tests

Run `node --test tests/feedback-worker.test.mjs` for server boundary behavior. C# bUnit tests exercise modal state, privacy, invalid input, successful issue URLs, and failure retry handling. The >=97% C# coverage gate remains enforced.

**Do not close #38 until the live endpoint is configured, end-to-end issue creation succeeds and the iPhone modal receives browser verification.**
