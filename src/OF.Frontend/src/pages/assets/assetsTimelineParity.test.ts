import { describe, expect, it } from "vitest";
import type { Asset, AssetEvent } from "../../types";
import { buildAssetTimelineRows, buildAssetTimelineTasks } from "./assetsTimelineModel";

const asset = { id: "ASSET-1", itemNumber: "ITEM" } as Asset;
function event(title: string, startDate: string | null, endDate: string | null): AssetEvent {
  return { assetId: asset.id, eventType: "RESERVED", cssClass: "reserved_event", title, startDate, endDate };
}

describe("Legacy reservation stacking parity", () => {
  it("stacks overlapping events and reuses lanes for non-overlapping events", () => {
    const events = [
      event("outer", "2026-08-01", "2026-08-31"),
      event("nested", "2026-08-05", "2026-08-10"),
      event("identical", "2026-08-05", "2026-08-10"),
      event("adjacent", "2026-09-01", "2026-09-02"),
    ];
    const rows = buildAssetTimelineRows([asset], { "ASSET-1": [...events].reverse() });
    expect(rows.map((row) => row.event?.title)).toEqual(["outer", "identical", "nested", "adjacent"]);
    expect(rows.map((row) => row.laneIndex)).toEqual([0, 1, 2, 0]);
    expect(rows.map((row) => row.timelineRowIndex)).toEqual([0, 1, 2, 0]);
    expect(buildAssetTimelineTasks(rows, new Date(2026, 7, 1))).toHaveLength(4);
    expect(buildAssetTimelineTasks(rows, new Date(2026, 7, 1)).map((task) => task.row)).toEqual([0, 1, 2, 0]);
    expect(rows.every((row) => row.asset.id === "ASSET-1")).toBe(true);
    expect(new Set(rows.map((row) => row.rowId)).size).toBe(4);
  });

  it("retains event identity when earlier events are inserted and disambiguates exact duplicate records", () => {
    const later = event("later", "2026-08-20", "2026-08-25");
    const original = buildAssetTimelineRows([asset], { "ASSET-1": [later] });
    const updated = buildAssetTimelineRows([asset], {
      "ASSET-1": [event("early", "2026-08-01", "2026-08-10"), later, later],
    });
    expect(updated[1].rowId).toBe(original[0].rowId);
    expect(updated[2].rowId).not.toBe(updated[1].rowId);
  });

  it("retains authoritative dates after clipping and never substitutes rendering fallback in metadata", () => {
    const rows = buildAssetTimelineRows([asset], {
      "ASSET-1": [event("clipped", "2024-01-01T23:00:00-12:00", "2099-12-31"), event("unknown", null, null)],
    });
    const tasks = buildAssetTimelineTasks(rows, new Date(2026, 7, 1));
    expect(tasks[0].period).toEqual({ start: null, end: null });
    expect(tasks[1].period).toEqual({ start: "2024-01-01", end: "2099-12-31" });
    expect(tasks[1]).toMatchObject({ start: "2026-08-01", end: "2036-07-28" });
  });

  it("keeps an empty asset context row with no visible event bar", () => {
    const rows = buildAssetTimelineRows([asset], {});
    expect(rows).toHaveLength(1);
    expect(rows[0].event).toBeNull();
    expect(buildAssetTimelineTasks(rows, new Date(2026, 7, 1))[0]).toMatchObject({
      name: "",
      custom_class: "event-none",
    });
  });
});
