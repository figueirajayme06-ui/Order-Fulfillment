import { describe, expect, it } from "vitest";
import { type TableColumnLayoutItem } from "../lib/tableColumnLayout";
import {
  AGREEMENT_COLUMN_CATALOG,
  AGREEMENT_TIMELINE_COLUMN_CATALOG,
  type AgreementColumnKey,
} from "../pages/agreements/agreementColumnCatalog";
import { ASSET_COLUMN_CATALOG, ASSET_TIMELINE_COLUMN_CATALOG } from "../pages/assets/assetColumnCatalog";
import { parseAgreementsSavedViewState, parseAssetsSavedViewState } from "./savedViews";

function createSavedState(overrides: Record<string, unknown> = {}) {
  return {
    viewMode: "table",
    sortField: "agreementNumber",
    sortDirection: "asc",
    statusFilter: "1",
    columnFilters: { fulfilmentStatus: "3" },
    ...overrides,
  };
}

describe("parseAgreementsSavedViewState", () => {
  it("migrates a version-1 state to the default version-5 Agreement layouts", () => {
    const parsed = parseAgreementsSavedViewState(createSavedState({ stateVersion: 1 }));

    expect(parsed?.stateVersion).toBe(5);
    expect(parsed?.columns?.map((column) => column.key)).toEqual(AGREEMENT_COLUMN_CATALOG.map((column) => column.key));
    expect(parsed?.timelineColumns?.map((column) => column.key)).toEqual(
      AGREEMENT_TIMELINE_COLUMN_CATALOG.map((column) => column.key),
    );
  });

  it("migrates a version-2 Agreement state to the compact default version-4 layout", () => {
    const parsed = parseAgreementsSavedViewState(createSavedState({ stateVersion: 2 }));
    expect(parsed?.stateVersion).toBe(5);
    expect(parsed?.columns).toEqual(
      AGREEMENT_COLUMN_CATALOG.map((definition) => ({
        key: definition.key,
        visible: "required" in definition && definition.required ? true : definition.defaultVisible,
        width: definition.defaultWidth,
      })),
    );
    expect(parsed?.columns?.reduce((total, column) => total + column.width, 0)).toBe(
      AGREEMENT_COLUMN_CATALOG.reduce((total, definition) => total + definition.defaultWidth, 0),
    );
  });

  it("round-trips independent Agreement table and timeline layouts", () => {
    const columns: TableColumnLayoutItem<AgreementColumnKey>[] = AGREEMENT_COLUMN_CATALOG.map((definition) => ({
      key: definition.key,
      visible: "required" in definition && definition.required ? true : definition.defaultVisible,
      width: definition.defaultWidth,
    }));
    const opportunity = columns.find((column) => column.key === "opportunityName")!;
    opportunity.visible = false;
    opportunity.width = 211;
    const moved = columns.splice(columns.indexOf(opportunity), 1)[0];
    columns.push(moved);

    const timelineColumns = AGREEMENT_TIMELINE_COLUMN_CATALOG.map((definition) => ({
      key: definition.key,
      visible: "required" in definition && definition.required ? true : definition.defaultVisible,
      width: definition.defaultWidth,
    }));
    const parsed = parseAgreementsSavedViewState(createSavedState({ stateVersion: 4, columns, timelineColumns }));
    expect(parsed?.columns).toEqual(columns);
    expect(parsed?.timelineColumns).toEqual(timelineColumns);
  });

  it("preserves canonical raw fulfilment filters", () => {
    const parsed = parseAgreementsSavedViewState(createSavedState());

    expect(parsed?.statusFilter).toBe("1");
    expect(parsed?.columnFilters.fulfilmentStatus).toEqual(["3"]);
  });

  it("migrates legacy text filters and round-trips v5 timeline and pagination state", () => {
    const parsed = parseAgreementsSavedViewState(
      createSavedState({
        stateVersion: 4,
        columnFilters: { agreementNumber: " A-100 " },
        timelineColumnFilters: { customerOrOpportunity: ["North", "Priority"] },
        currentPage: 3,
        timelinePage: 2,
        pageSize: 250,
      }),
    );
    expect(parsed?.columnFilters.agreementNumber).toEqual(["A-100"]);
    expect(parsed?.timelineColumnFilters?.customerOrOpportunity).toEqual(["North", "Priority"]);
    expect(parsed).toMatchObject({ stateVersion: 5, currentPage: 3, timelinePage: 2, pageSize: 250 });
  });

  it("rejects unsupported future saved-state versions", () => {
    expect(parseAgreementsSavedViewState(createSavedState({ stateVersion: 6 }))).toBeNull();
  });

  it("clears unsupported fulfilment filter values", () => {
    const parsed = parseAgreementsSavedViewState(
      createSavedState({
        statusFilter: "2",
        columnFilters: { fulfilmentStatus: "2" },
      }),
    );

    expect(parsed?.statusFilter).toBe("");
    expect(parsed?.columnFilters.fulfilmentStatus).toBeUndefined();
  });

  it("restores the supported off-hire date sort", () => {
    const parsed = parseAgreementsSavedViewState(
      createSavedState({
        sortField: "offHireDate",
        sortDirection: "desc",
      }),
    );

    expect(parsed?.sortField).toBe("offHireDate");
    expect(parsed?.sortDirection).toBe("desc");
  });

  it.each([
    ["an unknown sort field", { sortField: "createdDate" }],
    ["an invalid sort direction", { sortDirection: "sideways" }],
  ])("rejects %s", (_description, overrides) => {
    expect(parseAgreementsSavedViewState(createSavedState(overrides))).toBeNull();
  });
});

describe("parseAssetsSavedViewState", () => {
  it("repairs persisted Asset columns without discarding valid entries", () => {
    const parsed = parseAssetsSavedViewState({
      viewMode: "table",
      sortField: "id",
      sortDirection: "asc",
      columns: [
        { key: "description", visible: false, width: 9999 },
        { key: "description", visible: true, width: 200 },
        { key: "futureField", visible: true, width: 100 },
        { key: "id", visible: false, width: 10 },
      ],
    });

    expect(parsed?.stateVersion).toBe(5);
    expect(parsed?.columns?.slice(0, 3)).toEqual([
      { key: "status", visible: true, width: 96 },
      { key: "id", visible: true, width: 84 },
      { key: "description", visible: false, width: 480 },
    ]);
    expect(parsed?.columns).toHaveLength(ASSET_COLUMN_CATALOG.length);
    expect(parsed?.timelineColumns).toHaveLength(ASSET_TIMELINE_COLUMN_CATALOG.length);
  });

  it.each([
    "deliveryDate",
    "agreementLineValidFromDate",
    "agreementLineValidToDate",
    "terminationDate",
    "collectionDate",
  ])("restores the promoted %s date sort", (sortField) => {
    const parsed = parseAssetsSavedViewState({
      viewMode: "table",
      sortField,
      sortDirection: "desc",
      columnFilters: { [sortField]: "2026-08-31" },
    });

    expect(parsed?.sortField).toBe(sortField);
    expect(parsed?.sortDirection).toBe("desc");
    expect(parsed?.columnFilters[sortField]).toBe("2026-08-31");
  });
});
