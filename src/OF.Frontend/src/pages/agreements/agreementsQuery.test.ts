import { describe, expect, it } from "vitest";
import { buildAgreementFilterParams } from "./agreementsQuery";

describe("buildAgreementFilterParams", () => {
  it("maps every supported page filter to the existing API contract", () => {
    const params = buildAgreementFilterParams({
      showHistorical: true,
      selectedDivision: "01,02",
      searchTerm: "northwind",
      orderTypeFilter: "quote,agreement",
      statusFilter: "1,3",
      advancedFilters: {
        customerName: "North",
        agreementNumber: "A-10017",
        warehouse: "GLA",
        onHireDateFrom: "2026-08-01",
        onHireDateTo: "2026-08-02",
        offHireDateFrom: "2026-08-30",
        offHireDateTo: "2026-08-31",
        deliveryDateFrom: "2026-07-31",
        deliveryDateTo: "2026-08-01",
        validFromDate: "2026-07-28",
        validToDate: "2026-09-01",
        terminationDateFrom: "2026-08-20",
        terminationDateTo: "2026-08-21",
        collectionDateFrom: "2026-09-01",
        collectionDateTo: "2026-09-02",
        customerAddress: "Glasgow",
        lastUpdatedByName: "Planner",
      },
    });

    expect(params).toStrictEqual({
      showHistorical: true,
      division: "01,02",
      search: "northwind",
      orderTypes: "quote,agreement",
      statuses: "1,3",
      customerName: "North",
      agreementNumber: "A-10017",
      warehouse: "GLA",
      onHireDateFrom: "2026-08-01",
      onHireDateTo: "2026-08-02",
      offHireDateFrom: "2026-08-30",
      offHireDateTo: "2026-08-31",
      deliveryDateFrom: "2026-07-31",
      deliveryDateTo: "2026-08-01",
      validFromDate: "2026-07-28",
      validToDate: "2026-09-01",
      terminationDateFrom: "2026-08-20",
      terminationDateTo: "2026-08-21",
      collectionDateFrom: "2026-09-01",
      collectionDateTo: "2026-09-02",
      customerAddress: "Glasgow",
      lastUpdatedByName: "Planner",
    });
  });

  it("keeps the required flag and explicit undefined base fields when filters are empty", () => {
    const params = buildAgreementFilterParams({
      showHistorical: false,
      selectedDivision: "",
      searchTerm: "",
      orderTypeFilter: "",
      statusFilter: "",
      advancedFilters: {},
    });

    expect(params).toStrictEqual({
      showHistorical: false,
      division: undefined,
      search: undefined,
      orderTypes: undefined,
      statuses: undefined,
    });
  });

  it("maps status zero rather than treating it as absent", () => {
    const params = buildAgreementFilterParams({
      showHistorical: false,
      selectedDivision: "",
      searchTerm: "",
      orderTypeFilter: "",
      statusFilter: "0",
      advancedFilters: {},
    });

    expect(params.statuses).toBe("0");
  });

  it("preserves supported values, ignores empty and unknown fields, and does not mutate its input", () => {
    const advancedFilters = {
      customerName: "  Northwind  ",
      agreementNumber: "",
      unrelatedPageState: "not-an-api-filter",
    };
    const originalAdvancedFilters = { ...advancedFilters };

    const params = buildAgreementFilterParams({
      showHistorical: false,
      selectedDivision: "",
      searchTerm: "",
      orderTypeFilter: "",
      statusFilter: "",
      advancedFilters,
    });

    expect(params.customerName).toBe("  Northwind  ");
    expect(params).not.toHaveProperty("agreementNumber");
    expect(params).not.toHaveProperty("unrelatedPageState");
    expect(advancedFilters).toStrictEqual(originalAdvancedFilters);
  });
});
