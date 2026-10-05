import { describe, expect, it } from "vitest";
import { ASSET_COLUMN_CATALOG, ASSET_TIMELINE_COLUMN_CATALOG } from "../pages/assets/assetColumnCatalog";
import {
  fitTableColumns,
  limitVisibleTableColumns,
  getTableColumnPrintWidth,
  getTableMinimumWidth,
  moveTableColumn,
  moveTableColumnToVisibleIndex,
  repairTableColumnLayout,
  setTableColumnVisibility,
  setTableColumnWidth,
  visibleTableColumns,
} from "./tableColumnLayout";

describe("table column layout", () => {
  it("limits optional timeline columns while retaining required identity columns", () => {
    const layout = repairTableColumnLayout(
      ASSET_TIMELINE_COLUMN_CATALOG,
      ASSET_TIMELINE_COLUMN_CATALOG.map((column) => ({
        key: column.key,
        visible: true,
        width: column.defaultWidth,
      })),
    );

    const limited = limitVisibleTableColumns(ASSET_TIMELINE_COLUMN_CATALOG, layout, 4);

    expect(visibleTableColumns(limited).map((column) => column.key)).toEqual([
      "status",
      "id",
      "itemNumber",
      "description",
    ]);
  });
  it("repairs unknown, duplicate, missing, invalid and out-of-range entries individually", () => {
    const layout = repairTableColumnLayout(ASSET_COLUMN_CATALOG, [
      { key: "description", visible: false, width: 9999 },
      { key: "future", visible: true, width: 200 },
      { key: "description", visible: true, width: 200 },
      { key: "id", visible: false, width: 1 },
      { key: "status", visible: true, width: Number.NaN },
    ]);

    expect(layout.slice(0, 3)).toEqual([
      { key: "status", visible: true, width: 96 },
      { key: "id", visible: true, width: 84 },
      { key: "description", visible: false, width: 480 },
    ]);
    expect(layout).toHaveLength(ASSET_COLUMN_CATALOG.length);
    expect(new Set(layout.map((column) => column.key)).size).toBe(layout.length);
  });

  it("keeps fixed identity columns in place while optional visible columns move", () => {
    const layout = repairTableColumnLayout(ASSET_COLUMN_CATALOG);
    const moved = moveTableColumn(ASSET_COLUMN_CATALOG, layout, "description", "left");
    expect(
      visibleTableColumns(moved)
        .slice(0, 4)
        .map((column) => column.key),
    ).toEqual(["status", "id", "description", "itemNumber"]);
    expect(moveTableColumn(ASSET_COLUMN_CATALOG, moved, "id", "right")).toEqual(moved);
  });

  it("protects required visibility and clamps keyboard or pointer width changes", () => {
    const layout = repairTableColumnLayout(ASSET_COLUMN_CATALOG);
    expect(setTableColumnVisibility(ASSET_COLUMN_CATALOG, layout, "id", false)).toEqual(layout);
    expect(
      setTableColumnWidth(ASSET_COLUMN_CATALOG, layout, "description", 40).find((c) => c.key === "description")?.width,
    ).toBe(120);
    expect(
      setTableColumnWidth(ASSET_COLUMN_CATALOG, layout, "description", 900).find((c) => c.key === "description")?.width,
    ).toBe(480);
  });

  it("converts pixel layout proportions to deterministic print percentages", () => {
    expect(getTableColumnPrintWidth(331, 1324)).toBe("25.000000%");
    expect(getTableColumnPrintWidth(993, 1324)).toBe("75.000000%");
    expect(getTableColumnPrintWidth(0, 1324)).toBe("auto");
  });

  it("moves a visible column directly while preserving hidden positions and the locked prefix", () => {
    const layout = setTableColumnVisibility(
      ASSET_COLUMN_CATALOG,
      repairTableColumnLayout(ASSET_COLUMN_CATALOG),
      "warehouse",
      false,
    );
    const moved = moveTableColumnToVisibleIndex(ASSET_COLUMN_CATALOG, layout, "description", 8);

    expect(visibleTableColumns(moved).map((column) => column.key)).toEqual([
      "status",
      "id",
      "itemNumber",
      "division",
      "customerName",
      "deliveryDate",
      "agreementLineValidFromDate",
      "agreementLineValidToDate",
      "description",
      "terminationDate",
      "collectionDate",
      "daysOffHire",
    ]);
    expect(moved.findIndex((column) => column.key === "warehouse")).toBe(
      layout.findIndex((column) => column.key === "warehouse"),
    );
    expect(moveTableColumnToVisibleIndex(ASSET_COLUMN_CATALOG, moved, "id", 5)).toEqual(moved);
    expect(
      visibleTableColumns(moveTableColumnToVisibleIndex(ASSET_COLUMN_CATALOG, moved, "description", 0))[2].key,
    ).toBe("description");
  });

  it("grows descriptive columns more than compact operational columns", () => {
    const layout = repairTableColumnLayout(ASSET_COLUMN_CATALOG);
    const preferredWidth = visibleTableColumns(layout).reduce((total, column) => total + column.width, 0);
    const fitted = fitTableColumns(ASSET_COLUMN_CATALOG, layout, preferredWidth + 260);
    const widthByKey = new Map(fitted.map((column) => [column.key, column.width]));

    expect(fitted.reduce((total, column) => total + column.width, 0)).toBe(preferredWidth + 260);
    expect(widthByKey.get("description")! - 160).toBeGreaterThan(widthByKey.get("division")! - 55);
    expect(widthByKey.get("customerName")! - 150).toBeGreaterThan(widthByKey.get("warehouse")! - 75);
  });

  it("shrinks proportionally to preferred widths without crossing column minimums", () => {
    const layout = repairTableColumnLayout(ASSET_COLUMN_CATALOG);
    const minimumWidth = getTableMinimumWidth(ASSET_COLUMN_CATALOG, layout);
    const fitted = fitTableColumns(ASSET_COLUMN_CATALOG, layout, minimumWidth + 100);

    expect(fitted.reduce((total, column) => total + column.width, 0)).toBe(minimumWidth + 100);
    fitted.forEach((column) => {
      const definition = ASSET_COLUMN_CATALOG.find((candidate) => candidate.key === column.key)!;
      expect(column.width).toBeGreaterThanOrEqual(definition.minWidth);
      expect(column.width).toBeLessThanOrEqual(layout.find((candidate) => candidate.key === column.key)!.width);
    });
  });

  it("uses minimum widths and leaves overflow to the table when the viewport is too narrow", () => {
    const layout = repairTableColumnLayout(ASSET_COLUMN_CATALOG);
    const fitted = fitTableColumns(ASSET_COLUMN_CATALOG, layout, 320);

    expect(fitted.reduce((total, column) => total + column.width, 0)).toBe(
      getTableMinimumWidth(ASSET_COLUMN_CATALOG, layout),
    );
  });

  it("ignores hidden columns when fitting and never exceeds maximum widths", () => {
    const layout = setTableColumnVisibility(
      ASSET_COLUMN_CATALOG,
      repairTableColumnLayout(ASSET_COLUMN_CATALOG),
      "description",
      false,
    );
    const fitted = fitTableColumns(ASSET_COLUMN_CATALOG, layout, 100_000);

    expect(fitted.some((column) => column.key === "description")).toBe(false);
    fitted.forEach((column) => {
      const definition = ASSET_COLUMN_CATALOG.find((candidate) => candidate.key === column.key)!;
      expect(column.width).toBeLessThanOrEqual(definition.maxWidth);
    });
  });
});
