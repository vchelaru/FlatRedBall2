import { defineConfig, devices } from '@playwright/test';
import path from 'node:path';

const outDir = path.join(__dirname, '..', '_out', 'browser-ui-drive');

/**
 * External Playwright runner for AnimationEditor.Browser (#690 / Decision C2).
 * Set AE_BROWSER_URL to the live WasmAppHost URL (ephemeral port), e.g.
 *   http://127.0.0.1:5420/
 * Launch the Browser project separately — this suite does not embed demo backdoors.
 */
export default defineConfig({
  testDir: './tests',
  timeout: 120_000,
  expect: { timeout: 30_000 },
  fullyParallel: false,
  retries: 1,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: path.join(outDir, 'playwright-report') }]],
  use: {
    baseURL: process.env.AE_BROWSER_URL ?? 'http://127.0.0.1:5420/',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
    ...devices['Desktop Chrome'],
    viewport: { width: 1400, height: 900 },
  },
  outputDir: path.join(outDir, 'test-results'),
});
