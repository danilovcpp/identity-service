import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.IDENTITY_E2E_BASE_URL;
const port = process.env.IDENTITY_E2E_PORT;
if (!baseURL || !port || !process.env.IDENTITY_E2E_PASSWORD) {
  throw new Error(
    'Run scripts/Test-AdminWebSmoke.ps1 to create the disposable HTTPS backend and test accounts.',
  );
}

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  // Bound local browser/socket pressure as the real-backend suite grows.
  workers: 4,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  reporter: 'list',
  use: {
    baseURL,
    ignoreHTTPSErrors: true, // Disposable certificate; API proxy still validates via NODE_EXTRA_CA_CERTS.
    reducedMotion: 'reduce',
    trace: 'off',
    video: 'off',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `npm run dev -- --port ${port}`,
    url: baseURL,
    reuseExistingServer: false,
    timeout: 30_000,
  },
});
