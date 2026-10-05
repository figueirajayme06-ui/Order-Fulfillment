import { test, expect } from "@playwright/test";
import { agreementFixture, installAgreementApi } from "./fixtures";

test.beforeEach(async ({ page }) => {
  const state = agreementFixture();
  state.header.fulfilmentStatus = 0;
  await installAgreementApi(page, state);
  await page.goto("/agreements");
});

test("shows the Agreements workspace and search", async ({ page }) => {
  await expect(page.getByRole("heading", { name: "Agreements", exact: true })).toBeVisible();
  await expect(page.getByRole("searchbox", { name: "Search", exact: true })).toBeVisible();
});

test("offers the fulfilment status filter", async ({ page }) => {
  await page.getByRole("button", { name: /^Filter Fulfilment Status:/ }).click();
  await expect(page.getByRole("checkbox", { name: "Unfulfilled", exact: true })).toBeVisible();
});

test("opens the known agreement from its row", async ({ page }) => {
  const row = page.getByRole("row").filter({ hasText: "T-42" });
  await expect(row).toHaveCount(1);
  await expect(row).toBeVisible();
  await row.click();
  await expect(page).toHaveURL(/\/agreements\/42$/);
  await expect(page.getByRole("heading", { name: /T-42/ })).toBeVisible();
});

for (const period of ["current", "historical", "future"] as const) {
  test(`scrolls to today when opening a ${period} agreement timeline`, async ({ page }, testInfo) => {
    await page.clock.setFixedTime(new Date("2026-09-29T12:00:00"));
    const state = agreementFixture();
    state.header.fulfilmentStatus = 0;
    if (period !== "current") {
      const month = period === "historical" ? "06" : "12";
      state.header.onHireDate = `2026-${month}-01`;
      state.header.offHireDate = `2026-${month}-20`;
    }
    await installAgreementApi(page, state);
    await page.reload();
    if (period === "historical") await page.getByRole("checkbox", { name: "Show historical agreements" }).check();
    await page.getByRole("button", { name: "Timeline", exact: true }).click();
    const chart = page.locator(".gantt-container");
    const today = chart.locator(".current-highlight");
    await expect(today).toHaveCount(1);
    await expect
      .poll(async () => {
        const viewport = await chart.boundingBox();
        const marker = await today.boundingBox();
        return Boolean(viewport && marker && marker.x >= viewport.x && marker.x < viewport.x + viewport.width);
      })
      .toBe(true);
    await page.screenshot({ path: testInfo.outputPath("timeline-today.png"), fullPage: true });
  });
}
