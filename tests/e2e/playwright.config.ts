import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [
    ['html', { outputFolder: 'playwright-report' }],
    ['list']
  ],
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    // Only run chromium in CI for faster, more reliable tests
    // Firefox and webkit can be enabled locally if needed
    ...(!process.env.CI ? [
      {
        name: 'firefox',
        use: { ...devices['Desktop Firefox'] },
      },
      {
        name: 'webkit',
        use: { ...devices['Desktop Safari'] },
      },
    ] : []),
  ],
  // In CI, the workflow starts servers manually with proper readiness checks
  // In local dev, Playwright starts the servers automatically unless USE_EXISTING_SERVERS=true
  webServer: process.env.CI ? undefined : (process.env.USE_EXISTING_SERVERS ? undefined : [
    {
      command: 'cd ../../frontend/user-portal && npm run dev',
      url: 'http://localhost:5173',
      reuseExistingServer: true,
      timeout: 120000,
    },
    {
      command: 'cd ../../src/GymnasticsPlatform.Api && dotnet run',
      url: 'http://localhost:5137/health',
      reuseExistingServer: true,
      timeout: 120000,
    },
  ]),
});
