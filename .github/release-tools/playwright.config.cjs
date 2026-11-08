const { defineConfig } = require('@playwright/test');
module.exports = defineConfig({
  testDir: '.', testMatch: 'smoke.spec.cjs', fullyParallel: true,
  timeout: 60000, retries: 0, reporter: 'list',
  use: { baseURL: 'http://127.0.0.1:4173', browserName: 'chromium',
    launchOptions: process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {} },
  webServer: { command: 'python3 -m http.server 4173 --bind 127.0.0.1 --directory ../release-site',
    url: 'http://127.0.0.1:4173', reuseExistingServer: false }
});
