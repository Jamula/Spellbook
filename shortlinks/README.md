# Jamula branded short links

Source for a **separate** free Cloudflare Pages static site. This site is not the Spellbook Blazor application.

- Git repository: `Jamula/Spellbook` (temporary home; can split into its own repo later).
- Pages project name: `jamula-links`, output directory: `shortlinks`, build command: empty.
- Supported branded hosts: `s.jamula.net`, `s.jamula.com` (same redirect table on each).
- Redirects configured in [`_redirects`](./_redirects); additions need a GitHub issue/PR.

## Deployment and verification status

[Issue #17](https://github.com/Jamula/Spellbook/issues/17) records the owner's choice to replace Bitly custom-domain hosting with this free Pages site. Its original Bitly-DNS title predates that decision.

On 2026-11-07, the issue recorded successful deployment to `jamula-links.pages.dev` and Cloudflare API confirmation that both custom domains, DNS verification and SSL validation were active. The deployment parsed one valid redirect rule. Do not recreate the project or repeat the DNS migration solely because the original issue is still open.

**Final public verification remains outstanding.** On 2026-11-07, the managed development environment's enforced network policy rejected HTTPS requests to the branded hosts, Pages hosts and Bitly with proxy tunnel HTTP 403. Local DNS resolution also failed in that environment. These results do not establish whether public DNS or the deployed links are working.

## Complete the public checks

Run these checks from a network that permits the hosts, or allow the five hosts below in the development environment's outbound network settings. No provider credential or paid API is needed for these public checks.

```bash
dig +short CNAME s.jamula.net
dig +short CNAME s.jamula.com

# Do not add --location: inspect the initial response from each short-link host.
for host in jamula-links.pages.dev s.jamula.net s.jamula.com; do
  curl --silent --show-error --fail --dump-header - --output /dev/null \
    "https://$host/spellbook"
done

curl --silent --show-error --fail --output /dev/null \
  --write-out 'Spellbook HTTP %{http_code}\n' https://spellbook-ag4.pages.dev/

curl --silent --show-error --fail --dump-header - --output /dev/null \
  https://bit.ly/sp3llbook
```

Expected results:

| Check | Required result |
| --- | --- |
| Both branded CNAMEs | `jamula-links.pages.dev.` (the deployed project's assigned hostname) |
| All three `/spellbook` responses | HTTP 302; `Location: https://spellbook-ag4.pages.dev/`; valid HTTPS without disabling certificate checks |
| Destination | HTTP 200 and the Spellbook application loads |
| Existing Bitly fallback | Still redirects to the intended Spellbook destination; preserve it |

Then open both branded `/spellbook` links in desktop and iPhone Safari, confirm that Spellbook loads, and record the dated results in #17 before closing it. If a fallback returns an interstitial or an unexpected target, record the actual result rather than treating any 302 as success. The issue also records `https://bit.ly/4iLdcq5` as an earlier fallback; leave existing Bitly links intact.

## Diagnose a failed check

1. Compare the deployed Pages `/spellbook` response with the two custom-domain responses. If Pages itself fails, inspect the latest `jamula-links` deployment and its parsed `_redirects` rules before changing DNS.
2. In Pages > Custom domains, inspect **both** `s.jamula.net` and `s.jamula.com`. Confirm their current DNS and certificate status. These are subdomains using external Namecheap DNS; no nameserver migration is needed.
3. If public DNS is incorrect, verify the current required CNAME in the existing Pages project first. The assigned target recorded in #17 is `jamula-links.pages.dev`. Only after verification and owner authorization, correct the `s` CNAME in the affected zone. Do not restore `cname.bitly.com` for this Pages-hosted link, create competing records, or change MX, nameservers or unrelated records.
4. Wait for DNS propagation and certificate activation, then repeat the public checks. Keep the working fallback available throughout troubleshooting.

Changes to redirect destinations should remain focused issue-linked PRs. Verify the Pages response and both branded links after deployment; retain 302 redirects while destinations are being validated.

These are server-side 302 redirects; no application server, database or pay-per-request API is required. Cloudflare Pages limits and acceptable use apply.

References: https://developers.cloudflare.com/pages/configuration/custom-domains/ and https://developers.cloudflare.com/pages/configuration/redirects/
