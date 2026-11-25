# Account and private deck storage contract (draft)
Tracking #29, #30, #37. Guest use remains the default. No vendor selected, provisioned, or authorized by this document.

## Authentication boundary
- Use managed OIDC authorization code + PKCE with state/nonce, exact redirect origins and a supported client library. No client secret in WASM.
- Server validates signature (trusted issuer JWKS), algorithm allowlist, issuer, audience, expiry and intended token use. Fail closed if verification cannot be completed. Derive identity from verified (issuer, subject), never email or a supplied user ID.
- Prefer a same-origin session with HttpOnly, Secure cookies and CSRF defense if chosen architecture supports it; otherwise keep access tokens out of persistent browser storage and logs. Document provider-specific refresh and logout semantics before implementation.
- Require account switching, session expiry and logout to clear private cached state without deleting guest decks.

## Storage API proposal
All routes require an authenticated session and enforce owner constraints at the database query, not only the UI.
- GET /api/decks: owner-filtered, bounded pagination.
- GET /api/decks/{id}: owner-filtered, no existence disclosure for another owner.
- POST /api/decks: validate size, schema, quantities, and server-assigned ID.
- PUT /api/decks/{id}: require current revision via If-Match; atomic compare-and-swap; stale revision returns 409 or 412, no silent overwrite.
- DELETE /api/decks/{id}: explicit confirmation in UI, ownership and revision checks server-side.
Use unique (owner_id, deck_id) and a monotonically increasing revision; timestamp is informational, not a concurrency token.
No media, raw identity token, provider secret, or automatic upload of local data.

## Migration and recovery
1. Explicit sign-in and preview a proposed local-to-cloud copy or merge.
2. Preserve local JSON recovery export before migration; keep local data if any step fails.
3. Do not merge decks by display name alone. Resolve ID/name conflicts explicitly and make retry idempotent.
4. Account export/deletion, offline fallback and account switching require acceptance tests before activation.

## Provider selection gates
Evaluate current official free-tier terms for OIDC monthly active users and storage capacity, reads/writes, egress, backup, hard limits and overage behavior. No payment method required, no billable upgrade, $0 recurring cost. Record issuer/audience/redirect and mobile Safari constraints; do not put credentials in source. #29 implementation precedes enabling #30, but contract/test design is independent.

## Acceptance matrix
Anonymous and expired session denied; wrong issuer/audience/signature denied; two users cannot enumerate/read/update/delete each other's IDs; stale revision leaves prior data unchanged; concurrent retries are idempotent; invalid or oversized input leaves decks unchanged; offline/edit failure preserves local recovery; logout/account switch clears other user's visible data; user export/deletion and physical iPhone sign-in verified.
