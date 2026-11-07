import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = readFileSync(new URL('../functions/api/feedback.js', import.meta.url), 'utf8');
const { onRequestGet, onRequestPost } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const env = {
  TURNSTILE_SITE_KEY: 'public-site-key',
  TURNSTILE_SECRET: 'secret-is-server-only',
  GITHUB_FEEDBACK_TOKEN: 'server-only-github-token'
};
const origin = 'https://spellbook-ag4.pages.dev';
const payload = {
  kind: 'Bug', title: 'Test scanner feedback',
  details: 'The extracted card title includes a mana symbol.',
  token: 'valid-single-use-challenge-token'
};
const req = (value, from = origin) => new Request(origin + '/api/feedback', {
  method: 'POST',
  headers: { Origin: from, 'Content-Type': 'application/json' },
  body: JSON.stringify(value)
});
const run = async (value, fetcher, from) => {
  const original = globalThis.fetch;
  globalThis.fetch = fetcher;
  try { return await onRequestPost({ request: req(value, from), env }); }
  finally { globalThis.fetch = original; }
};

test('public config never returns a private token or secret', async () => {
  const response = await onRequestGet({ env });
  const text = await response.text();
  assert.equal(response.status, 200);
  assert.deepEqual(JSON.parse(text), { enabled: true, siteKey: env.TURNSTILE_SITE_KEY });
  assert.ok(!text.includes(env.GITHUB_FEEDBACK_TOKEN));
  assert.ok(!text.includes(env.TURNSTILE_SECRET));
  assert.deepEqual(await (await onRequestGet({ env: {} })).json(), { enabled: false, siteKey: null });
});

test('invalid and cross-origin feedback cannot contact GitHub', async () => {
  let requests = 0;
  const fetcher = async () => { requests++; throw Error('Unexpected external request'); };
  assert.equal((await run(payload, fetcher, 'https://evil.example')).status, 403);
  for (const bad of [
    { ...payload, title: 'no' },
    { ...payload, kind: 'Admin' },
    { ...payload, details: 'short' },
    { ...payload, title: 12345 },
    { ...payload, token: '' }
  ]) assert.equal((await run(bad, fetcher)).status, 400);
  assert.equal(requests, 0);
});

test('single-use Turnstile verified feedback creates only an authorized repository issue', async () => {
  const requests = [];
  const fetcher = async (url, options) => {
    requests.push({ url, options });
    if (String(url).includes('/siteverify'))
      return Response.json({ success: true, action: 'spellbook_feedback', hostname: 'spellbook-ag4.pages.dev' });
    assert.equal(String(url), 'https://api.github.com/repos/Jamula/Spellbook/issues');
    assert.equal(options.headers.authorization, 'Bearer ' + env.GITHUB_FEEDBACK_TOKEN);
    const submitted = JSON.parse(options.body);
    assert.equal(submitted.title, '[User feedback] [Bug] Test scanner feedback');
    assert.ok(submitted.body.includes(payload.details));
    assert.ok(!submitted.body.includes(payload.token));
    return Response.json({ html_url: 'https://github.com/Jamula/Spellbook/issues/99' }, { status: 201 });
  };
  const response = await run(payload, fetcher);
  assert.equal(response.status, 201);
  assert.deepEqual(await response.json(), { issueUrl: 'https://github.com/Jamula/Spellbook/issues/99' });
  assert.equal(requests.length, 2);
});

test('failed bot validation or GitHub API leaves no false success', async () => {
  const failedChallenge = await run(payload, async () => Response.json({
    success: false, action: 'spellbook_feedback', hostname: 'spellbook-ag4.pages.dev'
  }));
  assert.equal(failedChallenge.status, 403);
  const failedGitHub = await run(payload, async url =>
    String(url).includes('/siteverify')
      ? Response.json({ success: true, action: 'spellbook_feedback', hostname: 'spellbook-ag4.pages.dev' })
      : Response.json({ message: 'No permission' }, { status: 403 }));
  assert.equal(failedGitHub.status, 502);
});

test('valid UTF-8 feedback is accepted within character limits', async () => {
  const unicode = { ...payload, details: '漢'.repeat(2200) };
  let body;
  const response = await run(unicode, async (url, options) => {
    if (String(url).includes('/siteverify'))
      return Response.json({ success: true, action: 'spellbook_feedback', hostname: 'spellbook-ag4.pages.dev' });
    body = JSON.parse(options.body);
    return Response.json({ html_url: 'https://github.com/Jamula/Spellbook/issues/100' }, { status: 201 });
  });
  assert.equal(response.status, 201);
  assert.ok(body.body.includes(unicode.details));
});
