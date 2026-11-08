(() => {
  const key = 'spellbook.theme.v1';
  const palettes = {
    white: { accent: '#595142', hover: '#403a30', soft: '#f3efe5', border: '#938977', focus: '#4b4438' },
    blue: { accent: '#245b9d', hover: '#194675', soft: '#e7f0fb', border: '#668bb4', focus: '#164f8b' },
    black: { accent: '#333333', hover: '#1f1f1f', soft: '#ececec', border: '#777777', focus: '#242424' },
    red: { accent: '#a33131', hover: '#7e2222', soft: '#f8e8e7', border: '#b36b68', focus: '#8f2929' },
    green: { accent: '#2f6940', hover: '#214d2f', soft: '#e7f2e9', border: '#6c9575', focus: '#245a32' }
  };
  const valid = pair => Array.isArray(pair) && pair.length === 2 &&
    pair.every(color => Object.hasOwn(palettes, color)) && pair[0] !== pair[1];

  function apply(pair) {
    if (!valid(pair)) {
      document.documentElement.removeAttribute('data-theme');
      for (const property of ['--sage', '--sage-hover', '--theme-secondary', '--theme-secondary-soft', '--border', '--focus', '--theme-card']) {
        document.documentElement.style.removeProperty(property);
      }
      return false;
    }

    const [primary, secondary] = pair.map(color => palettes[color]);
    const root = document.documentElement;
    root.dataset.theme = pair.join('-');
    root.style.setProperty('--sage', primary.accent);
    root.style.setProperty('--sage-hover', primary.hover);
    root.style.setProperty('--theme-secondary', secondary.accent);
    root.style.setProperty('--theme-secondary-soft', secondary.soft);
    root.style.setProperty('--border', primary.border);
    root.style.setProperty('--focus', primary.focus);
    root.style.setProperty('--theme-card', `linear-gradient(135deg, ${primary.soft}, #fff 55%, ${secondary.soft})`);
    return true;
  }

  window.spellbookTheme = {
    load() {
      try {
        const saved = JSON.parse(localStorage.getItem(key));
        if (valid(saved)) {
          apply(saved);
          return saved;
        }
      } catch { /* Treat blocked or malformed browser storage as the default theme. */ }
      apply(null);
      return null;
    },
    save(pair) {
      if (!valid(pair)) return false;
      try {
        localStorage.setItem(key, JSON.stringify(pair));
      } catch { return false; }
      return apply(pair);
    },
    reset() {
      try { localStorage.removeItem(key); } catch { /* The default theme still applies. */ }
      apply(null);
      return true;
    }
  };
})();

window.spellbookTheme.load();
