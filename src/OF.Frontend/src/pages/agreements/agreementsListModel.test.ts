import { describe, expect, it } from "vitest";
import type { AgreementListItem } from "../../services/agreementsService";
import { filterAgreements, sortAgreements, type AgreementListFilters } from "./agreementsListModel";

function createAgreement(id: number, overrides: Partial<AgreementListItem> = {}): AgreementListItem {
  return {
    id,
    agreementNumber: `A-${id}`,
    customerName: `Customer ${id}`,
    customerNumber: `C-${id}`,
    division: "01",
    warehouse: "GLA",
    fulfilmentStatus: 0,
    onHireDate: "2026-08-01",
    offHireDate: "2026-08-31",
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

function createFilters(overrides: Partial<AgreementListFilters> = {}): AgreementListFilters {
  return {
    orderTypeFilter: "",
    showHistorical: true,
    columnFilters: {},
    ...overrides,
  };
}

describe("filterAgreements", () => {
  it("uses partial OR matching within multi-value columns and AND across columns", () => {
    const agreements = [
      createAgreement(101, { agreementNumber: "A-101", customerName: "North Customer" }),
      createAgreement(204, { agreementNumber: "A-204", customerName: "South Customer" }),
      createAgreement(1010, { agreementNumber: "A-1010", customerName: "North Customer" }),
    ];

    expect(
      filterAgreements(
        agreements,
        createFilters({
          showHistorical: true,
          columnFilters: { agreementNumber: ["a-101", "A-204"], customerName: ["north"] },
        }),
      ).map((agreement) => agreement.agreementNumber),
    ).toEqual(["A-101", "A-1010"]);
  });
  it("filters quote, temporary-agreement, and agreement prefixes case-insensitively", () => {
    const agreements = [
      createAgreement(1, { agreementNumber: "Q-1" }),
      createAgreement(2, { agreementNumber: "q-2" }),
      createAgreement(3, { agreementNumber: "T-3" }),
      createAgreement(4, { agreementNumber: "t-4" }),
      createAgreement(5, { agreementNumber: "A-5" }),
      createAgreement(6, { agreementNumber: "a-6" }),
      createAgreement(7, { agreementNumber: "X-7" }),
      createAgreement(8, { agreementNumber: null }),
    ];

    expect(filterAgreements(agreements, createFilters({ orderTypeFilter: "quote" })).map(({ id }) => id)).toEqual([
      1, 2,
    ]);
    expect(
      filterAgreements(agreements, createFilters({ orderTypeFilter: "temporaryAgreement" })).map(({ id }) => id),
    ).toEqual([3, 4]);
    expect(filterAgreements(agreements, createFilters({ orderTypeFilter: "agreement" })).map(({ id }) => id)).toEqual([
      5, 6,
    ]);
    expect(
      filterAgreements(agreements, createFilters({ orderTypeFilter: "quote,agreement" })).map(({ id }) => id),
    ).toEqual([1, 2, 5, 6]);
  });

  it("uses the existing inclusive thirty-day historical cutoff", () => {
    const now = new Date(2026, 7, 14, 15, 30, 45, 123);
    const cutoff = new Date(now.getTime());
    cutoff.setHours(0, 0, 0, 0);
    cutoff.setDate(cutoff.getDate() - 30);
    const justBeforeCutoff = new Date(cutoff.getTime() - 1);
    const justAfterCutoff = new Date(cutoff.getTime() + 1);
    const agreements = [
      createAgreement(1, { offHireDate: justBeforeCutoff.toISOString() }),
      createAgreement(2, { offHireDate: cutoff.toISOString() }),
      createAgreement(3, { offHireDate: justAfterCutoff.toISOString() }),
    ];

    const filtered = filterAgreements(agreements, createFilters({ showHistorical: false }), now);

    expect(filtered.map(({ id }) => id)).toEqual([2, 3]);
  });

  it("keeps null and invalid off-hire dates and bypasses the cutoff when historical is shown", () => {
    const now = new Date(2026, 7, 14, 12);
    const agreements = [
      createAgreement(1, { offHireDate: "2020-01-01T00:00:00.000Z" }),
      createAgreement(2, { offHireDate: null }),
      createAgreement(3, { offHireDate: "not-a-date" }),
    ];

    expect(filterAgreements(agreements, createFilters({ showHistorical: false }), now).map(({ id }) => id)).toEqual([
      2, 3,
    ]);
    expect(filterAgreements(agreements, createFilters({ showHistorical: true }), now).map(({ id }) => id)).toEqual([
      1, 2, 3,
    ]);
  });

  it("ANDs case-insensitive column filters and stringifies numeric status", () => {
    const agreements = [
      createAgreement(1, { customerName: "NorthWind Logistics", fulfilmentStatus: 0 }),
      createAgreement(2, { customerName: "Northwind Logistics", fulfilmentStatus: 1 }),
      createAgreement(3, { customerName: "Southwind Logistics", fulfilmentStatus: 0 }),
    ];

    const filtered = filterAgreements(
      agreements,
      createFilters({
        columnFilters: {
          customerName: "NORTH",
          fulfilmentStatus: "0",
        },
      }),
    );

    expect(filtered.map(({ id }) => id)).toEqual([1]);
  });

  it("matches the selected warehouse exactly and case-insensitively", () => {
    const agreements = [
      createAgreement(1, { warehouse: "ED1" }),
      createAgreement(2, { warehouse: "ed1" }),
      createAgreement(3, { warehouse: "ED10" }),
    ];

    const filtered = filterAgreements(agreements, createFilters({ columnFilters: { warehouse: "ED1" } }));

    expect(filtered.map(({ id }) => id)).toEqual([1, 2]);
  });

  it("uses exact OR matching for option-filter arrays and AND matching between fields", () => {
    const agreements = [
      createAgreement(1, { warehouse: "ED1", fulfilmentStatus: 0 }),
      createAgreement(2, { warehouse: "CN1", fulfilmentStatus: 1 }),
      createAgreement(3, { warehouse: "GLA", fulfilmentStatus: 1 }),
      createAgreement(4, { warehouse: "CN10", fulfilmentStatus: 3 }),
    ];

    const filtered = filterAgreements(
      agreements,
      createFilters({ columnFilters: { warehouse: ["ed1", "CN1"], fulfilmentStatus: ["0", "1"] } }),
    );

    expect(filtered.map(({ id }) => id)).toEqual([1, 2]);
  });

  it("does not match null values and does not mutate inputs or the injected date", () => {
    const now = new Date(2026, 7, 14, 9, 15);
    const originalNow = now.getTime();
    const agreements = [
      createAgreement(1, { customerName: null }),
      createAgreement(2, { customerName: "Customer Two" }),
    ];
    const originalAgreements = agreements.map((agreement) => ({ ...agreement }));
    const filters = createFilters({
      showHistorical: false,
      columnFilters: { customerName: "customer" },
    });
    const originalFilters = { ...filters, columnFilters: { ...filters.columnFilters } };

    const filtered = filterAgreements(agreements, filters, now);

    expect(filtered.map(({ id }) => id)).toEqual([2]);
    expect(filtered).not.toBe(agreements);
    expect(agreements).toEqual(originalAgreements);
    expect(filters).toEqual(originalFilters);
    expect(now.getTime()).toBe(originalNow);
  });
});

describe("sortAgreements", () => {
  it("returns a new array without mutating its input", () => {
    const agreements = [
      createAgreement(2, { agreementNumber: "A-10" }),
      createAgreement(1, { agreementNumber: "A-2" }),
    ];
    const originalOrder = [...agreements];

    const sorted = sortAgreements(agreements, "agreementNumber", "asc");

    expect(sorted.map((agreement) => agreement.id)).toEqual([1, 2]);
    expect(sorted).not.toBe(agreements);
    expect(agreements).toEqual(originalOrder);
  });

  it("uses the existing numeric-aware, case-insensitive string comparison", () => {
    const agreements = [
      createAgreement(1, { agreementNumber: "a-10" }),
      createAgreement(2, { agreementNumber: "A-2" }),
      createAgreement(3, { agreementNumber: "B-1" }),
    ];

    expect(sortAgreements(agreements, "agreementNumber", "asc").map((agreement) => agreement.id)).toEqual([2, 1, 3]);
    expect(sortAgreements(agreements, "agreementNumber", "desc").map((agreement) => agreement.id)).toEqual([3, 1, 2]);
  });

  it("sorts numeric fulfilment values in either direction", () => {
    const agreements = [
      createAgreement(1, { fulfilmentStatus: 1 }),
      createAgreement(2, { fulfilmentStatus: 3 }),
      createAgreement(3, { fulfilmentStatus: 0 }),
    ];

    expect(sortAgreements(agreements, "fulfilmentStatus", "asc").map((agreement) => agreement.id)).toEqual([3, 1, 2]);
    expect(sortAgreements(agreements, "fulfilmentStatus", "desc").map((agreement) => agreement.id)).toEqual([2, 1, 3]);
  });

  it("keeps null values last in both directions", () => {
    const agreements = [
      createAgreement(1, { offHireDate: null }),
      createAgreement(2, { offHireDate: "2026-09-01" }),
      createAgreement(3, { offHireDate: "2026-08-01" }),
    ];

    expect(sortAgreements(agreements, "offHireDate", "asc").map((agreement) => agreement.id)).toEqual([3, 2, 1]);
    expect(sortAgreements(agreements, "offHireDate", "desc").map((agreement) => agreement.id)).toEqual([2, 3, 1]);
  });
});
