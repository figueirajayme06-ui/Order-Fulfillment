import { describe, expect, it } from "vitest";
import type { Asset } from "../../types";
import { filterAssets, sortAssets, type AssetColumnFilters } from "./assetsListModel";

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
    agreementNumber: `Agreement ${id}`,
    customerName: `Customer ${id}`,
    deliveryDate: null,
    agreementLineValidFromDate: null,
    agreementLineValidToDate: null,
    description: `Description ${id}`,
    warehouseLocation: null,
    collectionDate: null,
    terminationDate: null,
    daysOffHire: null,
    ...overrides,
  };
}

describe("filterAssets", () => {
  it("matches multiple asset identifier fragments and descriptive values by contains", () => {
    const assets = [
      createAsset("ASSET-101", { description: "Quiet generator" }),
      createAsset("ASSET-204", { description: "Lighting tower" }),
      createAsset("ASSET-1010", { description: "Quiet generator" }),
    ];
    expect(
      filterAssets(assets, { id: ["asset-101", "ASSET-204"], description: ["quiet"] }).map((asset) => asset.id),
    ).toEqual(["ASSET-101", "ASSET-1010"]);
  });
  it("matches the customer column only by the displayed customer value", () => {
    const assets = [
      createAsset("customer-match", { customerName: "Acme Hire", agreementNumber: "AGR-100" }),
      createAsset("agreement-match", { customerName: "Beta Hire", agreementNumber: "AGR-200" }),
      createAsset("excluded", { customerName: "Gamma Hire", agreementNumber: "AGR-300" }),
    ];

    expect(filterAssets(assets, { customerName: "aCmE" }).map((asset) => asset.id)).toEqual(["customer-match"]);
    expect(filterAssets(assets, { customerName: "agr-200" })).toEqual([]);
  });

  it("matches the warehouse column only by the displayed warehouse value", () => {
    const assets = [
      createAsset("warehouse", { warehouse: "MAN", warehouseLocation: "Bay 1" }),
      createAsset("location", { warehouse: "GLA", warehouseLocation: "Yard Alpha" }),
      createAsset("excluded", { warehouse: "ED1", warehouseLocation: "Yard Beta" }),
    ];

    expect(filterAssets(assets, { warehouse: "man" }).map((asset) => asset.id)).toEqual(["warehouse"]);
    expect(filterAssets(assets, { warehouse: "yard alpha" })).toEqual([]);
  });

  it("matches the division column only by its displayed code", () => {
    const assets = [
      createAsset("north", { division: "01" }),
      createAsset("south", { division: "02" }),
      createAsset("unknown", { division: "03" }),
    ];

    expect(filterAssets(assets, { division: "01" }).map((asset) => asset.id)).toEqual(["north"]);
    expect(filterAssets(assets, { division: "south operations" })).toEqual([]);
  });

  it("combines populated columns with AND while ignoring empty filters", () => {
    const assets = [
      createAsset("matching", { customerName: "Acme", status: "OnHire", warehouse: "MAN" }),
      createAsset("wrong-warehouse", { customerName: "Acme", status: "OnHire", warehouse: "GLA" }),
      createAsset("wrong-status", { customerName: "Acme", status: "Available", warehouse: "MAN" }),
    ];
    const filters: AssetColumnFilters = {
      customerName: "ACME",
      status: "onhire",
      warehouse: "man",
      itemNumber: "",
    };

    expect(filterAssets(assets, filters).map((asset) => asset.id)).toEqual(["matching"]);
  });

  it("uses exact OR matching for status option arrays", () => {
    const assets = [
      createAsset("available", { status: "Available" }),
      createAsset("repair", { status: "Repair" }),
      createAsset("repair-prefix", { status: "Repair Pending" }),
    ];

    expect(filterAssets(assets, { status: ["available", "REPAIR"] }).map((asset) => asset.id)).toEqual([
      "available",
      "repair",
    ]);
  });

  it("uses exact OR matching for warehouse and division option arrays", () => {
    const assets = [
      createAsset("north-glasgow", { warehouse: "GLA", division: "01" }),
      createAsset("south-manchester", { warehouse: "MAN", division: "02" }),
      createAsset("south-prefix", { warehouse: "MAN-YARD", division: "020" }),
    ];

    expect(filterAssets(assets, { warehouse: ["gla", "MAN"], division: ["01", "02"] }).map(({ id }) => id)).toEqual([
      "north-glasgow",
      "south-manchester",
    ]);
  });

  it("matches numeric days off hire by case-insensitive string containment", () => {
    const assets = [
      createAsset("substring", { daysOffHire: 123 }),
      createAsset("different", { daysOffHire: 45 }),
      createAsset("missing", { daysOffHire: null }),
    ];

    expect(filterAssets(assets, { daysOffHire: "23" }).map((asset) => asset.id)).toEqual(["substring"]);
  });

  it("filters every promoted date column independently", () => {
    const asset = createAsset("dates", {
      deliveryDate: "2026-08-01T12:00:00.000Z",
      agreementLineValidFromDate: "2026-08-02T12:00:00.000Z",
      agreementLineValidToDate: "2026-08-03T12:00:00.000Z",
      terminationDate: "2026-08-04T12:00:00.000Z",
      collectionDate: "2026-08-05T12:00:00.000Z",
    });

    expect(filterAssets([asset], { deliveryDate: "2026-08-01" })).toEqual([asset]);
    expect(filterAssets([asset], { agreementLineValidFromDate: "2026-08-02" })).toEqual([asset]);
    expect(filterAssets([asset], { agreementLineValidToDate: "2026-08-03" })).toEqual([asset]);
    expect(filterAssets([asset], { terminationDate: "2026-08-04" })).toEqual([asset]);
    expect(filterAssets([asset], { collectionDate: "2026-08-05" })).toEqual([asset]);
  });

  it("returns a new array without mutating assets or filter input", () => {
    const first = Object.freeze(createAsset("2"));
    const second = Object.freeze(createAsset("1"));
    const assets = Object.freeze([first, second]);
    const filters = Object.freeze<AssetColumnFilters>({});

    const result = filterAssets(assets, filters);

    expect(result).not.toBe(assets);
    expect(result).toEqual([first, second]);
    expect(assets.map((asset) => asset.id)).toEqual(["2", "1"]);
    expect(filters).toEqual({});
  });
});

describe("sortAssets", () => {
  it("sorts strings case-insensitively with numeric segments", () => {
    const assets = [
      createAsset("third", { itemNumber: "item 10" }),
      createAsset("second", { itemNumber: "Item 2" }),
      createAsset("first", { itemNumber: "ITEM 1" }),
    ];

    expect(sortAssets(assets, "itemNumber", "asc").map((asset) => asset.id)).toEqual(["first", "second", "third"]);
    expect(sortAssets(assets, "itemNumber", "desc").map((asset) => asset.id)).toEqual(["third", "second", "first"]);
  });

  it.each([
    ["asc" as const, ["2", "10", "missing"]],
    ["desc" as const, ["10", "2", "missing"]],
  ])("sorts numeric values %s and leaves null last", (direction, expectedIds) => {
    const assets = [
      createAsset("missing", { daysOffHire: null }),
      createAsset("10", { daysOffHire: 10 }),
      createAsset("2", { daysOffHire: 2 }),
    ];

    expect(sortAssets(assets, "daysOffHire", direction).map((asset) => asset.id)).toEqual(expectedIds);
  });

  it.each([
    ["asc" as const, ["alpha", "zulu", "missing"]],
    ["desc" as const, ["zulu", "alpha", "missing"]],
  ])("leaves null strings last when sorting %s", (direction, expectedIds) => {
    const assets = [
      createAsset("missing", { description: null }),
      createAsset("zulu", { description: "Zulu" }),
      createAsset("alpha", { description: "alpha" }),
    ];

    expect(sortAssets(assets, "description", direction).map((asset) => asset.id)).toEqual(expectedIds);
  });

  it("returns a new array without mutating the input order or asset objects", () => {
    const first = Object.freeze(createAsset("10"));
    const second = Object.freeze(createAsset("2"));
    const assets = Object.freeze([first, second]);

    const result = sortAssets(assets, "id", "asc");

    expect(result).not.toBe(assets);
    expect(result).toEqual([second, first]);
    expect(assets).toEqual([first, second]);
    expect(result[0]).toBe(second);
    expect(result[1]).toBe(first);
  });
});
