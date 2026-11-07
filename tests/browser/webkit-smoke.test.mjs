import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { webkit, devices } from 'playwright';

const origin = 'http://127.0.0.1:4173';
const browser = await webkit.launch({ headless: true });
after(async () => browser.close());

async function openAppReady(page) {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  const create = page.getByRole('button', { name: 'Create deck' });
  try {
    await create.waitFor({ timeout: 12000 });
  } catch {
    // WebKit WASM bootstrap can lag on a cold, loaded CI runner. One reload
    // verifies the app recovers rather than accepting a permanently blank page.
    console.warn('WebKit bootstrap was delayed; retrying once after reload');
    await page.reload({ waitUntil: 'domcontentloaded' });
    await create.waitFor({ timeout: 20000 });
  }
}


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
    assert.ok(manifest.icons.some(icon => icon.src === '/icons/spellbook-192.png'));
    assert.ok(manifest.icons.some(icon => icon.src === '/icons/spellbook-512.png'));
    assert.equal((await page.request.get('/icons/spellbook.svg')).status(), 200);
    assert.equal((await page.request.get('/icons/spellbook-192.png')).status(), 200);
    assert.equal((await page.request.get('/icons/spellbook-512.png')).status(), 200);
    // A successful HTTP response does not prove an icon is a decodable image.
    for (const size of [192, 512]) {
      const dimensions = await page.evaluate(async size => {
        const img = new Image();
        img.src = `/icons/spellbook-${size}.png`;
        await img.decode();
        return [img.naturalWidth, img.naturalHeight];
      }, size);
      assert.deepEqual(dimensions, [size, size]);
    }
    assert.equal(await page.locator('link[rel="apple-touch-icon"]').getAttribute('href'), 'icons/spellbook-192.png');
  } finally {
    await context.close();
  }
});

test('iPhone WebKit creates a deck, edits a card and persists after reload', async () => {
  const context = await browser.newContext({ ...devices['iPhone 13'], baseURL: origin });
  const page = await context.newPage();
  try {
    await openAppReady(page);
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

test('iPhone WebKit opens in-app feedback modal without navigation or private data', async () => {
  const context = await browser.newContext({ ...devices['iPhone 13'], baseURL: origin });
  const page = await context.newPage();
  try {
    await openAppReady(page);
    await page.getByRole('button', { name: 'Send feedback' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.waitFor();
    assert.equal(await dialog.evaluate(element => element.open), true);
    await dialog.getByRole('textbox', { name: 'Title' }).fill('Scanner feedback');
    await dialog.getByRole('textbox', { name: 'Description and reproduction steps' })
      .fill('A mana symbol was included with the recognized card name.');
    await page.getByText('Feedback submission is not configured yet').waitFor();
    assert.equal(await dialog.getByRole('link', { name: /Use GitHub directly/ }).count(), 1);
    assert.equal(new URL(page.url()).pathname, '/');
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    assert.equal(await dialog.evaluate(element => element.open), false);
  } finally { await context.close(); }
});

test('iPhone WebKit keeps selected photo local and requires editable card-name confirmation', async () => {
  const context = await browser.newContext({ ...devices['iPhone 13'], baseURL: origin });
  const page = await context.newPage();
  try {
    await openAppReady(page);
    await page.getByRole('button', { name: 'Create deck' }).waitFor({ timeout: 30000 });
    await page.getByRole('textbox', { name: 'New deck name' }).fill('Photo smoke deck');
    await page.getByRole('button', { name: 'Create deck' }).click();
    await page.getByRole('region', { name: 'Scan a card' }).waitFor();
    const { readFileSync } = await import('node:fs');
    await page.locator('#card-media-picker').setInputFiles({
      name: 'local-card.png',
      mimeType: 'image/png',
      buffer: readFileSync('Spellbook/wwwroot/icons/spellbook-192.png')
    });
    await page.locator('.scan-preview').waitFor();
    assert.match(await page.locator('.scan-preview').getAttribute('src'), /^data:image\/png;base64,/);
    assert.equal(await page.locator('.card-row').count(), 0);
    const candidate = page.getByRole('textbox', { name: 'Name from photo' });
    await candidate.fill('Avacyn, Angel of Hope');
    await candidate.press('Tab');
    await page.getByRole('button', { name: 'Add confirmed card' }).click();
    await page.locator('.card-row a[href^="https://scryfall.com/search"]').waitFor();
    assert.equal(await page.locator('.card-row').count(), 1);
  } finally { await context.close(); }
});
