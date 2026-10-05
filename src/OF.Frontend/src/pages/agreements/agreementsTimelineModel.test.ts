import { describe, expect, it } from "vitest";
import type { AgreementListItem } from "../../services/agreementsService";
import { ApiFulfilmentStatus } from "../../types";
import {
  buildAgreementTimelineTasks,
  filterAgreementTimelineAgreements,
  filterAgreementTimelineHistory,
  getAgreementTimelineHistoryStart,
} from "./agreementsTimelineModel";

function createAgreement(id: number, overrides: Partial<AgreementListItem> = {}): AgreementListItem {
  return {
    id,
    agreementNumber: `A-${id}`,
    customerName: `Customer ${id}`,
    customerNumber: `C-${id}`,
    division: "01",
    warehouse: "GLA",
    fulfilmentStatus: ApiFulfilmentStatus.Unfulfilled,
    onHireDate: "2026-08-01T12:00:00.000Z",
    offHireDate: "2026-08-31T12:00:00.000Z",
    isDeleted: false,
    orderSource: "D365",
    lineCount: 1,
    deliveryDate: null,
    validFromDate: null,
    validToDate: null,
    terminationDate: null,
    collectionDate: null,
    customerAddress: null,
    lastUpdatedByName: null,
    opportunityName: null,
    ...overrides,
  };
}

describe("buildAgreementTimelineTasks", () => {
  it("filters every left-side timeline field and combines customer with opportunity", () => {
    const agreements = [
      createAgreement(1, {
        agreementNumber: "A-EXPO",
        customerName: "Northwind",
        opportunityName: "Summer Expo",
        warehouse: "MAN",
        deliveryDate: "2026-08-20T12:00:00.000Z",
        fulfilmentStatus: ApiFulfilmentStatus.FullyFulfilled,
      }),
      createAgreement(2, { agreementNumber: "A-YARD", customerName: "Contoso", warehouse: "GLA" }),
    ];

    expect(filterAgreementTimelineAgreements(agreements, { agreementNumber: "expo" }).map(({ id }) => id)).toEqual([1]);
    expect(
      filterAgreementTimelineAgreements(agreements, { agreementNumber: ["expo", "yard"] }).map(({ id }) => id),
    ).toEqual([1, 2]);
    expect(
      filterAgreementTimelineAgreements(agreements, { customerOrOpportunity: "summer" }).map(({ id }) => id),
    ).toEqual([1]);
    expect(
      filterAgreementTimelineAgreements(agreements, { customerOrOpportunity: "cont" }).map(({ id }) => id),
    ).toEqual([2]);
    expect(filterAgreementTimelineAgreements(agreements, { warehouse: "man" }).map(({ id }) => id)).toEqual([1]);
    expect(filterAgreementTimelineAgreements(agreements, { warehouse: ["man", "GLA"] }).map(({ id }) => id)).toEqual([
      1, 2,
    ]);
    expect(filterAgreementTimelineAgreements(agreements, { deliveryDate: "2026-08-20" }).map(({ id }) => id)).toEqual([
      1,
    ]);
    expect(
      filterAgreementTimelineAgreements(agreements, {
        fulfilmentStatus: String(ApiFulfilmentStatus.FullyFulfilled),
      }).map(({ id }) => id),
    ).toEqual([1]);
    expect(
      filterAgreementTimelineAgreements(agreements, {
        fulfilmentStatus: [String(ApiFulfilmentStatus.Unfulfilled), String(ApiFulfilmentStatus.FullyFulfilled)],
      }).map(({ id }) => id),
    ).toEqual([1, 2]);
  });

  it("maps raw statuses 0, 1, and 3 to their existing classes and progress", () => {
    const agreements = [
      createAgreement(10, { fulfilmentStatus: ApiFulfilmentStatus.Unfulfilled }),
      createAgreement(11, { fulfilmentStatus: ApiFulfilmentStatus.PartiallyFulfilled }),
      createAgreement(12, { fulfilmentStatus: ApiFulfilmentStatus.FullyFulfilled }),
    ];

    expect(buildAgreementTimelineTasks(agreements, "2026-08-14")).toEqual([
      {
        id: "10",
        name: "A-10 — Customer 10",
        start: "2026-08-01",
        end: "2026-08-31",
        progress: 0,
        custom_class: "bar-unfulfilled",
      },
      {
        id: "11",
        name: "A-11 — Customer 11",
        start: "2026-08-01",
        end: "2026-08-31",
        progress: 50,
        custom_class: "bar-partial",
      },
      {
        id: "12",
        name: "A-12 — Customer 12",
        start: "2026-08-01",
        end: "2026-08-31",
        progress: 100,
        custom_class: "bar-fulfilled",
      },
    ]);
  });

  it("clips active agreements at twelve months and removes agreements that ended before that boundary", () => {
    const agreements = [
      createAgreement(13, { onHireDate: "2020-01-01", offHireDate: "2027-01-01" }),
      createAgreement(14, { onHireDate: "2024-01-01", offHireDate: "2025-08-13" }),
      createAgreement(15, { onHireDate: "2025-08-14", offHireDate: "2025-09-01" }),
    ];

    expect(getAgreementTimelineHistoryStart("2026-08-14")).toBe("2025-08-14");
    expect(filterAgreementTimelineHistory(agreements, "2026-08-14").map(({ id }) => id)).toEqual([13, 15]);
    expect(buildAgreementTimelineTasks(agreements, "2026-08-14").map(({ id, start }) => ({ id, start }))).toEqual([
      { id: "13", start: "2025-08-14" },
      { id: "15", start: "2025-08-14" },
    ]);
  });

  it("omits raw status 2 so neutral Unknown is not rendered as a fulfilled-looking bar", () => {
    const agreements = [
      createAgreement(20, { fulfilmentStatus: ApiFulfilmentStatus.Overfulfilled }),
      createAgreement(21, { fulfilmentStatus: ApiFulfilmentStatus.Unfulfilled }),
    ];

    const tasks = buildAgreementTimelineTasks(agreements, "2026-08-14");

    expect(tasks.map((task) => task.id)).toEqual(["21"]);
  });

  it("uses the explicit today and existing thirty-day fallback for missing dates", () => {
    const agreements = [
      createAgreement(30, { onHireDate: null, offHireDate: null }),
      createAgreement(31, { onHireDate: "2026-08-20T18:30:00.000Z", offHireDate: null }),
      createAgreement(32, { onHireDate: null, offHireDate: "2026-08-24T18:30:00.000Z" }),
    ];

    const tasks = buildAgreementTimelineTasks(agreements, "2026-08-14");

    expect(tasks.map(({ id, start, end }) => ({ id, start, end }))).toEqual([
      { id: "30", start: "2026-08-14", end: "2026-09-13" },
      { id: "31", start: "2026-08-20", end: "2026-09-19" },
      { id: "32", start: "2026-08-14", end: "2026-08-24" },
    ]);
  });

  it("preserves the existing reversed and equal date range handling", () => {
    const agreements = [
      createAgreement(40, { onHireDate: "2026-09-10", offHireDate: "2026-09-01" }),
      createAgreement(41, { onHireDate: "2026-08-20", offHireDate: "2026-08-20" }),
    ];

    const tasks = buildAgreementTimelineTasks(agreements, "2026-08-14");

    expect(tasks.map(({ id, start, end }) => ({ id, start, end }))).toEqual([
      { id: "40", start: "2026-09-01", end: "2026-09-17" },
      { id: "41", start: "2026-08-20", end: "2026-08-27" },
    ]);
  });

  it("stringifies IDs and preserves the agreement and customer name fallbacks", () => {
    const agreements = [
      createAgreement(42, { agreementNumber: null, customerName: null }),
      createAgreement(43, { agreementNumber: "Q-43", customerName: "Northwind" }),
    ];

    const tasks = buildAgreementTimelineTasks(agreements, "2026-08-14");

    expect(tasks.map(({ id, name }) => ({ id, name }))).toEqual([
      { id: "42", name: "— — Unknown" },
      { id: "43", name: "Q-43 — Northwind" },
    ]);
  });

  it("does not mutate its agreement inputs", () => {
    const agreements = [
      createAgreement(50, { onHireDate: null, offHireDate: null }),
      createAgreement(51, { fulfilmentStatus: ApiFulfilmentStatus.Overfulfilled }),
    ];
    const originalAgreements = agreements.map((agreement) => ({ ...agreement }));

    const tasks = buildAgreementTimelineTasks(agreements, "2026-08-14");

    expect(tasks).toHaveLength(1);
    expect(agreements).toEqual(originalAgreements);
  });
});
