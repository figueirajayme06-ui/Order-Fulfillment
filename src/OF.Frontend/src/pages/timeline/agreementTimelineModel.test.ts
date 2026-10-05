import { describe, expect, it } from "vitest";
import type { AgreementLine, Reservation } from "../../types";
import { buildAgreementTimelineTasks } from "./agreementTimelineModel";

function line(id: number, overrides: Partial<AgreementLine> = {}): AgreementLine {
  return {
    id,
    isSubline: false,
    headerId: 1,
    itemNumber: "ITEM",
    genericItemNumber: null,
    quantity: 2,
    quantityFulfilled: 0,
    deliveryDate: "2026-08-01",
    validFromDate: "2026-08-02",
    validToDate: "2026-08-30",
    terminationDate: "2026-08-29",
    collectionDate: "2026-08-31",
    attributes: null,
    fulfilmentStatus: 0,
    activationStatus: 0,
    requiresFulfilment: true,
    isDeleted: false,
    changeSequence: 0,
    warehouse: "GLA",
    division: "01",
    facility: "",
    orderSource: "M3",
    orderLineNumber: null,
    agreementLineNumber: String(id),
    lastUpdatedBy: null,
    lastUpdatedDate: null,
    ...overrides,
  };
}
function reservation(id: number, lineId: number, overrides: Partial<Reservation> = {}): Reservation {
  return {
    id,
    lineId,
    assetId: `ASSET-${id}`,
    itemNumber: "ITEM",
    quantity: 1,
    effectiveQuantity: 1,
    warehouse: "GLA",
    isConfirmed: false,
    isDepotFulfilled: false,
    isRehire: false,
    actualAssetId: null,
    actualItemNumber: null,
    actualQuantity: null,
    notes: null,
    lastUpdatedBy: null,
    lastUpdatedDate: null,
    ...overrides,
  };
}

describe("Agreement reservation timeline", () => {
  it("groups every reservation immediately beneath its owning line with stable IDs", () => {
    const tasks = buildAgreementTimelineTasks(
      [line(1), line(2)],
      [reservation(3, 2), reservation(2, 1), reservation(1, 1)],
      "2026-09-08",
    );
    expect(tasks.map((task) => task.id)).toEqual(["line-1", "res-1", "res-2", "line-2", "res-3"]);
    expect(tasks[1].period).toEqual({ start: "2026-08-01", end: "2026-08-31" });
    expect(tasks[1]).toMatchObject({ start: "2026-08-01", end: "2026-08-31" });
  });

  it("uses collection, termination, valid-to precedence for serialized, depot and rehire reservations", () => {
    const tasks = buildAgreementTimelineTasks(
      [
        line(1),
        line(2, { collectionDate: null }),
        line(3, { collectionDate: null, terminationDate: null, deliveryDate: null }),
      ],
      [reservation(1, 1), reservation(2, 2, { isDepotFulfilled: true }), reservation(3, 3, { isRehire: true })],
      "2026-09-08",
    );
    expect(
      tasks.filter((task) => task.id.startsWith("res-")).map((task) => [task.start, task.end, task.custom_class]),
    ).toEqual([
      ["2026-08-01", "2026-08-31", "bar-reservation"],
      ["2026-08-01", "2026-08-29", "bar-depot"],
      ["2026-08-02", "2026-08-30", "bar-rehire"],
    ]);
  });

  it("keeps unknown dates explicit and excludes orphaned or deleted-line reservations", () => {
    const tasks = buildAgreementTimelineTasks(
      [
        line(1, {
          deliveryDate: null,
          validFromDate: "",
          collectionDate: null,
          terminationDate: null,
          validToDate: "",
        }),
        line(2, { isDeleted: true }),
      ],
      [reservation(1, 1), reservation(2, 2), reservation(3, 99)],
      "2026-09-08",
    );
    expect(tasks.map((task) => task.id)).toEqual(["line-1", "res-1"]);
    expect(tasks[1].period).toEqual({ start: null, end: null });
    expect(tasks[1]).toMatchObject({ start: "2026-09-08", end: "2026-09-08" });
  });

  it("retains every reservation on a large agreement, including 1,000 reservations on one line", () => {
    const lines = Array.from({ length: 400 }, (_, index) => line(index + 1));
    const reservations = Array.from({ length: 1000 }, (_, index) => reservation(index + 1, 1));
    const tasks = buildAgreementTimelineTasks(lines, reservations, "2026-09-08");
    expect(tasks).toHaveLength(1400);
    expect(tasks[1000].id).toBe("res-1000");
    expect(tasks[1001].id).toBe("line-2");
    expect(new Set(tasks.map((task) => task.id)).size).toBe(1400);
  });
});
