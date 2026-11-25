# Physical-device acceptance checklist
Tracking #4, #17, #31, #79. Record actual app version, date, device/iOS/Safari version, network and pass/fail evidence. Use disposable test decks and non-sensitive test media. A simulator is not physical-device acceptance.

| Check | Procedure | Pass condition |
| --- | --- | --- |
| Direct and branded URLs | Open https://spellbook-ag4.pages.dev/, https://s.jamula.net/spellbook and https://s.jamula.com/spellbook in desktop and iPhone Safari | Correct app, valid HTTPS, no warning/interstitial |
| Install | Safari Share > Add to Home Screen; launch icon | Correct icon/name and standalone view |
| Persistence | Create test deck, add a card, edit quantity; quit and relaunch | Same deck and quantity |
| Offline | After successful online load, disconnect network and relaunch; edit disposable deck; reconnect | Expected offline behavior documented, no silent loss; manual entry available when app loads |
| Backup | Export JSON to Files; restore in isolated test context | Filename, contents and deck data verified; no overwriting unrelated decks |
| Scanner | Select short privacy-safe iPhone MOV, scan immediately, cancel, retry | Results or specific recoverable error; cancelled work does not add cards; no automatic upload |
| Feedback | Use disposable text only after credentials configured and explicit public consent | Exactly one public issue, correct URL, error preserves text |
| Release | Use existing release runbook only after protection settings verified | Tested immutable artifact promoted and prior known-good artifact restored; no bypass |

For #17, do not treat a Bitly warning page returning HTTP 200 as a successful redirect. Keep direct app URL as fallback. For #79, original clip or equivalent reviewed fixture is still required for reported failure. For #31, see docs/releases.md: do not disable automatic production deployment before protected promotion is operational. Record unresolved failures on their respective issues rather than closing on emulation alone.
