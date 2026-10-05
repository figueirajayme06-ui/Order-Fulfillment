import { test, expect } from "@playwright/test";

import { agreementFixture, installAgreementApi } from "./fixtures";

test.beforeEach(async ({ page }) => {
  await installAgreementApi(page, agreementFixture());
});

test.describe("Navigation", () => {
  test("should show the homepage with workspace links", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByRole("heading", { name: /good to see you/i })).toBeVisible();
    await expect(page.getByRole("navigation", { name: "Workspaces" })).toBeVisible();
    await expect(page.getByRole("link", { name: /Agreements/ })).toBeVisible();
  });

  test("should navigate to ringfence page", async ({ page }) => {
    await page.goto("/");
    await page
      .getByRole("navigation", { name: "Workspaces" })
      .getByRole("link", { name: /Ringfence/ })
      .click();
    await expect(page).toHaveURL("/ringfence");
    await expect(page.locator("h1")).toContainText("Ringfence");
    await expect(page.getByRole("navigation", { name: "Main navigation" })).toBeVisible();

    await page.getByRole("link", { name: "Home" }).click();
    await expect(page).toHaveURL("/");
    await expect(page.getByRole("navigation", { name: "Workspaces" })).toBeVisible();
  });
});
