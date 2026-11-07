const { test, expect } = require('@playwright/test');
const fs = require('node:fs/promises');

// One isolated user journey exercises existing v1 data and user-owned backups.
// Run the same journey against the previous artifact before approving rollback.
test('saved v1 decks survive reload, editing, JSON export and CRUD', async ({ page }) => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => {
    if (!localStorage.getItem('spellbook.decks.v1')) localStorage.setItem('spellbook.decks.v1', JSON.stringify([
      { id: 'c7e0945c-f38c-412d-9063-03aa09c13941', name: 'Saved compatibility deck',
        format: 'Casual 60-card', updatedUtc: '2026-01-01T00:00:00Z',
        cards: [{ name: 'Island', quantity: 2, section: 'Main' }] }
    ]));
  });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Saved compatibility deck', exact: true })).toBeVisible();
  await page.getByLabel('Increase quantity for Island', { exact: true }).click();
  await page.getByLabel('Deck name', { exact: true }).fill('Edited saved deck');
  await page.getByLabel('Deck name', { exact: true }).press('Tab');
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Edited saved deck', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Main deck · 3 cards', exact: true })).toBeVisible();
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export JSON', exact: true }).click();
  const backup = JSON.parse(await fs.readFile(await (await download).path(), 'utf8'));
  expect(backup[0].name).toBe('Edited saved deck');
  expect(backup[0].cards[0].quantity).toBe(3);
  await page.getByLabel('New deck name', { exact: true }).fill('Disposable smoke deck');
  await page.getByRole('button', { name: 'Create deck', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Disposable smoke deck', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Delete deck', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Edited saved deck', exact: true })).toBeVisible();
  expect(errors).toEqual([]);
});
