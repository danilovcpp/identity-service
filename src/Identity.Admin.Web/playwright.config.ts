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
  projects: [
    {
      name: 'chromium',
      testIgnore: [
        '**/invitations.spec.ts',
        '**/accountStatus.spec.ts',
        '**/antDesign.spec.ts',
        '**/passwordReset.spec.ts',
        '**/sessions.spec.ts',
      ],
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'chromium-visual',
      testMatch: '**/antDesign.spec.ts',
      dependencies: ['chromium-sessions'],
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'chromium-sessions',
      testMatch: '**/sessions.spec.ts',
      dependencies: ['chromium-password-reset'],
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'chromium-password-reset',
      testMatch: '**/passwordReset.spec.ts',
      dependencies: ['chromium-account-status'],
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'chromium-account-status',
      testMatch: '**/accountStatus.spec.ts',
      dependencies: ['chromium-invitations'],
      use: { ...devices['Desktop Chrome'] },
    },
    // These scenarios create users in the shared disposable database. Run them
    // after dashboard baseline assertions, rather than racing live counts.
    {
      name: 'chromium-invitations',
      testMatch: '**/invitations.spec.ts',
      dependencies: ['chromium'],
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: `npm run dev -- --port ${port}`,
    url: baseURL,
    reuseExistingServer: false,
    timeout: 30_000,
  },
});
