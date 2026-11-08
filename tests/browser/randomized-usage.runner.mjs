import assert from 'node:assert/strict';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { devices, webkit } from 'playwright';
import {
  createSeededRandom,
  deriveIterationSeed,
  parseRandomizedUsageArgs,
  shuffle
} from './randomized-usage.core.mjs';

const iconPath = new URL('../../Spellbook/wwwroot/icons/spellbook-192.png', import.meta.url);

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

async function openAppReady(page) {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  const create = page.getByRole('button', { name: 'Create deck' });
  try {
    await create.waitFor({ timeout: 12000 });
  } catch {
    await page.reload({ waitUntil: 'domcontentloaded' });
    await create.waitFor({ timeout: 20000 });
  }
}

async function enforceLocalOnlyNetwork(page, baseUrl) {
  const allowedOrigin = new URL(baseUrl).origin;
  await page.route('**/*', route => {
    const requestUrl = route.request().url();
    if (requestUrl.startsWith('data:') || requestUrl.startsWith('blob:') || requestUrl.startsWith('about:')) {
      route.continue();
      return;
    }

    try {
      const parsed = new URL(requestUrl);
      if (parsed.origin === allowedOrigin) {
        route.continue();
        return;
      }
    } catch {
      route.abort('blockedbyclient');
      return;
    }

    route.abort('blockedbyclient');
  });
}

async function runDeckFlow(page, token, setStep) {
  setStep('create deck');
  await page.getByRole('textbox', { name: 'New deck name' }).fill(`Random deck ${token}`);
  await page.getByRole('button', { name: 'Create deck' }).click();
  await page.getByRole('heading', { name: `Random deck ${token}` }).waitFor({ timeout: 30000 });

  setStep('edit deck metadata');
  await page.getByLabel('Deck name').fill(`Random deck ${token} edited`);
  await page.getByLabel('Game format').selectOption('Commander');
  await page.getByRole('heading', { name: `Random deck ${token} edited` }).waitFor();

  const cardName = `Synthetic card ${token}`;
  setStep('add and edit card quantity');
  await page.getByRole('textbox', { name: 'Card name' }).fill(cardName);
  await page.getByRole('button', { name: 'Add', exact: true }).click();
  await page.getByText(cardName, { exact: true }).first().waitFor();
  await page.getByRole('button', { name: new RegExp(`Increase quantity for ${escapeRegExp(cardName)}`) }).click();
  await page.getByRole('button', { name: new RegExp(`Decrease quantity for ${escapeRegExp(cardName)}`) }).click();
}

async function runImportExportFlow(page, token, setStep) {
  setStep('export collection JSON');
  const jsonExport = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export JSON' }).click();
  const downloadedJson = await jsonExport;
  assert.equal(downloadedJson.suggestedFilename(), 'spellbook-decks.json');

  setStep('import collection JSON');
  const importedDeckName = `Imported deck ${token}`;
  const importPayload = JSON.stringify([
    {
      name: importedDeckName,
      format: 'Modern',
      cards: [{ name: `Imported card ${token}`, quantity: 1, section: 'Main' }]
    }
  ]);
  await page.locator('label.upload:has-text("Import JSON") input[type=file]').setInputFiles({
    name: `import-${token}.json`,
    mimeType: 'application/json',
    buffer: Buffer.from(importPayload, 'utf8')
  });
  await page.getByRole('heading', { name: importedDeckName }).waitFor({ timeout: 15000 });

  setStep('import/export deck list');
  await page
    .locator('details')
    .filter({ hasText: 'Import/export a Moxfield or Archidekt deck list' })
    .locator('summary')
    .click();
  await page.getByRole('textbox', { name: 'Imported deck name' }).fill(`Decklist ${token}`);
  await page.getByRole('textbox', { name: 'Deck list' }).fill('1 Sol Ring\n1 Arcane Signet');
  await page.getByRole('button', { name: 'Review deck list' }).click();
  await page.getByRole('heading', { name: new RegExp(`Review: Decklist ${escapeRegExp(token)}`) }).waitFor();
  await page.getByRole('checkbox', { name: /I reviewed the card names, quantities, and zones/ }).check();
  await page.getByRole('button', { name: 'Import reviewed deck' }).click();
  await page.getByRole('heading', { name: `Decklist ${token}` }).waitFor({ timeout: 15000 });

  const txtExport = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export deck TXT' }).click();
  assert.equal((await txtExport).suggestedFilename(), 'spellbook-deck.txt');

  const csvExport = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export deck CSV' }).click();
  assert.equal((await csvExport).suggestedFilename(), 'spellbook-deck.csv');
}

async function runSettingsFlow(page, setStep) {
  setStep('open privacy controls');
  await page.locator('details.privacy-controls > summary').click();
  setStep('save reject analytics preference');
  await page.getByRole('button', { name: 'Reject optional analytics / revoke consent' }).click();
  await page.getByText('Optional analytics are off.', { exact: false }).waitFor();
}

async function runCardIntakeFlow(page, token, setStep) {
  setStep('attach local synthetic card image');
  const image = await readFile(iconPath);
  await page.locator('#card-media-picker').setInputFiles({
    name: `local-card-${token}.png`,
    mimeType: 'image/png',
    buffer: image
  });
  await page.locator('.scan-preview').waitFor({ timeout: 10000 });

  setStep('confirm recognized card name locally');
  const scannedName = `Scanned card ${token}`;
  await page.getByRole('textbox', { name: 'Name from photo' }).fill(scannedName);
  await page.getByRole('button', { name: 'Add confirmed card' }).click();
  await page.getByText(scannedName, { exact: true }).first().waitFor({ timeout: 10000 });
}

const flowRegistry = [
  { id: 'deck', run: runDeckFlow },
  { id: 'importExport', run: runImportExportFlow },
  { id: 'settings', run: runSettingsFlow },
  { id: 'cardIntake', run: runCardIntakeFlow }
];

function selectFlows(flowNames) {
  if (!flowNames) {
    return flowRegistry;
  }

  const unique = [...new Set(flowNames)];
  const selected = unique.map(name => flowRegistry.find(flow => flow.id === name));
  const missing = unique.filter((_, index) => !selected[index]);
  if (missing.length > 0) {
    throw new Error(`Unknown --flows values: ${missing.join(', ')}`);
  }
  return selected;
}

async function run() {
  const options = parseRandomizedUsageArgs(process.argv.slice(2));
  const selectedFlows = selectFlows(options.flowNames);
  const browser = await webkit.launch({ headless: true });
  const report = {
    startedAt: new Date().toISOString(),
    baseUrl: options.baseUrl,
    iterationCount: options.iterations,
    startIteration: options.startIteration,
    baseSeed: options.seed,
    flows: selectedFlows.map(flow => flow.id),
    iterations: []
  };

  let firstFailure = null;
  try {
    for (let offset = 0; offset < options.iterations; offset++) {
      const iteration = options.startIteration + offset;
      const iterationSeed = deriveIterationSeed(options.seed, iteration);
      const random = createSeededRandom(iterationSeed);
      const order = shuffle(selectedFlows, random);
      const token = `${iteration}-${Math.floor(random() * 1_000_000).toString().padStart(6, '0')}`;
      const entry = {
        iteration,
        seed: iterationSeed,
        flowOrder: order.map(flow => flow.id),
        failingStep: null,
        status: 'passed',
        error: null
      };

      const context = await browser.newContext({ ...devices['iPhone 13'], baseURL: options.baseUrl });
      const page = await context.newPage();
      let currentStep = 'bootstrap';
      const setStep = step => {
        currentStep = step;
      };

      try {
        await enforceLocalOnlyNetwork(page, options.baseUrl);
        await openAppReady(page);
        for (const flow of order) {
          setStep(`flow:${flow.id}`);
          await flow.run(page, token, setStep);
        }
      } catch (error) {
        entry.status = 'failed';
        entry.failingStep = currentStep;
        entry.error = error?.stack || String(error);
        firstFailure ??= {
          iteration,
          seed: iterationSeed,
          failingStep: currentStep,
          error: entry.error
        };
      } finally {
        await context.close();
      }

      report.iterations.push(entry);
      console.log(JSON.stringify({ event: 'randomized-usage-iteration', ...entry }));

      if (entry.status === 'failed') {
        break;
      }
    }
  } finally {
    await browser.close();
  }

  report.completedAt = new Date().toISOString();
  await mkdir(path.dirname(options.reportPath), { recursive: true });
  await writeFile(options.reportPath, JSON.stringify(report, null, 2), 'utf8');
  console.log(JSON.stringify({ event: 'randomized-usage-report', path: options.reportPath }));

  if (firstFailure) {
    throw new Error(
      `Randomized usage failed at iteration ${firstFailure.iteration} with seed ${firstFailure.seed} on step ${firstFailure.failingStep}`
    );
  }
}

await run();
