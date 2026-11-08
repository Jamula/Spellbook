const MAX_DEFAULT_ITERATIONS = 12;

function integerOrNull(value) {
  if (value === undefined || value === null || value === '') {
    return null;
  }
  const parsed = Number.parseInt(String(value), 10);
  return Number.isInteger(parsed) ? parsed : null;
}

function hashSeed(seedText) {
  let h1 = 0x811c9dc5;
  for (let i = 0; i < seedText.length; i++) {
    h1 ^= seedText.charCodeAt(i);
    h1 = Math.imul(h1, 0x01000193);
  }
  return h1 >>> 0;
}

export function createSeededRandom(seed) {
  let state = hashSeed(String(seed || 'spellbook'));
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t ^= t + Math.imul(t ^ (t >>> 7), 61 | t);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function shuffle(items, random) {
  const copy = [...items];
  for (let i = copy.length - 1; i > 0; i--) {
    const j = Math.floor(random() * (i + 1));
    [copy[i], copy[j]] = [copy[j], copy[i]];
  }
  return copy;
}

export function deriveIterationSeed(baseSeed, iterationNumber) {
  return `${baseSeed}:iteration:${iterationNumber}`;
}

export function parseRandomizedUsageArgs(argv = [], env = process.env) {
  const options = {
    iterations: integerOrNull(env.SPELLBOOK_RANDOMIZED_ITERATIONS) ?? 3,
    seed: env.SPELLBOOK_RANDOMIZED_SEED ?? 'spellbook-randomized-default',
    baseUrl: env.SPELLBOOK_RANDOMIZED_BASE_URL ?? 'http://127.0.0.1:4173',
    reportPath: env.SPELLBOOK_RANDOMIZED_REPORT ?? '/tmp/spellbook-randomized-usage-report.json',
    startIteration: integerOrNull(env.SPELLBOOK_RANDOMIZED_START_ITERATION) ?? 1,
    flowNames: null
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    const next = argv[i + 1];
    if (arg === '--iterations') {
      options.iterations = integerOrNull(next);
      i++;
      continue;
    }
    if (arg === '--seed') {
      options.seed = next;
      i++;
      continue;
    }
    if (arg === '--base-url') {
      options.baseUrl = next;
      i++;
      continue;
    }
    if (arg === '--report') {
      options.reportPath = next;
      i++;
      continue;
    }
    if (arg === '--start-iteration') {
      options.startIteration = integerOrNull(next);
      i++;
      continue;
    }
    if (arg === '--flows') {
      options.flowNames = next?.split(',').map(value => value.trim()).filter(Boolean) ?? [];
      i++;
      continue;
    }
    throw new Error(`Unknown argument: ${arg}`);
  }

  if (!Number.isInteger(options.iterations) || options.iterations < 1 || options.iterations > MAX_DEFAULT_ITERATIONS) {
    throw new Error(`--iterations must be between 1 and ${MAX_DEFAULT_ITERATIONS}`);
  }
  if (!Number.isInteger(options.startIteration) || options.startIteration < 1) {
    throw new Error('--start-iteration must be at least 1');
  }
  if (typeof options.seed !== 'string' || options.seed.trim().length === 0) {
    throw new Error('--seed must be a non-empty string');
  }
  if (typeof options.baseUrl !== 'string' || !/^https?:\/\//.test(options.baseUrl)) {
    throw new Error('--base-url must be an absolute http(s) URL');
  }

  options.seed = options.seed.trim();
  return options;
}
