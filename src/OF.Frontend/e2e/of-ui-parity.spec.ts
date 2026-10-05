import { test, expect } from "@playwright/test";
import { agreementFixture as fixture, installAgreementApi as installApi } from "./fixtures";

test("agreement reset, T deletion and existing report navigation", async ({ page }, info) => {
  const state = fixture();
  const calls = await installApi(page, state);
  await page.goto("/agreements/42");
  await expect(page.getByRole("button", { name: "Activate", exact: true })).toBeEnabled();
  await page.screenshot({ path: info.outputPath("agreement-before.png"), fullPage: true });
  page.once("dialog", (dialog) => dialog.dismiss());
  await page.getByRole("button", { name: "Unfulfil all lines", exact: true }).click();
  expect(calls).toEqual([]);
  page.once("dialog", async (dialog) => {
    expect(dialog.message()).toContain("3 reservations");
    await dialog.accept();
  });
  await page.getByRole("button", { name: "Unfulfil all lines", exact: true }).click();
  await expect(page.getByText("All lines unfulfilled. Reservations removed: 3.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Activate", exact: true })).toBeDisabled();
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Delete line T-42-1", exact: true }).click();
  await expect(page.getByText("Line T-42-1 deleted.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Delete line T-42-2", exact: true })).toBeDisabled();
  expect(calls).toEqual(["reset", "delete"]);
  await page.screenshot({ path: info.outputPath("agreement-after.png"), fullPage: true });
  state.header.agreementNumber = "A-42";
  state.header.fulfilmentStatus = 3;
  await page.reload();
  const summary = page.getByRole("link", { name: "Order summary sheet" });
  await expect(summary).toHaveAttribute("href", "https://legacy.example/of/report/A-42/display?quoteId=quote-42");
  await expect(page.getByRole("button", { name: /^Delete line/ })).toHaveCount(0);
  await page.emulateMedia({ media: "print" });
  await expect(summary).toBeHidden();
  await page.screenshot({ path: info.outputPath("agreement-print.png"), fullPage: true });
});

test("reservation timeline stacks by line and exposes the authoritative dates to keyboard users", async ({
  page,
}, info) => {
  const state = fixture();
  await installApi(page, state);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/agreements/42/timeline");
  await expect(page.locator(".bar-wrapper")).toHaveCount(5);
  const ids = await page.locator(".bar-wrapper").evaluateAll((bars) => bars.map((bar) => bar.getAttribute("data-id")));
  expect(ids).toEqual(["line-1", "res-11", "res-12", "line-2", "res-13"]);
  const reservation = page.locator('.bar-wrapper[data-id="res-11"]');
  await expect(reservation).toHaveAttribute("tabindex", "0");
  await expect(reservation).toHaveAttribute("aria-label", "\u21b3 ASSET-11 (ITEM-1): Sep 3, 2026 \u2013 Sep 29, 2026");
  await reservation.focus();
  await expect(reservation).toBeFocused();
  await page.screenshot({ path: info.outputPath("timeline.png"), fullPage: true });
  await page.emulateMedia({ media: "print" });
  await expect(page.locator(".bar-wrapper")).toHaveCount(5);
  await page.screenshot({ path: info.outputPath("timeline-print.png"), fullPage: true });
  expect(errors).toEqual([]);
});
