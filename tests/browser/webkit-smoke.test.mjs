import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { webkit, devices } from 'playwright';

const origin = 'http://127.0.0.1:4173';
const browser = await webkit.launch({ headless: true });
after(async () => browser.close());

test('mobile WebKit loads Spellbook with PWA metadata and local icon', async () => {
  const context = await browser.newContext({ ...devices['iPhone 13'], baseURL: origin });
  const page = await context.newPage();
  try {
    const response = await page.goto('/', { waitUntil: 'domcontentloaded' });
    assert.equal(response.status(), 200);
    await page.getByRole('heading', { name: 'My decks' }).waitFor({ timeout: 30000 });
    await page.getByRole('button', { name: 'Create deck' }).waitFor({ timeout: 30000 });
    assert.equal(await page.locator('link[rel="manifest"]').getAttribute('href'), 'manifest.webmanifest');
    assert.equal(await page.locator('meta[name="apple-mobile-web-app-title"]').getAttribute('content'), 'Spellbook');
    const manifestResponse = await page.request.get('/manifest.webmanifest');
    assert.equal(manifestResponse.status(), 200);
    const manifest = await manifestResponse.json();
    assert.equal(manifest.display, 'standalone');
    assert.equal(manifest.start_url, '/');
    assert.equal(manifest.scope, '/');
    assert.ok(manifest.icons.some(icon => icon.src === '/icons/spellbook.svg'));
    assert.equal((await page.request.get('/icons/spellbook.svg')).status(), 200);
  } finally {
    await context.close();
  }
});

test('iPhone WebKit creates a deck, edits a card and persists after reload', async () => {
  const context = await browser.newContext({ ...devices['iPhone 13'], baseURL: origin });
  const page = await context.newPage();
  try {
    await page.goto('/');
    await page.getByRole('button', { name: 'Create deck' }).waitFor({ timeout: 30000 });
    await page.getByRole('textbox', { name: 'New deck name' }).fill('WebKit smoke deck');
    await page.getByRole('button', { name: 'Create deck' }).click();
    await page.getByRole('heading', { name: 'WebKit smoke deck' }).waitFor();
    await page.getByRole('textbox', { name: 'Card name' }).fill('Avacyn, Angel of Hope');
    await page.getByRole('button', { name: 'Add', exact: true }).click();
    await page.getByText('Avacyn, Angel of Hope', { exact: true }).first().waitFor();
    await page.reload();
    await page.getByRole('heading', { name: 'WebKit smoke deck' }).waitFor({ timeout: 30000 });
    await page.getByText('Avacyn, Angel of Hope', { exact: true }).first().waitFor();
    assert.match(await page.locator('.app-version').innerText(), /^Version 0\.1\.\d+/);
  } finally {
    await context.close();
  }
});
