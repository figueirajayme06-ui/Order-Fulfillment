import { describe, expect, it } from "vitest";
import type { WarehouseLookup } from "../../services/lookupsService";
import type { AvailabilityItem } from "../../types/availability";
import {
  buildAvailabilityGrid,
  filterAvailabilityGridByFacilities,
  filterAvailabilityGridByStock,
  filterAvailabilityGridByWarehouses,
  getAvailabilityFacilityKey,
  getDefaultHiddenWarehouseKeys,
  getAvailabilityWarehouseKey,
} from "./availabilityGrid";

function makeItem(overrides: Partial<AvailabilityItem> = {}): AvailabilityItem {
  return {
    warehouseCode: "EE0",
    warehouse: "East England",
    facility: "East",
    divisionCode: "01",
    divisionName: "United Kingdom",
    genericCode: "CH0100",
    genericDescription: "Chiller",
    itemNumber: "CH0100REVAIC",
    descriptionIntl: "Chiller 100kW",
    available: 1,
    count: 2,
    genericOnly: false,
    reservationMode: "asset",
    ...overrides,
  };
}

describe("buildAvailabilityGrid", () => {
  it("groups warehouses by division and facility with the agreement location first", () => {
    const model = buildAvailabilityGrid(
      [
        makeItem({
          warehouseCode: "IE0",
          warehouse: "Ireland",
          facility: "Dublin",
          divisionCode: "02",
          divisionName: "Ireland",
        }),
        makeItem({ warehouseCode: "EN0", warehouse: "North", facility: "North", itemNumber: "CH0100LOW" }),
        makeItem({ warehouseCode: "EE0", warehouse: "East", facility: "East" }),
      ],
      "CH0100",
      "01",
      " en0 ",
    );

    expect(model.divisions.map((division) => division.code)).toEqual(["01", "02"]);
    expect(model.divisions[0].facilities.map((facility) => facility.name)).toEqual(["North", "East"]);
    expect(model.warehouses.map((warehouse) => warehouse.code)).toEqual(["EN0", "EE0", "IE0"]);
  });

  it("keeps a related specific substitution after regular availability items", () => {
    const model = buildAvailabilityGrid(
      [makeItem({ itemNumber: "CH0100BASE" }), makeItem({ itemNumber: "AAARELATED", substitutionReason: "RELATED" })],
      "CH0100",
    );

    expect(model.items.map((item) => item.itemNumber)).toEqual(["CH0100BASE", "AAARELATED"]);
    expect(model.items[1].substitutionReason).toBe("RELATED");
  });

  it("keeps reported zero availability and normalizes warehouse codes", () => {
    const model = buildAvailabilityGrid(
      [
        makeItem({ available: 0, count: 4 }),
        makeItem({ warehouseCode: "EE5", available: 0, count: 2 }),
        makeItem({ warehouseCode: " ee1 ", available: 7, count: 7 }),
        makeItem({ warehouseCode: "EE2", available: 7, count: 7 }),
      ],
      "CH0100",
    );

    expect(model.warehouses.map((warehouse) => warehouse.code)).toEqual(["EE0", "EE1", "EE2", "EE5"]);
    expect(model.items[0].warehouseData.get("EE1")).toEqual({
      available: 7,
      count: 7,
      reservationMode: "asset",
    });
    expect(model.items[0].warehouseData.get("EE0")).toEqual({
      available: 0,
      count: 4,
      reservationMode: "asset",
    });
  });

  it("adds normalized configured local warehouses without creating availability cells", () => {
    const catalog: WarehouseLookup[] = [
      {
        warehouseCode: " ee1 ",
        warehouse: "East repair",
        facility: " UKC ",
        divisionCode: " 01 ",
        divisionName: "United Kingdom",
      },
      {
        warehouseCode: "EE1",
        warehouse: "Duplicate repair",
        facility: "Duplicate facility",
        divisionCode: "01",
        divisionName: "United Kingdom",
      },
      {
        warehouseCode: "EE0",
        warehouse: "",
        facility: "",
        divisionCode: "",
        divisionName: "",
      },
    ];

    const model = buildAvailabilityGrid([makeItem()], "CH0100", undefined, undefined, catalog);

    expect(model.warehouses.map((warehouse) => warehouse.code)).toEqual(["EE0", "EE1"]);
    expect(model.warehouses.find((warehouse) => warehouse.code === "EE1")).toMatchObject({
      name: "East repair",
      facility: "UKC",
      divisionCode: "01",
    });
    expect(model.items[0].warehouseData.has("EE1")).toBe(false);
    expect(model.items[0].warehouseData.get("EE0")).toEqual({
      available: 1,
      count: 2,
      reservationMode: "asset",
    });
    expect(model.warehouses.find((warehouse) => warehouse.code === "EE0")).toMatchObject({
      name: "East England",
      facility: "East",
      divisionCode: "01",
      divisionName: "United Kingdom",
    });
  });

  it("hides asset warehouses with no result by default, keeps zero-availability results, and protects the agreement warehouse", () => {
    const model = buildAvailabilityGrid(
      [
        makeItem({ warehouseCode: "EE0", available: 0 }),
        makeItem({ warehouseCode: "EN0", available: 0, count: 1 }),
        makeItem({ warehouseCode: "ES0", available: 2 }),
        makeItem({ warehouseCode: "EQ0", available: 8, reservationMode: "quantity" }),
      ],
      "CH0100",
      "01",
      "EE0",
      [
        {
          warehouseCode: "EM0",
          warehouse: "Missing summary",
          facility: "East",
          divisionCode: "01",
          divisionName: "United Kingdom",
        },
      ],
    );

    expect(Array.from(getDefaultHiddenWarehouseKeys(model, " ee0 ")).sort()).toEqual(["EM0", "EQ0"]);
  });

  it("orders the agreement division first without treating code casing as identity", () => {
    const model = buildAvailabilityGrid(
      [
        makeItem({ warehouseCode: "AA0", divisionCode: "AA", divisionName: "Another area" }),
        makeItem({ warehouseCode: "UK0", divisionCode: "UK", divisionName: "United Kingdom" }),
      ],
      "CH0100",
      "uk",
    );

    expect(model.divisions.map((division) => division.code)).toEqual(["UK", "AA"]);
  });

  it("filters facilities independently by their division and prunes empty groups", () => {
    const model = buildAvailabilityGrid(
      [
        makeItem({ warehouseCode: "EE0", facility: "Main", divisionCode: "01" }),
        makeItem({ warehouseCode: "EN0", facility: "North", divisionCode: "01" }),
        makeItem({ warehouseCode: "IE0", facility: "Main", divisionCode: "02" }),
      ],
      "CH0100",
    );

    const withoutFirstMain = filterAvailabilityGridByFacilities(
      model,
      new Set([getAvailabilityFacilityKey("01", " main ")]),
    );

    expect(withoutFirstMain.divisions.map((division) => division.code)).toEqual(["01", "02"]);
    expect(withoutFirstMain.divisions[0].facilities.map((facility) => facility.name)).toEqual(["North"]);
    expect(withoutFirstMain.divisions[1].facilities.map((facility) => facility.name)).toEqual(["Main"]);
    expect(withoutFirstMain.warehouses.map((warehouse) => warehouse.code)).toEqual(["EN0", "IE0"]);

    const withoutSecondDivision = filterAvailabilityGridByFacilities(
      model,
      new Set([getAvailabilityFacilityKey("02", "MAIN")]),
    );

    expect(withoutSecondDivision.divisions.map((division) => division.code)).toEqual(["01"]);
    expect(withoutSecondDivision.warehouses.map((warehouse) => warehouse.code)).toEqual(["EE0", "EN0"]);
  });

  it("filters warehouses by normalized code and prunes empty facility and division groups", () => {
    const model = buildAvailabilityGrid(
      [
        makeItem({ warehouseCode: "EE0", facility: "Main", divisionCode: "01" }),
        makeItem({ warehouseCode: "EE1", facility: "Main", divisionCode: "01" }),
        makeItem({ warehouseCode: "IE1", facility: "Dublin", divisionCode: "02" }),
      ],
      "CH0100",
    );

    const withoutLocalRepair = filterAvailabilityGridByWarehouses(
      model,
      new Set([getAvailabilityWarehouseKey(" ee1 ")]),
    );

    expect(withoutLocalRepair.divisions.map((division) => division.code)).toEqual(["01", "02"]);
    expect(withoutLocalRepair.divisions[0].facilities[0].warehouses.map((warehouse) => warehouse.code)).toEqual([
      "EE0",
    ]);
    expect(withoutLocalRepair.warehouses.map((warehouse) => warehouse.code)).toEqual(["EE0", "IE1"]);

    const withoutSecondDivision = filterAvailabilityGridByWarehouses(
      model,
      new Set([getAvailabilityWarehouseKey("ie1")]),
    );

    expect(withoutSecondDivision.divisions.map((division) => division.code)).toEqual(["01"]);
    expect(withoutSecondDivision.warehouses.map((warehouse) => warehouse.code)).toEqual(["EE0", "EE1"]);
  });

  it("composes facility and warehouse filters without discarding a hidden child warehouse", () => {
    const model = buildAvailabilityGrid(
      [
        makeItem({ warehouseCode: "EE0", facility: "East" }),
        makeItem({ warehouseCode: "EN0", facility: "North" }),
        makeItem({ warehouseCode: "EN1", facility: "North" }),
      ],
      "CH0100",
    );
    const hiddenWarehouses = new Set([getAvailabilityWarehouseKey("EN1")]);

    const withoutNorth = filterAvailabilityGridByWarehouses(
      filterAvailabilityGridByFacilities(model, new Set([getAvailabilityFacilityKey("01", "North")])),
      hiddenWarehouses,
    );
    expect(withoutNorth.warehouses.map((warehouse) => warehouse.code)).toEqual(["EE0"]);

    const withNorthRestored = filterAvailabilityGridByWarehouses(model, hiddenWarehouses);
    expect(withNorthRestored.warehouses.map((warehouse) => warehouse.code)).toEqual(["EE0", "EN0"]);
  });

  it("keeps only items with physical stock in the visible warehouses", () => {
    const model = buildAvailabilityGrid(
      [
        makeItem({ itemNumber: "CH0100LOCAL", warehouseCode: "EE0", available: 0, count: 1 }),
        makeItem({ itemNumber: "CH0100LOCAL", warehouseCode: "IE0", available: 0, count: 0 }),
        makeItem({ itemNumber: "CH0100REMOTE", warehouseCode: "EE0", available: 0, count: 0 }),
        makeItem({ itemNumber: "CH0100REMOTE", warehouseCode: "IE0", available: 0, count: 2 }),
      ],
      "CH0100",
    );

    const localOnly = filterAvailabilityGridByStock(
      filterAvailabilityGridByWarehouses(model, new Set([getAvailabilityWarehouseKey("IE0")])),
    );
    expect(localOnly.items.map((item) => item.itemNumber)).toEqual(["CH0100LOCAL"]);

    const allWarehouses = filterAvailabilityGridByStock(model);
    expect(allWarehouses.items.map((item) => item.itemNumber)).toEqual(["CH0100LOCAL", "CH0100REMOTE"]);
  });

  it("does not filter catalogue items before any warehouse columns are displayed", () => {
    const model = buildAvailabilityGrid([makeItem({ count: 0 })], "CH0100");
    const withoutWarehouses = filterAvailabilityGridByWarehouses(
      model,
      new Set(model.warehouses.map((warehouse) => getAvailabilityWarehouseKey(warehouse.code))),
    );

    expect(filterAvailabilityGridByStock(withoutWarehouses).items).toHaveLength(1);
  });

  it("groups facility codes without treating casing or surrounding whitespace as identity", () => {
    const model = buildAvailabilityGrid(
      [makeItem({ warehouseCode: "EE0", facility: " UKC " }), makeItem({ warehouseCode: "EN0", facility: "ukc" })],
      "CH0100",
    );

    expect(model.divisions[0].facilities).toHaveLength(1);
    expect(model.divisions[0].facilities[0].name).toBe("UKC");
    expect(model.divisions[0].facilities[0].warehouses.map((warehouse) => warehouse.code)).toEqual(["EE0", "EN0"]);
  });

  it("uses a stable facility identity when hierarchy metadata is blank", () => {
    const blankFacilityKey = getAvailabilityFacilityKey(" 01 ", "");

    expect(blankFacilityKey).toBe(getAvailabilityFacilityKey("01", "   "));
    expect(blankFacilityKey).toBe(getAvailabilityFacilityKey("01"));
    expect(blankFacilityKey).not.toBe(getAvailabilityFacilityKey("01", "Unknown facility"));
  });
});
