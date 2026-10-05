import { defineConfig } from "@playwright/test";

// Deterministic browser checks use mocked API data and never mutate a real agreement.
export default defineConfig({
  testDir: ".",
  testMatch: "of-ui-parity.spec.ts",
  workers: 1,
  reporter: "list",
  use: {
    channel: process.env.PLAYWRIGHT_CHANNEL,
    baseURL: "http://127.0.0.1:5174",
    viewport: { width: 1440, height: 1000 },
  },
  webServer: {
    command: "npm run dev -- --host 127.0.0.1",
    url: "http://127.0.0.1:5174",
    reuseExistingServer: !process.env.CI,
  },
});
