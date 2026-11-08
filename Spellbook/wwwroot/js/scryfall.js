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
    // Scryfall's autocomplete endpoint favors name prefixes. A name search
    // finds the same term anywhere in the card name, e.g. "Angel" also finds
    // "Baneslayer Angel". Quote the term so user input stays a name phrase,
    // not additional Scryfall search syntax.
    const phrase = `name:"${q.replace(/[\\"]/g, "\\$&")}"`;
    const body = await cached("n:" + q.toLowerCase(),
      "/cards/search?q=" + encodeURIComponent(phrase) + "&unique=cards&order=name");
    if (!Array.isArray(body?.data)) return [];
    const results = [];
    const seen = new Set();
    for (const card of body.data) {
      if (typeof card?.name !== "string" || seen.has(card.name)) continue;
      seen.add(card.name);
      results.push({
        name: card.name,
        manaValue: Number(card.cmc) || 0,
        manaCost: card.mana_cost || card.card_faces?.map(face => face.mana_cost).filter(Boolean).join(" // ") || "",
        typeLine: card.type_line || card.card_faces?.map(face => face.type_line).filter(Boolean).join(" // ") || "",
        colors: Array.isArray(card.colors) ? card.colors : [],
        imageUrl: card.image_uris?.small || card.card_faces?.[0]?.image_uris?.small || null,
        scryfallUrl: card.scryfall_uri || null
      });
      if (results.length === 10) break;
    }
    return results;
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
      manaCost: body.mana_cost || body.card_faces?.map(face => face.mana_cost).filter(Boolean).join(" // ") || "",
      typeLine: body.type_line || "",
      colors: Array.isArray(body.colors) ? body.colors : [],
      imageUrl: body.image_uris?.small || body.card_faces?.[0]?.image_uris?.small || null,
      scryfallUrl: body.scryfall_uri || null
    };
  }
  function scrollSuggestionIntoView(index) {
    const option = document.getElementById("card-suggestion-" + Number(index));
    option?.scrollIntoView({ block: "nearest", inline: "nearest" });
  }
  return { autocomplete, exact, scrollSuggestionIntoView };
})();
