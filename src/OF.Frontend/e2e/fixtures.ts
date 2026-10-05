import type { Page } from "@playwright/test";

export function agreementFixture() {
  const header = {
    id: 42,
    agreementNumber: "T-42",
    quotePublicId: "quote-42",
    customerName: "Parity test customer",
    division: "01",
    facility: "GLA",
    fulfilmentStatus: 3,
    activationStatus: 0,
    isDeleted: false,
    onHireDate: "2026-09-01",
    offHireDate: "2026-09-30",
    orderSource: "Test",
  };
  const lines = [1, 2].map((id) => ({
    id,
    headerId: 42,
    agreementLineNumber: `T-42-${id}`,
    itemNumber: `ITEM-${id}`,
    genericItemNumber: null as string | null,
    quantity: 2,
    quantityFulfilled: 2,
    fulfilmentStatus: 3,
    activationStatus: 0,
    requiresFulfilment: true,
    isDeleted: false,
    isSubline: false,
    division: "01",
    warehouse: "GLA",
    facility: "GLA",
    validFromDate: "2026-09-01",
    validToDate: "2026-09-30",
    deliveryDate: "2026-09-03",
    terminationDate: "2026-09-27",
    collectionDate: "2026-09-29",
  }));
  const reservations = [11, 12, 13].map((id) => ({
    id,
    lineId: id === 13 ? 2 : 1,
    assetId: `ASSET-${id}`,
    itemNumber: `ITEM-${id === 13 ? 2 : 1}`,
    warehouse: "GLA",
    quantity: 1,
    effectiveQuantity: 1,
    isConfirmed: false,
    isDepotFulfilled: false,
    isRehire: id === 12,
    actualQuantity: null,
    actualAssetId: null,
    actualItemNumber: null,
  }));
  return { header, lines, reservations };
}

export async function installAgreementApi(page: Page, state: ReturnType<typeof agreementFixture>) {
  const calls: string[] = [];
  await page.route("**/api/**", async (route) => {
    const url = new URL(route.request().url());
    const path = url.pathname;
    const json = (body: unknown) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
    if (path === "/api/auth/me")
      return json({
        loginName: "test@example.com",
        displayName: "Parity Tester",
        division: "01",
        language: "en",
        isReadOnly: false,
        isAdmin: false,
        isSuperAdmin: false,
      });
    if (path === "/api/app-config")
      return json({
        environmentLabel: "Test",
        showPreviewBanner: false,
        legacyFrontendUrl: "https://legacy.example/of/",
      });
    if (path === "/api/agreements")
      return json([{ ...state.header, warehouse: "GLA", customerNumber: "C-42", lineCount: state.lines.length }]);
    if (path === "/api/views" || path === "/api/ringfence") return json([]);
    if (path === "/api/lookups/divisions") return json([{ code: "01", name: "Scotland" }]);
    if (path === "/api/agreements/42") return json({ header: state.header, lines: state.lines });
    if (path === "/api/reservations/header/42") return json(state.reservations);
    if (path === "/api/agreements/42/unfulfil") {
      calls.push("reset");
      const reservationsRemoved = state.reservations.length;
      state.reservations = [];
      state.header.fulfilmentStatus = 0;
      state.lines.forEach((line) => {
        line.fulfilmentStatus = 0;
        line.quantityFulfilled = 0;
      });
      return json({ linesReset: state.lines.length, reservationsRemoved, headerStatus: 0 });
    }
    if (path === "/api/agreements/42/lines/1") {
      calls.push("delete");
      state.lines = state.lines.filter((line) => line.id !== 1);
      return json({ lineId: 1, removedReservationCount: 0, headerStatus: 0 });
    }
    if (path === "/api/activation/42") {
      calls.push("activate");
      return json({ type: "activation", headerId: 42 });
    }
    return route.fulfill({ status: 404, contentType: "application/json", body: "{}" });
  });
  return calls;
}
