// Run with node --test scripts/privacy.test.cjs; isolated VM per scenario, no packages.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../Spellbook/wwwroot/js/privacy.js'), 'utf8');
function browser(value = null, blocked = false) {
  const localStorage = { getItem() { if (blocked) throw Error('blocked'); return value; }, setItem(key, next) { if (blocked) throw Error('blocked'); value = next; } };
  // Every possible network/DOM entry used by common tracking loaders is absent:
  // touching one throws instead of silently allowing a request.
  const context = vm.createContext({ window: {}, localStorage });
  vm.runInContext(source, context);
  return context.window.spellbookPrivacy;
}
test('no transport before consent, after opt-in, refusal, reload or revocation; strict payload boundary', () => {
  const api = browser();
  assert.equal(JSON.stringify(api.read()), '{"usage":false,"recordings":false}');
  const events = ['page_view', 'deck_created', 'card_added', 'backup_exported', 'feedback_opened'];
  for (const consent of [[false, false], [true, false], [true, true], [false, true]]) {
    assert.equal(api.setConsent(...consent), true);
    assert.equal(JSON.stringify(api.read()), JSON.stringify({ usage: consent[0], recordings: consent[1] }));
    for (const name of events) {
      assert.equal(api.track(name), false);
      assert.equal(api.track(name, { deck: 'private', id: 'secret' }), false);
    }
    for (const name of ['private deck', 'click', undefined, {}, 'page_view?secret']) assert.equal(api.track(name), false);
  }
  assert.equal(api.revoke(), true);
  assert.equal(JSON.stringify(api.read()), '{"usage":false,"recordings":false}');
  assert.equal(api.setConsent('true', 1), true);
  assert.equal(JSON.stringify(api.read()), '{"usage":false,"recordings":false}');
  assert.equal(browser('{"usage":true,"recordings":true}').track('page_view'), false);
});
test('corrupt/blocked storage and blocker/offline environment fail closed', () => {
  for (const api of [browser('{broken'), browser(null, true), browser('null'), browser('false')]) {
    assert.equal(JSON.stringify(api.read()), '{"usage":false,"recordings":false}');
    assert.equal(api.track('page_view'), false);
    assert.doesNotThrow(() => api.revoke());
  }
  const api = browser(null, true);
  assert.equal(api.setConsent(true, true), false);
  assert.equal(api.revoke(), false);
  assert.equal(api.track('page_view'), false);
});
