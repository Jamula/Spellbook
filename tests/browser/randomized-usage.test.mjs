import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';

const origin = process.env.RUT_ORIGIN ?? 'http://127.0.0.1:4173';
const iterations = Number.parseInt(process.env.RUT_ITERATIONS ?? '10', 10);
const baseSeed = Number.parseInt(process.env.RUT_SEED ?? '20261108', 10) >>> 0;
const browser = await chromium.launch({ headless: true, ...(process.env.RUT_CHROMIUM_PATH ? { executablePath: process.env.RUT_CHROMIUM_PATH } : {}) });
after(async () => browser.close());

function random(seed) {
  let state = seed || 1;
  return () => {
    state ^= state << 13;
    state ^= state >>> 17;
    state ^= state << 5;
    return (state >>> 0) / 0x1_0000_0000;
  };
}

for (let index = 0; index < iterations; index += 1) {
  const seed = (baseSeed + index) >>> 0;
  test(`Randomized Usage Testing iteration ${index + 1}/${iterations} (seed ${seed})`, async () => {
    const rand = random(seed);
    const context = await browser.newContext();
    const page = await context.newPage();
    const errors = [];
    let step;
    page.on('pageerror', error => errors.push(error.message));
    await page.route('https://api.scryfall.com/**', route => {
      const url = new URL(route.request().url());
      if (url.pathname === '/cards/named') {
        const name = url.searchParams.get('exact') ?? 'Synthetic card';
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            object: 'card', name, cmc: 2, mana_cost: '{1}{U}', type_line: 'Creature',
            colors: ['U'], image_uris: { small: 'https://cards.scryfall.io/small/front/s/y/synthetic.jpg' },
            scryfall_uri: 'https://scryfall.com/card/synthetic/reference'
          })
        });
      }
      return route.abort();
    });
    try {
      step = 'open deck collection';
      const response = await page.goto(origin, { waitUntil: 'domcontentloaded' });
      assert.equal(response.status(), 200);
      await page.getByRole('heading', { name: 'My decks' }).waitFor({ timeout: 30000 });
      const deckName = `RUT ${seed}`;
      const cardName = `Synthetic card ${seed}`;
      let activeCardName = cardName;

      step = 'create deck';
      await page.getByRole('textbox', { name: 'New deck name' }).fill(deckName);
      await page.getByRole('button', { name: 'Create deck' }).click();
      await page.getByRole('heading', { name: deckName, level: 1 }).waitFor();

      step = 'open settings';
      if (rand() < 0.35) {
        await page.getByRole('button', { name: 'Menu' }).click();
        await page.getByRole('link', { name: 'Settings' }).click();
        await page.getByRole('heading', { name: 'Settings' }).waitFor();
        await page.getByRole('button', { name: 'Menu' }).click();
        await page.getByRole('link', { name: 'Decks' }).click();
        await page.getByRole('heading', { name: 'My decks' }).waitFor();
        step = 'open deck editor';
        await page.getByRole('button', { name: /^Open deck/ }).click();
        await page.getByRole('heading', { name: deckName, level: 1 }).waitFor();
      }

      step = 'add a card';
      await page.getByRole('combobox', { name: 'Card name' }).fill(cardName);
      await page.getByRole('button', { name: 'Add', exact: true }).click();
      const cardRow = page.locator('.card-row').filter({ hasText: cardName });
      await cardRow.waitFor();

      if (rand() < 0.65) {
        step = 'edit card title';
        await cardRow.getByRole('button', { name: `Edit card name for ${cardName}` }).click();
        await page.getByRole('textbox', { name: `New card name for ${cardName}` }).fill(`Renamed ${seed}`);
        await page.getByRole('button', { name: `Save card name for ${cardName}` }).click();
        await page.getByText('Card name verified and saved').waitFor();
        activeCardName = `Renamed ${seed}`;
      }
      if (rand() < 0.75) {
        step = 'change quantity';
        await page.getByRole('button', { name: `Increase quantity for ${activeCardName}` }).click();
        await page.getByRole('button', { name: `Decrease quantity for ${activeCardName}` }).click();
      }

      step = 'return to collection';
      await page.getByRole('link', { name: 'All decks' }).click();
      await page.getByRole('heading', { name: 'My decks' }).waitFor();
      const tile = page.locator('.deck-tile').filter({ has: page.getByRole('textbox', { name: `Deck name for ${deckName}` }) });
      await tile.waitFor();

      step = 'delete test deck';
      await tile.getByRole('button', { name: /^Delete deck/ }).click();
      await tile.waitFor({ state: 'detached' });
      assert.deepEqual(errors, []);
    } catch (error) {
      await mkdir('test-results/randomized-usage', { recursive: true });
      const prefix = `test-results/randomized-usage/seed-${seed}`;
      await page.screenshot({ path: `${prefix}.png`, fullPage: true }).catch(() => {});
      console.error(JSON.stringify({ seed, iteration: index + 1, step, url: page.url(), error: error.message, pageErrors: errors }, null, 2));
      throw error;
    } finally {
      await context.close();
    }
  });
}
