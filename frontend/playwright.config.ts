import { defineConfig, devices } from '@playwright/test';

/**
 * KasserPro E2E Test Configuration
 * @see https://playwright.dev/docs/test-configuration
 * 
 * IMPORTANT: Before running tests:
 * 1. Start backend: dotnet run (port 5243)
 * 2. Start frontend: npm run dev (port 5173)
 * 3. Run tests: npm run test:e2e
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // Run tests sequentially for this flow
  forbidOnly: false,
  retries: 0,
  workers: 1, // Single worker for sequential execution
  timeout: 60000, // 60 seconds per test
  expect: {
    timeout: 10000, // 10 seconds for assertions
  },
  reporter: [
    ['html', { open: 'never' }],
    ['list']
  ],
  
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    locale: 'ar-EG',
    timezoneId: 'Africa/Cairo',
    actionTimeout: 15000, // 15 seconds for actions
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  // Auto-start dev server before tests
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:3000',
    reuseExistingServer: true,
    timeout: 120000,
  },
});
