// Local-only groundwork. There is deliberately no vendor transport or script loader.
// A future reviewed launch must change the notice version and obtain fresh consent.
window.spellbookPrivacy = (() => {
  const key = 'spellbook.privacy.v1';
  const allowed = new Set(['page_view', 'deck_created', 'card_added', 'backup_exported', 'feedback_opened']);
  let choice = { usage: false, recordings: false };
  let unsavedChoice = false;
  function read() {
    if (unsavedChoice) return { ...choice };
    try {
      const saved = JSON.parse(localStorage.getItem(key));
      choice = { usage: saved?.usage === true, recordings: saved?.recordings === true };
    } catch { choice = { usage: false, recordings: false }; }
    return { ...choice };
  }
  function setConsent(usage, recordings) {
    choice = { usage: usage === true, recordings: recordings === true };
    try {
      localStorage.setItem(key, JSON.stringify(choice));
      unsavedChoice = false;
      return true;
    } catch {
      unsavedChoice = true;
      return false; // Denial/revocation still applies to this page in memory.
    }
  }
  function track(name, ...extra) {
    // Reject arbitrary payloads, URLs, titles and identifiers. Never queue events.
    if (!allowed.has(name) || extra.length || !choice.usage) return false;
    return false; // Launch is unavailable, even when consent is present or storage is forged.
  }
  return Object.freeze({ read, setConsent, revoke: () => setConsent(false, false), track });
})();
