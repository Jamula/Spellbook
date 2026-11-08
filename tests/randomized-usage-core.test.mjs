import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  createSeededRandom,
  deriveIterationSeed,
  parseRandomizedUsageArgs,
  shuffle
} from './browser/randomized-usage.core.mjs';

test('parseRandomizedUsageArgs applies defaults and parses explicit options', () => {
  const defaults = parseRandomizedUsageArgs([], {});
  assert.equal(defaults.iterations, 3);
  assert.equal(defaults.seed, 'spellbook-randomized-default');
  assert.equal(defaults.baseUrl, 'http://127.0.0.1:4173');
  assert.equal(defaults.startIteration, 1);

  const parsed = parseRandomizedUsageArgs([
    '--iterations', '4',
    '--seed', ' issue-68 ',
    '--base-url', 'https://example.test',
    '--report', '/tmp/custom-report.json',
    '--start-iteration', '3',
    '--flows', 'deck,settings,cardIntake'
  ], {});

  assert.equal(parsed.iterations, 4);
  assert.equal(parsed.seed, 'issue-68');
  assert.equal(parsed.baseUrl, 'https://example.test');
  assert.equal(parsed.reportPath, '/tmp/custom-report.json');
  assert.equal(parsed.startIteration, 3);
  assert.deepEqual(parsed.flowNames, ['deck', 'settings', 'cardIntake']);
});

test('parseRandomizedUsageArgs rejects unsupported values', () => {
  assert.throws(() => parseRandomizedUsageArgs(['--iterations', '0'], {}), /--iterations must be between 1 and 12/);
  assert.throws(() => parseRandomizedUsageArgs(['--iterations', '13'], {}), /--iterations must be between 1 and 12/);
  assert.throws(() => parseRandomizedUsageArgs(['--seed', ''], {}), /--seed must be a non-empty string/);
  assert.throws(() => parseRandomizedUsageArgs(['--base-url', '/relative'], {}), /--base-url must be an absolute/);
  assert.throws(() => parseRandomizedUsageArgs(['--start-iteration', '0'], {}), /--start-iteration must be at least 1/);
});

test('deriveIterationSeed and shuffle are deterministic for a seed', () => {
  const seed = deriveIterationSeed('issue-68', 4);
  assert.equal(seed, 'issue-68:iteration:4');

  const list = ['deck', 'importExport', 'settings', 'cardIntake'];
  const randomizedA = shuffle(list, createSeededRandom(seed));
  const randomizedB = shuffle(list, createSeededRandom(seed));
  assert.deepEqual(randomizedA, randomizedB);
  assert.equal(new Set(randomizedA).size, list.length);
  assert.deepEqual([...randomizedA].sort(), [...list].sort());
});
