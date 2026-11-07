/* Public Scryfall REST API. Never send deck contents, user media, or personal identifiers.
 * Respect Scryfall's 50-100ms minimum spacing; 150ms here, plus UI debounce.
 * Bounded in-memory cache: no browser persistence of external metadata.
 */
window.spellbookScryfall = (() => {
  const api = "https://api.scryfall.com";
  const cache = new Map();
  let lastRequest = 0;
  let queue = Promise.resolve();
  function pacedFetch(path) {
    const run = queue.catch(() => {}).then(async () => {
      const wait = Math.max(0, 150 - (Date.now() - lastRequest));
      if (wait) await new Promise(resolve => setTimeout(resolve, wait));
      lastRequest = Date.now();
      const response = await fetch(api + path, {
        headers: { "Accept": "application/json" },
        mode: "cors",
        credentials: "omit"
      });
      if (!response.ok) {
        if (response.status === 404) return null;
        throw new Error("Scryfall request failed (" + response.status + ")");
      }
      return response.json();
    });
    queue = run.then(() => {}, () => {});
    return run;
  }
  function put(key, value) {
    if (cache.size >= 100) cache.delete(cache.keys().next().value);
    cache.set(key, { value, expires: Date.now() + 10 * 60_000 });
    return value;
  }
  async function cached(key, url) {
    const existing = cache.get(key);
    if (existing && existing.expires > Date.now()) return existing.value;
    return put(key, await pacedFetch(url));
  }
  async function autocomplete(query) {
    const q = String(query || "").trim();
    if (q.length < 2 || q.length > 160) return [];
    const body = await cached("a:" + q.toLowerCase(),
      "/cards/autocomplete?q=" + encodeURIComponent(q));
    return Array.isArray(body?.data) ? body.data.slice(0, 10) : [];
  }
  async function exact(name) {
    const n = String(name || "").trim();
    if (!n || n.length > 200) return null;
    const body = await cached("e:" + n.toLowerCase(),
      "/cards/named?exact=" + encodeURIComponent(n));
    if (!body || body.object !== "card") return null;
    return {
      name: body.name,
      manaValue: Number(body.cmc) || 0,
      manaCost: body.mana_cost || "",
      typeLine: body.type_line || "",
      colors: Array.isArray(body.colors) ? body.colors : [],
      imageUrl: body.image_uris?.small || body.card_faces?.[0]?.image_uris?.small || null,
      scryfallUrl: body.scryfall_uri || null
    };
  }
  return { autocomplete, exact };
})();
