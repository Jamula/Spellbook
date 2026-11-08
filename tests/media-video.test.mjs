import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';
import vm from 'node:vm';

const mediaScript = await readFile(new URL('../Spellbook/wwwroot/js/media.js', import.meta.url), 'utf8');

function createVideo() {
  const listeners = new Map();
  return {
    duration: Number.NaN,
    readyState: 0,
    currentTime: 0,
    seeking: false,
    videoWidth: 640,
    videoHeight: 480,
    error: null,
    pause() {},
    addEventListener(name, listener) {
      const group = listeners.get(name) ?? new Set();
      group.add(listener);
      listeners.set(name, group);
    },
    removeEventListener(name, listener) {
      listeners.get(name)?.delete(listener);
    },
    listenerCount(name) {
      return listeners.get(name)?.size ?? 0;
    },
    dispatch(name) {
      for (const listener of [...(listeners.get(name) ?? [])]) listener();
    }
  };
}

function createMedia(video) {
  const canvas = {
    width: 0,
    height: 0,
    getContext: () => ({ drawImage() {}, getImageData: () => ({ data: new Uint8ClampedArray(0) }) }),
    toDataURL: () => 'data:image/jpeg;base64,AA=='
  };
  const window = {
    addEventListener() {},
    Tesseract: { createWorker: async () => ({ recognize: async () => ({ data: { text: '' } }) }) }
  };
  const sandbox = {
    window,
    document: {
      getElementById: id => id === 'video-preview' ? video : null,
      createElement: name => name === 'canvas' ? canvas : null
    },
    URL,
    AbortController,
    setTimeout,
    clearTimeout,
    console
  };
  vm.runInNewContext(mediaScript, sandbox, { filename: 'media.js' });
  return window.spellbookMedia;
}

test('video scanning waits for metadata and a decoded first frame', async () => {
  const video = createVideo();
  const media = createMedia(video);
  let complete = false;
  const scan = media.scanVideo('video-preview').then(result => {
    complete = true;
    return result;
  });

  await Promise.resolve();
  assert.equal(complete, false, 'scan waits instead of rejecting a video whose metadata is still loading');

  video.duration = 0.05;
  video.readyState = 1;
  video.dispatch('loadedmetadata');
  await Promise.resolve();
  assert.equal(complete, false, 'scan also waits for actual frame data before drawing a canvas');

  video.readyState = 2;
  video.dispatch('loadeddata');
  const result = await scan;
  assert.equal(result.frames.length, 1);
  assert.equal(result.frames[0].time, 0);
  assert.equal(result.cancelled, false);
});

test('cancelling while metadata is loading stops the scan without capturing a frame', async () => {
  const video = createVideo();
  const media = createMedia(video);
  const scan = media.scanVideo('video-preview');

  await Promise.resolve();
  media.cancelVideoScan();
  const result = await scan;

  assert.equal(result.frames.length, 0);
  assert.equal(result.cancelled, true);
  assert.equal(result.sampledFrames, 0);
  assert.equal(video.listenerCount('loadedmetadata'), 0);
  assert.equal(video.listenerCount('durationchange'), 0);
  assert.equal(video.listenerCount('abort'), 0);
});
