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
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
  // Only start servers automatically when not using existing servers
  // Set USE_EXISTING_SERVERS=true when services are already running
  webServer: process.env.CI || process.env.USE_EXISTING_SERVERS ? undefined : [
    {
      command: 'cd ../../frontend/user-portal && npm run dev',
      url: 'http://localhost:5173',
      reuseExistingServer: true,
      timeout: 120000,
    },
    {
      command: 'cd ../../src/GymnasticsPlatform.Api && dotnet run',
      url: 'http://localhost:5001/health',
      reuseExistingServer: true,
      timeout: 120000,
    },
  ],
});
