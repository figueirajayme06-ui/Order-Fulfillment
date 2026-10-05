import { describe, expect, it } from "vitest";
import type { Asset, AssetEvent } from "../../types";
import {
  buildAssetTimelinePeriod,
  buildAssetTimelineRows,
  buildAssetTimelineTasks,
  deriveAssetTimelineEventDivisions,
  filterAssetTimelineAssets,
  formatAssetTimelineRange,
  normalizeAssetEventsByAssetId,
  toAssetTimelineDateValue,
} from "./assetsTimelineModel";

function createAsset(id: string, overrides: Partial<Asset> = {}): Asset {
  return {
    id,
    individualItemNumber: `Individual ${id}`,
    itemNumber: `Item ${id}`,
    status: "Available",
    warehouse: "GLA",
    division: "01",
    facility: null,
    estimatedReadyDate: null,
    telemetryStatus: null,
    agreementNumber: null,
    customerName: null,
    deliveryDate: null,
    agreementLineValidFromDate: null,
    agreementLineValidToDate: null,
    description: null,
    warehouseLocation: null,
    collectionDate: null,
    terminationDate: null,
    daysOffHire: null,
    ...overrides,
  };
}

function createEvent(overrides: Partial<AssetEvent> = {}): AssetEvent {
  return {
    assetId: "ASSET-1",
    eventType: "ONHIRE",
    startDate: "2026-08-10T12:00:00.000Z",
    endDate: "2026-08-20T12:00:00.000Z",
    title: "Planned hire",
    cssClass: "onhire_event",
    ...overrides,
  };
}

function dateParts(date: Date) {
  return [date.getFullYear(), date.getMonth() + 1, date.getDate()];
}

describe("Asset timeline model", () => {
  it("filters every left-side Asset timeline field case-insensitively", () => {
    const assets = [
      createAsset("ASSET-EXPO", {
        status: "Service",
        itemNumber: "ITEM-42",
        warehouse: "MAN",
      }),
      createAsset("ASSET-YARD", { status: "Available", itemNumber: "ITEM-7", warehouse: "GLA" }),
    ];

    expect(filterAssetTimelineAssets(assets, { id: "expo" }).map(({ id }) => id)).toEqual(["ASSET-EXPO"]);
    expect(filterAssetTimelineAssets(assets, { id: ["expo", "yard"] }).map(({ id }) => id)).toEqual([
      "ASSET-EXPO",
      "ASSET-YARD",
    ]);
    expect(filterAssetTimelineAssets(assets, { status: "serv" }).map(({ id }) => id)).toEqual(["ASSET-EXPO"]);
    expect(filterAssetTimelineAssets(assets, { status: ["service", "AVAILABLE"] }).map(({ id }) => id)).toEqual([
      "ASSET-EXPO",
      "ASSET-YARD",
    ]);
    expect(filterAssetTimelineAssets(assets, { itemNumber: "42" }).map(({ id }) => id)).toEqual(["ASSET-EXPO"]);
    expect(filterAssetTimelineAssets(assets, { warehouse: "man" }).map(({ id }) => id)).toEqual(["ASSET-EXPO"]);
    expect(filterAssetTimelineAssets(assets, { warehouse: ["MAN"], division: ["01"] }).map(({ id }) => id)).toEqual([
      "ASSET-EXPO",
    ]);
    expect(filterAssetTimelineAssets(assets, { warehouse: ["MA"] })).toEqual([]);
  });

  it("preserves an explicit division selection and derives first-seen semicolon divisions otherwise", () => {
    const assets = [
      createAsset("1", { division: " 01 " }),
      createAsset("2", { division: "01" }),
      createAsset("3", { division: null }),
      createAsset("4", { division: " " }),
      createAsset("5", { division: "02" }),
      createAsset("6", { division: "ab" }),
      createAsset("7", { division: "AB" }),
    ];
    const originalAssets = assets.map((asset) => ({ ...asset }));

    expect(deriveAssetTimelineEventDivisions("01, 02 ", assets)).toBe("01, 02 ");
    expect(deriveAssetTimelineEventDivisions("", assets)).toBe("01;02;ab;AB");
    expect(deriveAssetTimelineEventDivisions("", [])).toBe("");
    expect(assets).toEqual(originalAssets);
  });

  it("normalizes response keys and event IDs, orders null dates first, and leaves source events unchanged", () => {
    const source = {
      " asset-1 ": [
        createEvent({ assetId: " other-id ", startDate: "2026-08-20", title: "Late" }),
        createEvent({ assetId: null, startDate: null, title: "Missing date" }),
        createEvent({ assetId: "", startDate: "2026-08-01", title: "Early" }),
        createEvent({ assetId: "   ", startDate: "2026-08-15", title: "Whitespace ID" }),
      ],
      "   ": [createEvent({ title: "Ignored blank key" })],
    };
    const originalSource = structuredClone(source);

    const result = normalizeAssetEventsByAssetId(source);

    expect(Object.keys(result)).toEqual(["ASSET-1"]);
    expect(result["ASSET-1"].map((event) => event.title)).toEqual(["Missing date", "Early", "Whitespace ID", "Late"]);
    expect(result["ASSET-1"].map((event) => event.assetId)).toEqual(["ASSET-1", "ASSET-1", "", "OTHER-ID"]);
    expect(source).toEqual(originalSource);
    expect(result["ASSET-1"][0]).not.toBe(source[" asset-1 "][1]);
  });

  it("preserves the current later-key overwrite for normalized key collisions", () => {
    const result = normalizeAssetEventsByAssetId({
      " asset-1 ": [createEvent({ title: "First key" })],
      "ASSET-1": [createEvent({ title: "Later key" })],
    });

    expect(result["ASSET-1"].map((event) => event.title)).toEqual(["Later key"]);
  });

  it("maps every known CSS class and label case-insensitively without trimming unknown values", () => {
    const mappings: Array<[string | null, string | null, string, string]> = [
      ["RINGFENCE_EVENT", "ringfence", "event-ringfence", "Ringfence"],
      ["onhire_event", "ONHIRE", "event-onhire", "On Hire"],
      ["reserved_event", "reserved", "event-reserved", "Reserved"],
      ["service_event", "SERVICE", "event-service", "Service"],
      ["repair_event", "repair", "event-repair", "Repair"],
      ["collection_event", "COLLECTION", "event-collection", "Collection"],
      ["transport_event", "transport", "event-transport", "Transport"],
      ["onhold_event", "ONHOLD", "event-onhold", "On Hold"],
      [" service_event ", " service ", "event-unknown", "Event"],
      [null, null, "event-unknown", "Event"],
    ];
    const asset = createAsset("asset-1");
    const events = mappings.map(([cssClass, eventType], index) =>
      createEvent({ cssClass, eventType, title: String(index) }),
    );

    const rows = buildAssetTimelineRows([asset], { "ASSET-1": events });

    expect(rows.map((row) => [row.eventClass, row.eventLabel])).toEqual(
      mappings.map(([, , eventClass, eventLabel]) => [eventClass, eventLabel]),
    );
    expect(rows.map((row) => row.rowId)).toEqual(
      mappings.map(
        ([cssClass, eventType, eventClass], index) =>
          `asset-1::${eventClass}::${encodeURIComponent(JSON.stringify(["2026-08-10T12:00:00.000Z", "2026-08-20T12:00:00.000Z", eventType, String(index), cssClass]))}::0`,
      ),
    );
  });

  it("expands assets in asset/event order and creates one exact no-event alignment row", () => {
    const first = createAsset(" asset-1 ");
    const second = createAsset("asset-2");
    const firstEvents = [
      createEvent({ cssClass: "service_event", eventType: "SERVICE", title: "First" }),
      createEvent({ cssClass: "repair_event", eventType: "REPAIR", title: "Second" }),
    ];
    const originalEvents = firstEvents.map((event) => ({ ...event }));

    const rows = buildAssetTimelineRows([first, second], {
      "ASSET-1": firstEvents,
      "UNUSED-ASSET": [createEvent({ title: "Ignored" })],
    });

    expect(rows.map(({ rowId, eventClass, eventLabel }) => ({ rowId, eventClass, eventLabel }))).toEqual([
      {
        rowId: ` asset-1 ::event-service::${encodeURIComponent('["2026-08-10T12:00:00.000Z","2026-08-20T12:00:00.000Z","SERVICE","First","service_event"]')}::0`,
        eventClass: "event-service",
        eventLabel: "Service",
      },
      {
        rowId: ` asset-1 ::event-repair::${encodeURIComponent('["2026-08-10T12:00:00.000Z","2026-08-20T12:00:00.000Z","REPAIR","Second","repair_event"]')}::0`,
        eventClass: "event-repair",
        eventLabel: "Repair",
      },
      { rowId: "asset-2::none", eventClass: "event-none", eventLabel: "" },
    ]);
    expect(rows[0].asset).toBe(first);
    expect(rows[2].event).toBeNull();
    expect(firstEvents).toEqual(originalEvents);
  });

  it("maps event and no-event rows to exact tasks with explicit fallback dates and title fallbacks", () => {
    const itemFallback = createAsset("asset-1", { itemNumber: "ITEM-FALLBACK" });
    const idFallback = createAsset("asset-2", { itemNumber: null });
    const noEvent = createAsset("asset-3");
    const rows = buildAssetTimelineRows([itemFallback, idFallback, noEvent], {
      "ASSET-1": [
        createEvent({ startDate: null, endDate: null, title: "   ", cssClass: "service_event", eventType: "SERVICE" }),
      ],
      "ASSET-2": [
        createEvent({
          startDate: "2026-08-10T18:30:00.000Z",
          endDate: "2026-08-20T18:30:00.000Z",
          title: null,
          cssClass: "repair_event",
          eventType: "REPAIR",
        }),
      ],
    });

    const tasks = buildAssetTimelineTasks(rows, new Date(2026, 7, 1));

    expect(tasks).toEqual([
      {
        id: `asset-1::event-service::${encodeURIComponent('[null,null,"SERVICE","   ","service_event"]')}::0`,
        name: "Service: ITEM-FALLBACK",
        start: "2026-08-01",
        end: "2026-08-01",
        progress: 100,
        custom_class: "event-service",
        period: { start: null, end: null },
        row: 0,
      },
      {
        id: `asset-2::event-repair::${encodeURIComponent('["2026-08-10T18:30:00.000Z","2026-08-20T18:30:00.000Z","REPAIR",null,"repair_event"]')}::0`,
        name: "Repair: asset-2",
        start: "2026-08-10",
        end: "2026-08-20",
        progress: 100,
        custom_class: "event-repair",
        period: { start: "2026-08-10", end: "2026-08-20" },
        row: 1,
      },
      {
        id: "asset-3::none",
        name: "",
        start: "2026-08-01",
        end: "2026-08-01",
        progress: 0,
        custom_class: "event-none",
        row: 2,
      },
    ]);
  });

  it("clips event bars at the twelve-month history boundary", () => {
    const rows = buildAssetTimelineRows([createAsset("asset-1")], {
      "ASSET-1": [createEvent({ startDate: "2024-01-01", endDate: "2025-09-01" })],
    });

    expect(buildAssetTimelineTasks(rows, new Date(2025, 7, 14))[0]).toMatchObject({
      start: "2025-08-14",
      end: "2025-09-01",
    });
  });

  it("keeps inclusive same-day geometry and retains reversed or missing source endpoints", () => {
    const rows = buildAssetTimelineRows([createAsset("asset-1")], {
      "ASSET-1": [
        createEvent({ startDate: "2026-08-20T10:00:00Z", endDate: "2026-08-20T18:00:00Z" }),
        createEvent({ startDate: "2026-09-10T10:00:00Z", endDate: "2026-09-01T18:00:00Z" }),
        createEvent({ startDate: "2026-10-05T10:00:00Z", endDate: null }),
      ],
    });

    expect(buildAssetTimelineTasks(rows, new Date(2026, 7, 1)).map(({ start, end }) => ({ start, end }))).toEqual([
      { start: "2026-08-20", end: "2026-08-20" },
      { start: "2026-09-10", end: "2026-09-10" },
      { start: "2026-10-05", end: "2026-10-05" },
    ]);
    expect(buildAssetTimelineTasks(rows, new Date(2026, 7, 1)).map((task) => task.period)).toEqual([
      { start: "2026-08-20", end: "2026-08-20" },
      { start: "2026-09-10", end: "2026-09-01" },
      { start: "2026-10-05", end: null },
    ]);
  });

  it("caps task geometry while retaining source dates and marking invalid endpoints unknown", () => {
    const rows = buildAssetTimelineRows([createAsset("asset-1")], {
      "ASSET-1": [
        createEvent({ startDate: "2020-01-01", endDate: "2099-12-31" }),
        createEvent({ startDate: "2020-01-01", endDate: "2029-12-28" }),
        createEvent({ startDate: "2020-01-01", endDate: "not-a-date" }),
      ],
    });

    expect(buildAssetTimelineTasks(rows, new Date(2026, 7, 1)).map((task) => task.end)).toEqual([
      "2036-07-28",
      "2029-12-28",
      "2026-08-01",
    ]);
    expect(buildAssetTimelineTasks(rows, new Date(2026, 7, 1)).map((task) => task.period)).toEqual([
      { start: "2020-01-01", end: "2099-12-31" },
      { start: "2020-01-01", end: "2029-12-28" },
      { start: "2020-01-01", end: null },
    ]);
  });

  it("builds a rolling twelve-month history through the current three-month horizon", () => {
    const sameYear = buildAssetTimelinePeriod(new Date(2026, 7, 14, 18, 30));
    const crossYear = buildAssetTimelinePeriod(new Date(2026, 10, 8, 18, 30));

    expect(dateParts(sameYear.start)).toEqual([2025, 8, 14]);
    expect(dateParts(sameYear.end)).toEqual([2026, 10, 31]);
    expect(toAssetTimelineDateValue(sameYear.start)).toBe("2025-08-14");
    expect(toAssetTimelineDateValue(sameYear.end)).toBe("2026-10-31");
    expect(formatAssetTimelineRange(sameYear.start, sameYear.end)).toBe("Aug 2025 to Oct 2026");

    expect(dateParts(crossYear.start)).toEqual([2025, 11, 8]);
    expect(dateParts(crossYear.end)).toEqual([2027, 1, 31]);
    expect(formatAssetTimelineRange(crossYear.start, crossYear.end)).toBe("Nov 2025 to Jan 2027");
  });

  it("does not mutate rows, assets, events, or the explicit fallback date while building tasks", () => {
    const asset = createAsset("asset-1");
    const event = createEvent({ startDate: null, endDate: null });
    const rows = buildAssetTimelineRows([asset], { "ASSET-1": [event] });
    const rowSnapshot = rows.map((row) => ({ ...row, asset: { ...row.asset }, event: row.event && { ...row.event } }));
    const fallbackDate = new Date(2026, 7, 1);
    const fallbackTime = fallbackDate.getTime();

    const tasks = buildAssetTimelineTasks(rows, fallbackDate);

    expect(tasks).toHaveLength(1);
    expect(rows).toEqual(rowSnapshot);
    expect(asset).toEqual(rowSnapshot[0].asset);
    expect(event).toEqual(rowSnapshot[0].event);
    expect(fallbackDate.getTime()).toBe(fallbackTime);
  });
});
