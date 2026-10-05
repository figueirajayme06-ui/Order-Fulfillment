import { afterEach, describe, expect, it } from "vitest";
import {
  clearSavedViews,
  createSavedView,
  deleteSavedView,
  listSavedViews,
  updateSavedView,
} from "./savedViewsStorage";
import { parseAgreementsSavedViewState, type AgreementsSavedViewState } from "../types/savedViews";

type DecoderState = { label: string };

class MemoryStorage {
  private store = new Map<string, string>();

  getItem(key: string): string | null {
    return this.store.has(key) ? (this.store.get(key) ?? null) : null;
  }

  setItem(key: string, value: string): void {
    this.store.set(key, value);
  }

  removeItem(key: string): void {
    this.store.delete(key);
  }
}

const decodeState = (value: unknown): DecoderState | null => {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }

  const label = (value as Record<string, unknown>).label;
  return typeof label === "string" ? { label } : null;
};

describe("savedViewsStorage", () => {
  const storage = new MemoryStorage();

  afterEach(() => {
    clearSavedViews(storage);
  });

  it("creates and lists saved views for a page", () => {
    const created = createSavedView("agreements", "My Agreement View", { label: "first" }, storage);

    expect(created).not.toBeNull();

    const views = listSavedViews("agreements", decodeState, storage);

    expect(views).toHaveLength(1);
    expect(views[0].name).toBe("My Agreement View");
    expect(views[0].state.label).toBe("first");
  });

  it("keeps pages isolated", () => {
    createSavedView("agreements", "Agreements View", { label: "agreements" }, storage);
    createSavedView("assets", "Assets View", { label: "assets" }, storage);

    const agreementViews = listSavedViews("agreements", decodeState, storage);
    const assetViews = listSavedViews("assets", decodeState, storage);

    expect(agreementViews).toHaveLength(1);
    expect(assetViews).toHaveLength(1);
    expect(agreementViews[0].name).toBe("Agreements View");
    expect(assetViews[0].name).toBe("Assets View");
  });

  it("updates a saved view", () => {
    const created = createSavedView("agreements", "View A", { label: "before" }, storage);
    expect(created).not.toBeNull();

    const updated = updateSavedView("agreements", created!.id, "View B", { label: "after" }, storage);

    expect(updated).not.toBeNull();
    expect(updated!.name).toBe("View B");
    expect(updated!.state.label).toBe("after");
  });

  it("deletes a saved view", () => {
    const created = createSavedView("agreements", "Delete Me", { label: "gone" }, storage);
    expect(created).not.toBeNull();

    const deleted = deleteSavedView("agreements", created!.id, storage);

    expect(deleted).toBe(true);
    expect(listSavedViews("agreements", decodeState, storage)).toHaveLength(0);
  });

  it("ignores corrupt storage payloads", () => {
    storage.setItem("nof.savedViews.v1", "{bad-json");

    const views = listSavedViews("agreements", decodeState, storage);

    expect(views).toHaveLength(0);
  });

  it("filters out entries with invalid state via decoder", () => {
    storage.setItem(
      "nof.savedViews.v1",
      JSON.stringify({
        schemaVersion: 1,
        views: [
          {
            id: "a",
            name: "Good",
            page: "orders",
            state: { label: "ok" },
            createdAt: "2026-01-01T00:00:00.000Z",
            updatedAt: "2026-01-01T00:00:00.000Z",
          },
          {
            id: "b",
            name: "Bad",
            page: "orders",
            state: { unknown: true },
            createdAt: "2026-01-01T00:00:00.000Z",
            updatedAt: "2026-01-01T00:00:00.000Z",
          },
        ],
      }),
    );

    const views = listSavedViews("agreements", decodeState, storage);

    expect(views).toHaveLength(1);
    expect(views[0].name).toBe("Good");
  });

  it("round trips an Agreement view sorted by off-hire date with its date-filter state", () => {
    const state: AgreementsSavedViewState = {
      stateVersion: 2,
      viewMode: "table",
      searchTerm: "",
      showHistorical: true,
      selectedDivision: "01",
      orderTypeFilter: "agreement",
      statusFilter: "",
      advancedFilters: {},
      columnFilters: { offHireDate: "2026-08" },
      dateColumnFilters: { offHireDate: { operator: "nextMonth" } },
      sortField: "offHireDate",
      sortDirection: "desc",
    };

    const created = createSavedView("agreements", "Off-hire planning", state, storage);
    const views = listSavedViews("agreements", parseAgreementsSavedViewState, storage);

    expect(created).not.toBeNull();
    expect(views).toHaveLength(1);
    expect(views[0].state).toStrictEqual(parseAgreementsSavedViewState(state));
    expect(JSON.parse(storage.getItem("nof.savedViews.v1")!)).toMatchObject({
      schemaVersion: 1,
      views: [{ state: { stateVersion: 2, sortField: "offHireDate", sortDirection: "desc" } }],
    });
  });
});
