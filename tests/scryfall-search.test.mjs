import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const source = await readFile(new URL('../Spellbook/wwwroot/js/scryfall.js', import.meta.url), 'utf8');

function client(fetchImpl, documentImpl = { getElementById: () => null }) {
  const window = {};
  vm.runInNewContext(source, { window, fetch: fetchImpl, document: documentImpl });
  return window.spellbookScryfall;
}

test('name search finds suffix matches and returns a bounded unique list', async () => {
  const requests = [];
  const cards = [
    { name: 'Angel of Destiny', mana_cost: '{3}{W}{W}', cmc: 5, colors: ['W'], image_uris: { small: 'https://cards.scryfall.io/small/front/a/b/example.jpg' } },
    { name: 'Baneslayer Angel', mana_cost: '{3}{W}{W}', cmc: 5, colors: ['W'] },
    { name: 'Baneslayer Angel', mana_cost: '{3}{W}{W}', cmc: 5, colors: ['W'] },
    ...Array.from({ length: 12 }, (_, i) => ({ name: `Example ${i} Angel` }))
  ];
  const api = client(async url => {
    requests.push(url);
    return { ok: true, json: async () => ({ data: cards }) };
  });

  const results = await api.autocomplete(' Angel ');

  assert.deepEqual([...results].map(card => card.name), cards.map(card => card.name).filter((name, i, all) => all.indexOf(name) === i).slice(0, 10));
  assert.ok(results.some(card => card.name === 'Baneslayer Angel'));
  assert.equal(results[0].manaCost, '{3}{W}{W}');
  assert.equal(results[0].manaValue, 5);
  assert.deepEqual([...results[0].colors], ['W']);
  assert.equal(results[0].imageUrl, 'https://cards.scryfall.io/small/front/a/b/example.jpg');
  assert.equal(requests.length, 1);
  const url = new URL(requests[0]);
  assert.equal(url.pathname, '/cards/search');
  assert.equal(url.searchParams.get('q'), 'name:"Angel"');
  assert.equal(url.searchParams.get('unique'), 'cards');
  assert.equal(url.searchParams.get('order'), 'name');
});

test('search term is treated as a quoted name phrase and repeated queries use cache', async () => {
  const requests = [];
  const api = client(async url => {
    requests.push(url);
    return { ok: true, json: async () => ({ data: [{ name: 'Angel’s Grace' }] }) };
  });

  const first = await api.autocomplete('Angel "of"');
  const second = await api.autocomplete(' angel "OF" ');

  assert.deepEqual([...first].map(card => card.name), ['Angel’s Grace']);
  assert.deepEqual([...second], [...first]);
  assert.equal(requests.length, 1);
  assert.equal(new URL(requests[0]).searchParams.get('q'), 'name:"Angel \\"of\\""');
});

test('short and overlong queries do not call Scryfall', async () => {
  let count = 0;
  const api = client(async () => {
    count++;
    return { ok: true, json: async () => ({ data: [] }) };
  });

  assert.deepEqual([...(await api.autocomplete('A'))], []);
  assert.deepEqual([...(await api.autocomplete('x'.repeat(161)))], []);
  assert.equal(count, 0);
});

test('keyboard-active suggestion scrolls within its nearest container', () => {
  let scrolled = null;
  let lookedUpId = null;
  const api = client(async () => ({ ok: true, json: async () => ({ data: [] }) }), {
    getElementById: id => {
      lookedUpId = id;
      return { scrollIntoView: options => { scrolled = options; } };
    }
  });

  api.scrollSuggestionIntoView(4);

  assert.equal(lookedUpId, 'card-suggestion-4');
  assert.equal(scrolled.block, 'nearest');
  assert.equal(scrolled.inline, 'nearest');
});

test('suggestion panel has a viewport-bounded vertical scroll area', async () => {
  const css = await readFile(new URL('../Spellbook/wwwroot/css/app.css', import.meta.url), 'utf8');
  assert.match(css, /\.card-suggestions\{[^}]*max-height:min\(280px,40vh\)[^}]*overflow-y:auto/);
});
