# Deploy Spellbook to Cloudflare Pages (free-first)

Spellbook is a **static .NET 10 Blazor WebAssembly** app. No server process or database is needed for its current features. Hosting must remain on a free tier, with paid features disabled unless the owner explicitly approves them.

## Build locally or in GitHub Actions

```sh
dotnet restore Spellbook/Spellbook.csproj
dotnet publish Spellbook/Spellbook.csproj -c Release -o publish
```

The deployable files are under `publish/wwwroot`. The GitHub Actions **Build Spellbook** workflow produces `spellbook-site`, containing a ZIP of those exact files plus integrity metadata. See [releases.md](releases.md) for extraction and production controls.

## Create a free Pages project

1. In Cloudflare Dashboard, go to **Workers & Pages** and create a **Pages / Direct Upload** project named `spellbook` if the name is available. Verify the free-plan terms for your account before proceeding.
2. Do **not** enable paid Workers, R2, D1, Azure, or AI APIs. Do not attach payment methods specifically for this app.
3. Authenticate Wrangler in a trusted developer environment (never commit access tokens):

```sh
npx wrangler login
npx wrangler pages deploy publish/wwwroot --project-name spellbook
```

Cloudflare should return a `*.pages.dev` deployment address. Project setup and deployment have **not** been performed by this document.

## Optional automated deployment

The existing project uses Git integration for previews. The owner-reviewed
activation plan in [releases.md](releases.md) supersedes automatic main-to-production
deployment; do not add another production job or retain a competing Git path.

When the Pages project exists and the user chooses to automate releases, create a narrowly scoped Cloudflare API token and save it as a GitHub Actions secret named `CLOUDFLARE_API_TOKEN`. Set `CLOUDFLARE_ACCOUNT_ID` as a repository variable. Have the workflow publish and upload `publish/wwwroot` using Wrangler; do not put secrets in a Blazor client app or repository.

## Verify on an iPhone

1. Open the HTTPS `*.pages.dev` URL in Safari.
2. Create a deck, add and adjust a card, reload the page and confirm it persists.
3. Export JSON to Files and import it again.
4. Confirm the file picker can select a photo and video (the starter **does not recognize cards**).
5. Add the web app to the Home Screen and test launching it.
6. Check that no external images/videos or deck data are uploaded during ordinary editing.

**Data caution:** Decks live in the current browser's localStorage. They are not automatically synchronized to another device, and clearing Safari site data can erase them. Export backups regularly.

## References

- Cloudflare Pages direct upload: https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration/
- GitHub Actions: `.github/workflows/build.yml`
- Tracking: #4 (deployment), #12 (GitHub merge security checks)
