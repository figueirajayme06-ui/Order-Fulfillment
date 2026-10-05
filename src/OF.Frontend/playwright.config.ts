import { existsSync } from "node:fs";
import { defineConfig, devices } from "@playwright/test";

// Match Vite's existing local certificate detection; browser fixtures need no backend.
const baseURL =
  existsSync("certs/localhost-key.pem") && existsSync("certs/localhost.pem")
    ? "https://localhost:5174"
    : "http://localhost:5174";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: "html",
  use: {
    baseURL,
    trace: "on-first-retry",
    ignoreHTTPSErrors: true,
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], channel: process.env.PLAYWRIGHT_CHANNEL },
    },
  ],
  webServer: {
    command: "npm run dev",
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    ignoreHTTPSErrors: true,
  },
});
