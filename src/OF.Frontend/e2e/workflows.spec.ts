import { test, expect } from "@playwright/test";
import { agreementFixture, installAgreementApi } from "./fixtures";

test.beforeEach(async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  const state = agreementFixture();
  state.lines[0].genericItemNumber = "GEN-1";
  await installAgreementApi(page, state);
  const asset = {
    id: "ASSET-11",
    itemNumber: "ITEM-1",
    description: "Generator",
    status: "Available",
    warehouse: "GLA",
    division: "01",
    warehouseLocation: "Bay 1",
  };
  const ringfence = {
    id: 7,
    title: "Protected fleet",
    fromDate: "2026-09-01",
    toDate: "2026-09-30",
    divisions: "01",
    warehouse: "GLA",
    owner: "test@example.com",
    assetCount: 1,
    createdBy: "test@example.com",
    createdAt: "2026-09-01",
  };
  await page.route("**/api/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    const responses: Record<string, unknown> = {
      "/api/assets": [asset],
      "/api/event/events": { events: {} },
      "/api/ringfence": [ringfence],
      "/api/ringfence/7": { ringfence, assets: [asset], items: [{ id: 1, assetId: asset.id }] },
      "/api/lookups/users": [{ loginName: "test@example.com", fullName: "Parity Tester" }],
      "/api/lookups/warehouses": [
        { warehouseCode: "GLA", warehouse: "Glasgow", facility: "GLA", divisionCode: "01", divisionName: "Scotland" },
      ],
      "/api/availability/summary": [
        {
          warehouseCode: "GLA",
          warehouse: "Glasgow",
          facility: "GLA",
          divisionCode: "01",
          divisionName: "Scotland",
          genericCode: "GEN-1",
          genericDescription: "Generator",
          itemNumber: "ITEM-1",
          descriptionIntl: "Generator",
          available: 1,
          count: 1,
          genericOnly: false,
          reservationMode: "asset",
        },
      ],
    };
    if (path in responses) return route.fulfill({ json: responses[path] });
    return route.fallback();
  });
});

test("Ringfence retains editing, native confirmation, and the Assets handoff", async ({ page }, info) => {
  await page.goto("/ringfence?ringfenceId=7");
  await expect(page.getByRole("link", { name: "ASSET-11", exact: true })).toBeVisible();
  await page.screenshot({ path: info.outputPath("ringfence.png"), fullPage: true });
  await page.getByRole("button", { name: "Edit details", exact: true }).click();
  await expect(page.getByLabel("Ringfence name", { exact: true })).toHaveValue("Protected fleet");
  await page.getByRole("button", { name: "Cancel", exact: true }).click();
  await page.getByRole("button", { name: "Remove asset ASSET-11", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await page.getByRole("button", { name: "Add assets", exact: true }).click();
  await page.getByRole("button", { name: "Choose from Assets", exact: true }).click();
  await expect(page).toHaveURL(/\/assets\?ringfenceId=7&returnTo=/);
  await expect(page.getByRole("region", { name: "Asset rows" })).toContainText("ASSET-11");
  await page.screenshot({ path: info.outputPath("assets.png"), fullPage: true });
  await page.emulateMedia({ media: "print" });
  await expect(page.getByRole("navigation", { name: "Main navigation" })).toBeHidden();
  await expect(page.locator("main")).toHaveJSProperty("clientWidth", 1440);
  await page.screenshot({ path: info.outputPath("assets-print.png"), fullPage: true });
});

test("agreement availability retains filters, resizing, and keyboard asset search", async ({ page }, info) => {
  await page.goto("/agreements/42");
  await page.getByRole("button", { name: "View availability for agreement line T-42-1", exact: true }).click();
  await expect(page.getByRole("combobox", { name: "Choose divisions", exact: true })).toBeVisible();
  const separator = page.getByRole("separator");
  await separator.focus();
  const previousSize = Number(await separator.getAttribute("aria-valuenow"));
  await page.keyboard.press("ArrowRight");
  await expect(separator).toHaveAttribute("aria-valuenow", String(previousSize + 10));
  await page.screenshot({ path: info.outputPath("availability.png"), fullPage: true });
  await page.getByRole("button", { name: "Reserve", exact: true }).first().click();
  const dialog = page.getByRole("dialog", { name: "Select Asset" });
  await expect(dialog).toBeVisible();
  const item = dialog.getByRole("textbox", { name: "Item number", exact: true });
  await item.fill("ITEM-1");
  const request = page.waitForRequest((request) => new URL(request.url()).pathname === "/api/assets");
  await item.press("Enter");
  await request;
  await expect(dialog).toContainText("ASSET-11");
  await page.screenshot({ path: info.outputPath("asset-selector.png"), fullPage: true });
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await page.emulateMedia({ media: "print" });
  await expect(page.locator("main")).toHaveJSProperty("clientWidth", 1440);
  await expect
    .poll(() => page.locator('[data-print-table="fulfilment"]').evaluate((table) => table.clientWidth))
    .toBeGreaterThan(1300);
  await page.screenshot({ path: info.outputPath("availability-print.png"), fullPage: true });
});
