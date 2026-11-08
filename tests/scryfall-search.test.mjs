import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const source = await readFile(new URL('../Spellbook/wwwroot/js/scryfall.js', import.meta.url), 'utf8');

function client(fetchImpl) {
  const window = {};
  vm.runInNewContext(source, { window, fetch: fetchImpl });
  return window.spellbookScryfall;
}

test('name search finds suffix matches and returns a bounded unique list', async () => {
  const requests = [];
  const cards = [
    { name: 'Angel of Destiny' },
    { name: 'Baneslayer Angel' },
    { name: 'Baneslayer Angel' },
    ...Array.from({ length: 12 }, (_, i) => ({ name: `Example ${i} Angel` }))
  ];
  const api = client(async url => {
    requests.push(url);
    return { ok: true, json: async () => ({ data: cards }) };
  });

  const names = await api.autocomplete(' Angel ');

  assert.deepEqual([...names], cards.map(card => card.name).filter((name, i, all) => all.indexOf(name) === i).slice(0, 10));
  assert.ok(names.includes('Baneslayer Angel'));
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

  assert.deepEqual([...first], ['Angel’s Grace']);
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
