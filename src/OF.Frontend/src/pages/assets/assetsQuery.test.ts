import { describe, expect, it } from "vitest";
import { buildAssetFilterParams } from "./assetsQuery";

describe("buildAssetFilterParams", () => {
  it("maps every supported page filter to the existing API contract", () => {
    const params = buildAssetFilterParams({
      searchTerm: "generator",
      statusFilter: "Available,Repair",
      warehouseFilter: "ED1",
      selectedDivision: "110,120",
      hideRemovedStock: true,
      advancedFilters: {
        itemNumber: "ITEM-1",
        description: "Diesel",
        facility: "UKN",
        agreementNumber: "A123",
        deliveryDateFrom: "2026-01-01",
        deliveryDateTo: "2026-01-31",
        validFromDate: "2026-02-01",
        validToDate: "2026-02-28",
        warehouseLocation: "Yard A",
        individualItemNumber: "SERIAL-1",
        terminationDateFrom: "2026-03-01",
        terminationDateTo: "2026-03-31",
        collectionDateFrom: "2026-04-01",
        collectionDateTo: "2026-04-30",
        estimatedReadyDateFrom: "2026-05-01",
        estimatedReadyDateTo: "2026-05-31",
      },
    });

    expect(params).toStrictEqual({
      search: "generator",
      statuses: "Available,Repair",
      warehouse: "ED1",
      division: "110,120",
      excludeStatuses: "RemovedStock,Scrap,Sold",
      itemNumber: "ITEM-1",
      description: "Diesel",
      facility: "UKN",
      agreementNumber: "A123",
      deliveryDateFrom: "2026-01-01",
      deliveryDateTo: "2026-01-31",
      validFromDate: "2026-02-01",
      validToDate: "2026-02-28",
      warehouseLocation: "Yard A",
      individualItemNumber: "SERIAL-1",
      terminationDateFrom: "2026-03-01",
      terminationDateTo: "2026-03-31",
      collectionDateFrom: "2026-04-01",
      collectionDateTo: "2026-04-30",
      estimatedReadyDateFrom: "2026-05-01",
      estimatedReadyDateTo: "2026-05-31",
    });
  });

  it("keeps explicit undefined base fields when filters are empty", () => {
    const params = buildAssetFilterParams({
      searchTerm: "",
      statusFilter: "",
      warehouseFilter: "",
      selectedDivision: "",
      hideRemovedStock: false,
      advancedFilters: {},
    });

    expect(params).toStrictEqual({
      search: undefined,
      statuses: undefined,
      warehouse: undefined,
      division: undefined,
      excludeStatuses: undefined,
    });
  });

  it("preserves supported values, ignores empty and unknown fields, and does not mutate its input", () => {
    const advancedFilters = {
      itemNumber: "  ITEM-1  ",
      description: "",
      unrelatedPageState: "not-an-api-filter",
    };
    const originalAdvancedFilters = { ...advancedFilters };

    const params = buildAssetFilterParams({
      searchTerm: "",
      statusFilter: "",
      warehouseFilter: "",
      selectedDivision: "",
      hideRemovedStock: true,
      advancedFilters,
    });

    expect(params.itemNumber).toBe("  ITEM-1  ");
    expect(params).not.toHaveProperty("description");
    expect(params).not.toHaveProperty("unrelatedPageState");
    expect(advancedFilters).toStrictEqual(originalAdvancedFilters);
  });
});
