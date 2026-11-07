# Jamula branded short links

Source for a **separate** free Cloudflare Pages static site. This site is not the Spellbook Blazor application.

- Git repository: `Jamula/Spellbook` (temporary home; can split into its own repo later).
- Pages project name: `jamula-links` (desired), output directory: `shortlinks`, build command: empty.
- Supported branded hosts: `s.jamula.net`, `s.jamula.com` (same redirect table on each).
- Redirects configured in [`_redirects`](./_redirects); additions need a GitHub issue/PR.

## Migration checklist

1. Create Cloudflare Pages project and deploy `shortlinks/` **before changing DNS**. Verify `https://<assigned-subdomain>.pages.dev/spellbook` returns 302 to the correct destination.
2. Under Pages > Custom domains, add **both** `s.jamula.net` and `s.jamula.com`. These are subdomains and work with external Namecheap DNS; no nameserver migration is needed.
3. In each Namecheap DNS zone, update **only** the `s` CNAME from Bitly `cname.bitly.com` to the **actual assigned Pages hostname**, after adding the corresponding custom domain in Pages. Leave MX and other DNS untouched.
4. Wait for DNS propagation and Cloudflare TLS activation. Test `https://s.jamula.net/spellbook` and `https://s.jamula.com/spellbook` in mobile and desktop browsers and confirm redirects.
5. Keep `https://bit.ly/sp3llbook` working as a fallback. Don't remove Bitly records until both branded links work.

These are server-side 302 redirects; no application server, database or pay-per-request API is required. Cloudflare Pages limits and acceptable use apply.

References: https://developers.cloudflare.com/pages/configuration/custom-domains/ and https://developers.cloudflare.com/pages/configuration/redirects/
