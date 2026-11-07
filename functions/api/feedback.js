// Cloudflare Pages Function for public feedback. Requires free Turnstile
// and a server-only, repo-scoped GitHub Issues write credential.
// Never expose GITHUB_FEEDBACK_TOKEN or TURNSTILE_SECRET to browser code.
const allowedOrigin = env => env.FEEDBACK_ORIGIN || "https://spellbook-ag4.pages.dev";
const json = (data, status = 200) => new Response(JSON.stringify(data), {
  status,
  headers: { "content-type": "application/json; charset=utf-8", "cache-control": "no-store", "x-content-type-options": "nosniff" }
});
const configured = env => Boolean(env.TURNSTILE_SITE_KEY && env.TURNSTILE_SECRET && env.GITHUB_FEEDBACK_TOKEN);

export async function onRequestGet({ env }) {
  return json({ enabled: configured(env), siteKey: configured(env) ? env.TURNSTILE_SITE_KEY : null });
}

export async function onRequestPost({ request, env }) {
  if (!configured(env)) return json({ error: "Feedback is unavailable" }, 503);
  // Same-origin submission only, plus single-use Turnstile validation.
  if (request.headers.get("Origin") !== allowedOrigin(env))
    return json({ error: "Origin not allowed" }, 403);
  if (!String(request.headers.get("content-type") || "").toLowerCase().startsWith("application/json"))
    return json({ error: "JSON is required" }, 415);
  if (Number(request.headers.get("content-length") || "0") > 6500)
    return json({ error: "Feedback too large" }, 413);

  let payload;
  try {
    const raw = await request.text();
    if (new TextEncoder().encode(raw).length > 6500) return json({ error: "Feedback too large" }, 413);
    payload = JSON.parse(raw);
  } catch { return json({ error: "Invalid feedback" }, 400); }
  const kind = payload?.kind;
  const title = typeof payload?.title === "string" ? payload.title.trim() : null;
  const details = typeof payload?.details === "string" ? payload.details.trim() : null;
  if (!["Bug", "Feature", "General"].includes(kind) ||
      typeof title !== "string" || title.length < 3 || title.length > 120 ||
      typeof details !== "string" || details.length < 10 || details.length > 4000 ||
      typeof payload.token !== "string" || payload.token.length < 10 || payload.token.length > 4096)
    return json({ error: "Check feedback fields and verification" }, 400);

  const form = new FormData();
  form.set("secret", env.TURNSTILE_SECRET);
  form.set("response", payload.token);
  let verification;
  try {
    const check = await fetch("https://challenges.cloudflare.com/turnstile/v0/siteverify", { method: "POST", body: form });
    verification = check.ok ? await check.json() : null;
  } catch { return json({ error: "Verification unavailable" }, 503); }
  if (!verification?.success || verification.action !== "spellbook_feedback" ||
      verification.hostname !== new URL(allowedOrigin(env)).hostname)
    return json({ error: "Verification failed" }, 403);

  // Explicit user-entered content only. No deck, browser storage, or personal metadata.
  const issueBody = `## Type\n${kind}\n\n## Feedback\n${details}\n\n---\nSubmitted through the Spellbook public feedback form.\n`;
  let created;
  try {
    const result = await fetch("https://api.github.com/repos/Jamula/Spellbook/issues", {
      method: "POST",
      headers: {
        "authorization": `Bearer ${env.GITHUB_FEEDBACK_TOKEN}`,
        "accept": "application/vnd.github+json",
        "user-agent": "Spellbook-Feedback",
        "x-github-api-version": "2022-11-28",
        "content-type": "application/json"
      },
      body: JSON.stringify({ title: `[User feedback] [${kind}] ${title}`, body: issueBody })
    });
    created = result.status === 201 ? await result.json() : null;
  } catch { return json({ error: "Issue creation unavailable" }, 502); }
  if (!created?.html_url || !/^https:\/\/github\.com\/Jamula\/Spellbook\/issues\/\d+$/.test(created.html_url))
    return json({ error: "Issue creation unavailable" }, 502);
  return json({ issueUrl: created.html_url }, 201);
}
