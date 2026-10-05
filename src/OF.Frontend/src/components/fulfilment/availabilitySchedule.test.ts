import { describe, expect, it } from "vitest";
import type { Asset, AssetEvent } from "../../types";
import {
  buildAvailabilityScheduleRange,
  getAssetLocationLabel,
  getAvailabilityCommitments,
  getCommitmentLabel,
  normalizeAvailabilityEventsByAssetId,
} from "./availabilitySchedule";

function makeEvent(overrides: Partial<AssetEvent> = {}): AssetEvent {
  return {
    assetId: "ASSET-1",
    eventType: "RESERVED",
    startDate: "2026-08-20",
    endDate: "2026-09-10",
    title: "Reserved for A731959",
    cssClass: "reserved_event",
    ...overrides,
  };
}

function makeAsset(overrides: Partial<Asset> = {}): Asset {
  return {
    id: "ASSET-1",
    individualItemNumber: "ASSET-1",
    itemNumber: "CH0100REVAIC",
    status: "Ready",
    warehouse: "EE0",
    division: "01",
    facility: "East",
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
    ...overrides,
  };
}

describe("availabilitySchedule", () => {
  it("builds only an ordered, complete schedule range", () => {
    expect(buildAvailabilityScheduleRange("2026-09-01", "2026-09-30")).toMatchObject({
      start: new Date("2026-09-01T00:00:00Z"),
      end: new Date("2026-09-30T00:00:00Z"),
    });
    expect(buildAvailabilityScheduleRange("2026-09-01", undefined)).toBeNull();
    expect(buildAvailabilityScheduleRange("2026-10-01", "2026-09-30")).toBeNull();
  });

  it("normalizes asset IDs and orders events without mutating the response", () => {
    const late = makeEvent({ startDate: "2026-09-08", title: "Late" });
    const early = makeEvent({ startDate: "2026-09-02", title: "Early" });
    const source = { " asset-1 ": [late, early] };

    const normalized = normalizeAvailabilityEventsByAssetId(source);

    expect(normalized["ASSET-1"].map((event) => event.title)).toEqual(["Early", "Late"]);
    expect(source[" asset-1 "]).toEqual([late, early]);
  });

  it("clips overlapping commitments to the requested period and excludes outside events", () => {
    const range = buildAvailabilityScheduleRange("2026-09-01", "2026-09-10");
    const events = normalizeAvailabilityEventsByAssetId({
      "ASSET-1": [
        makeEvent(),
        makeEvent({ startDate: "2026-09-04", endDate: "2026-09-05", cssClass: "service_event" }),
        makeEvent({ startDate: "2026-10-01", endDate: "2026-10-02" }),
      ],
    });

    const commitments = getAvailabilityCommitments(" asset-1 ", events, range);

    expect(commitments).toHaveLength(2);
    expect(commitments[0]).toMatchObject({ leftPercent: 0, widthPercent: 100, kind: "reserved" });
    expect(commitments[1]).toMatchObject({ leftPercent: 30, widthPercent: 20, kind: "service" });
  });

  it("retains valid textual commitments when no timeline range is available", () => {
    const events = normalizeAvailabilityEventsByAssetId({ "ASSET-1": [makeEvent()] });

    expect(getAvailabilityCommitments("ASSET-1", events, null)).toMatchObject([
      { leftPercent: 0, widthPercent: 100, kind: "reserved" },
    ]);
  });

  it("uses explicit location, then facility and warehouse, then the selected warehouse fallback", () => {
    expect(getAssetLocationLabel(makeAsset({ warehouseLocation: "Bay 4" }), "East England", "EE0")).toBe("Bay 4");
    expect(getAssetLocationLabel(makeAsset(), "East England", "EE0")).toBe("East — EE0");
    expect(getAssetLocationLabel(makeAsset({ facility: null, warehouse: null }), "East England", "EE0")).toBe(
      "East England",
    );
  });

  it("uses event titles when present and makes event-type fallbacks readable", () => {
    expect(getCommitmentLabel(makeEvent(), "Commitment")).toBe("Reserved for A731959");
    expect(getCommitmentLabel(makeEvent({ title: "", eventType: "ON_HIRE" }), "Commitment")).toBe("On Hire");
    expect(getCommitmentLabel(makeEvent({ title: null, eventType: null }), "Commitment")).toBe("Commitment");
  });
});
