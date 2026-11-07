// Public client code: no GitHub credentials or deck data may appear here.
// Feedback is submitted only after the user reviews the public text.
window.spellbookFeedback = (() => {
  let token = null;
  let scriptPromise;
  let widget;
  function loadTurnstile() {
    if (window.turnstile) return Promise.resolve(window.turnstile);
    if (!scriptPromise) {
      scriptPromise = new Promise((resolve, reject) => {
        const s = document.createElement("script");
        s.src = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";
        s.async = true;
        s.onload = () => window.turnstile ? resolve(window.turnstile) : reject(new Error("Spam prevention unavailable"));
        s.onerror = () => reject(new Error("Spam prevention unavailable"));
        document.head.appendChild(s);
      }).catch(e => { scriptPromise = null; throw e; });
    }
    return scriptPromise;
  }
  async function open(dialog) {
    dialog.showModal();
    token = null;
    try {
      const response = await fetch("/api/feedback", { credentials: "same-origin", cache: "no-store" });
      if (!response.ok) return false;
      const config = await response.json();
      if (!config.enabled || typeof config.siteKey !== "string") return false;
      const turnstile = await loadTurnstile();
      if (widget !== undefined) turnstile.remove(widget);
      widget = turnstile.render(dialog.querySelector(".feedback-turnstile"), {
        sitekey: config.siteKey,
        action: "spellbook_feedback",
        callback: value => { token = value; },
        "expired-callback": () => { token = null; },
        "error-callback": () => { token = null; }
      });
      return true;
    } catch { return false; }
  }
  function close(dialog) {
    dialog.close();
    token = null;
  }
  async function submit(kind, title, details) {
    if (!token) throw new Error("Complete the spam prevention challenge before submitting");
    const response = await fetch("/api/feedback", {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ kind, title, details, token })
    });
    if (!response.ok) throw new Error("Feedback endpoint could not accept the request");
    const result = await response.json();
    if (typeof result.issueUrl !== "string") throw new Error("Missing GitHub issue link");
    token = null;
    return result.issueUrl;
  }
  return { open, close, submit };
})();
