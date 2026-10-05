import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { TableColumnsMenu } from "../../components/common";
import i18n from "../../i18n";
import {
  repairTableColumnLayout,
  setTableColumnVisibility,
  type TableColumnLayoutItem,
} from "../../lib/tableColumnLayout";
import type { Asset } from "../../types";
import { ASSET_COLUMN_CATALOG, type AssetColumnKey } from "./assetColumnCatalog";
import { AssetsTable, type AssetsTableProps } from "./AssetsTable";
import styles from "./AssetsPage.module.css";

const statusOptions = [
  "Available",
  "OnHire",
  "Service",
  "Repair",
  "Collection",
  "In Transit",
  "RemovedStock",
  "Scrap",
  "Sold",
];

function createAsset(id: string, overrides: Partial<Asset> = {}): Asset {
  return {
    id,
    individualItemNumber: `Individual ${id}`,
    itemNumber: `Item ${id}`,
    status: "Available",
    warehouse: "GLA",
    division: "01",
    facility: "Main yard",
    estimatedReadyDate: "2026-08-12T12:00:00.000Z",
    telemetryStatus: null,
    agreementNumber: `Agreement ${id}`,
    customerName: `Customer ${id}`,
    deliveryDate: "2026-08-02T12:00:00.000Z",
    agreementLineValidFromDate: "2026-08-03T12:00:00.000Z",
    agreementLineValidToDate: "2026-08-30T12:00:00.000Z",
    description: `Description ${id}`,
    warehouseLocation: "Bay 1",
    collectionDate: "2026-09-01T12:00:00.000Z",
    terminationDate: "2026-08-29T12:00:00.000Z",
    daysOffHire: 12,
    ...overrides,
  };
}

function createProps(overrides: Partial<AssetsTableProps> = {}): AssetsTableProps {
  return {
    assets: [createAsset("ASSET-1")],
    columnFilters: {},
    columnLayout: repairTableColumnLayout(ASSET_COLUMN_CATALOG),
    currentPage: 1,
    divisionFilter: "01",
    divisionOptions: [
      { label: "01", value: "01" },
      { label: "02", value: "02" },
    ],
    getAssetProfilePath: (assetId) => `/assets/${encodeURIComponent(assetId)}`,
    selectedAssetIds: new Set(),
    selectedAssetsOnPageCount: 0,
    pageSize: 50,
    showAllDivisionOption: true,
    sortDirection: "asc",
    sortField: "id",
    statusFilter: "",
    statusOptions,
    totalPages: 1,
    warehouseOptions: [
      { label: "GLA", value: "GLA" },
      { label: "MAN", value: "MAN" },
    ],
    onColumnFilterChange: vi.fn(),
    onColumnLayoutChange: vi.fn(),
    onDateColumnFilterChange: vi.fn(),
    onDivisionFilterChange: vi.fn(),
    onNextPage: vi.fn(),
    onPageSizeChange: vi.fn(),
    onPreviousPage: vi.fn(),
    onSelectAll: vi.fn(),
    onSelectAsset: vi.fn(),
    onSort: vi.fn(),
    onStatusFilterChange: vi.fn(),
    ...overrides,
  };
}

function renderTable(props: AssetsTableProps) {
  return render(
    <MemoryRouter>
      <AssetsTable {...props} />
    </MemoryRouter>,
  );
}

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

describe("AssetsTable", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage("en");
  });

  afterEach(cleanup);

  it("offers every legacy business column and keeps additions optional", () => {
    const legacyKeys: AssetColumnKey[] = [
      "warehouseName",
      "agreementNumber",
      "customerNumber",
      "facility",
      "warehouseLocation",
      "estimatedReadyDate",
      "productGroup",
      "productCategory",
      "runHours",
      "size",
      "telemetryStatus",
      "remark",
    ];

    for (const key of legacyKeys) {
      expect(ASSET_COLUMN_CATALOG.find((column) => column.key === key)).toMatchObject({ defaultVisible: false });
    }
  });

  it("shows a note count only when an Asset has notes", () => {
    renderTable(
      createProps({ assets: [createAsset("ASSET-1", { noteCount: 3 }), createAsset("ASSET-2", { noteCount: 0 })] }),
    );

    expect(screen.getByLabelText("3 notes")).toHaveAttribute("title", "3 notes");
    expect(screen.queryByLabelText("0 notes")).not.toBeInTheDocument();
  });

  it("preserves every known status label plus raw and missing unknown values", () => {
    const statuses: Array<[string | null, string]> = [
      ["Available", "Available"],
      ["OnHire", "On Hire"],
      ["Service", "Service"],
      ["Repair", "Repair"],
      ["Assess", "Assess"],
      ["Collection", "Collection"],
      ["In Transit", "In Transit"],
      ["RemovedStock", "RemovedStock"],
      ["Scrap", "Scrap"],
      ["Sold", "Sold"],
      ["Unexpected", "Unexpected"],
      [null, "Unknown"],
    ];
    const assets = statuses.map(([status], index) => createAsset(`status-${index}`, { status }));

    renderTable(createProps({ assets }));

    statuses.forEach(([, label], index) => {
      const row = screen.getByRole("link", { name: `status-${index}` }).closest("tr");
      expect(row).not.toBeNull();
      expect(within(row!).getByText(label)).toBeInTheDocument();
      expect(within(row!).queryByRole("img")).not.toBeInTheDocument();
    });
  });

  it("fits visible columns to the measured table viewport using catalogue priorities", async () => {
    const clientWidth = vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(1600);
    const offsetWidth = vi.spyOn(HTMLElement.prototype, "offsetWidth", "get").mockReturnValue(1616);

    try {
      const { container } = renderTable(createProps());

      await waitFor(() => expect(container.querySelector("table")?.style.width).toBe("1600px"));
      const firstColgroup = container.querySelector("colgroup")!;
      const descriptionWidth = Number.parseFloat(
        (firstColgroup.querySelector('[data-column-key="description"]') as HTMLElement).style.width,
      );
      const divisionWidth = Number.parseFloat(
        (firstColgroup.querySelector('[data-column-key="division"]') as HTMLElement).style.width,
      );
      expect(descriptionWidth - 160).toBeGreaterThan(divisionWidth - 55);
      expect((container.querySelector(`.${styles.tableSurface}`) as HTMLElement).style.minWidth).toBe("1090px");
    } finally {
      clientWidth.mockRestore();
      offsetWidth.mockRestore();
    }
  });

  it("keeps header, filters, rows and both colgroups aligned after reorder and keyboard/pointer resize", async () => {
    const user = userEvent.setup();
    const onSort = vi.fn();
    const onColumnFilterChange = vi.fn();

    function SpikeHarness() {
      const [layout, setLayout] = useState<TableColumnLayoutItem<AssetColumnKey>[]>(() =>
        repairTableColumnLayout(ASSET_COLUMN_CATALOG),
      );
      return (
        <>
          <TableColumnsMenu
            catalogue={ASSET_COLUMN_CATALOG}
            layout={layout}
            onChange={setLayout}
            onVisibilityChange={(key, visible) =>
              setLayout((current) => setTableColumnVisibility(ASSET_COLUMN_CATALOG, current, key, visible))
            }
          />
          <AssetsTable
            {...createProps({ onSort, onColumnFilterChange })}
            columnLayout={layout}
            onColumnLayoutChange={setLayout}
          />
        </>
      );
    }

    const { container } = render(
      <MemoryRouter>
        <SpikeHarness />
      </MemoryRouter>,
    );
    await user.click(screen.getByRole("button", { name: "Columns, 13 visible" }));
    const dialog = screen.getByRole("dialog", { name: "Configure columns" });
    const dataTransfer = { dropEffect: "none", effectAllowed: "none", setData: vi.fn() };
    fireEvent.dragStart(within(dialog).getByTitle("Drag to reorder Description"), { dataTransfer });
    const itemNumberRow = within(dialog).getByRole("checkbox", { name: "Item #" }).closest("li")!;
    fireEvent.dragEnter(itemNumberRow, { dataTransfer });
    fireEvent.dragOver(itemNumberRow, { dataTransfer });
    fireEvent.drop(itemNumberRow, { dataTransfer });
    expect(screen.getByText("Moved Description to position 3")).toBeInTheDocument();

    expect(
      screen
        .getAllByRole("button", { name: /^Sort by / })
        .slice(0, 5)
        .map((button) => button.textContent),
    ).toEqual([
      expect.stringContaining("Status"),
      expect.stringContaining("Asset ID"),
      expect.stringContaining("Description"),
      expect.stringContaining("Item #"),
      expect.stringContaining("WHS"),
    ]);
    const row = screen.getByRole("link", { name: "ASSET-1" }).closest("tr")!;
    expect(
      Array.from(row.cells)
        .slice(1, 6)
        .map((cell) => cell.textContent),
    ).toEqual(["Available", "ASSET-1", "Description ASSET-1", "Item ASSET-1", "GLA"]);

    const resize = screen.getByRole("separator", { name: "Resize Description column" });
    resize.focus();
    await user.keyboard("{ArrowRight}");
    expect(resize).toHaveAttribute("aria-valuenow", "165");
    fireEvent.pointerDown(resize, { clientX: 100, pointerId: 1 });
    fireEvent.pointerMove(resize, { clientX: 120, pointerId: 1 });
    fireEvent.pointerUp(resize, { clientX: 120, pointerId: 1 });
    expect(screen.getByRole("separator", { name: "Resize Description column" })).toHaveAttribute(
      "aria-valuenow",
      "185",
    );

    const colgroups = container.querySelectorAll("colgroup");
    expect(colgroups).toHaveLength(2);
    expect(colgroups[0].innerHTML).toBe(colgroups[1].innerHTML);
    const printWidths = Array.from(colgroups[0].querySelectorAll("col[data-column-key]"), (column) =>
      Number.parseFloat((column as HTMLElement).style.getPropertyValue("--print-column-width")),
    );
    expect(printWidths.reduce((total, width) => total + width, 0)).toBeCloseTo(100, 4);
    fireEvent.click(screen.getByRole("button", { name: /^Sort by Description/ }));
    fireEvent.change(screen.getByLabelText("Filter Description"), { target: { value: "boom" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Description"), { key: "Enter" });
    expect(onSort).toHaveBeenCalledWith("description");
    expect(onColumnFilterChange).toHaveBeenCalledWith("description", ["boom"]);
    expect(screen.getByRole("link", { name: "ASSET-1" })).toHaveAttribute("href", "/assets/ASSET-1");
  });

  it("hides and restores optional columns, protects identity columns, and returns focus on Escape", async () => {
    const user = userEvent.setup();
    function Harness() {
      const [layout, setLayout] = useState<TableColumnLayoutItem<AssetColumnKey>[]>(() =>
        repairTableColumnLayout(ASSET_COLUMN_CATALOG),
      );
      return (
        <>
          <TableColumnsMenu
            catalogue={ASSET_COLUMN_CATALOG}
            layout={layout}
            onChange={setLayout}
            onVisibilityChange={(key, visible) =>
              setLayout((current) => setTableColumnVisibility(ASSET_COLUMN_CATALOG, current, key, visible))
            }
          />
          <AssetsTable {...createProps()} columnLayout={layout} onColumnLayoutChange={setLayout} />
        </>
      );
    }

    render(
      <MemoryRouter>
        <Harness />
      </MemoryRouter>,
    );
    const trigger = screen.getByRole("button", { name: "Columns, 13 visible" });
    await user.click(trigger);
    expect(screen.getByRole("dialog", { name: "Configure columns" })).toContainElement(
      document.activeElement as HTMLElement | null,
    );
    expect(screen.getByRole("checkbox", { name: /Asset ID/ })).toBeDisabled();
    await user.click(screen.getByRole("checkbox", { name: "Description" }));
    expect(screen.getByRole("heading", { name: "Available (13)" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Sort by Description/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Filter Description")).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Reset columns" }));
    expect(screen.getByRole("button", { name: /^Sort by Description/ })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Done" }));
    expect(screen.queryByRole("dialog", { name: "Configure columns" })).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
    await user.click(trigger);
    await user.keyboard("{Escape}");
    expect(screen.queryByRole("dialog", { name: "Configure columns" })).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });

  it("renders encoded profile links and one value per non-date data column", () => {
    const getAssetProfilePath = vi.fn((assetId: string) => `/assets/${encodeURIComponent(assetId)}`);
    const asset = createAsset("A/B C?", {
      warehouse: "MAN",
      warehouseLocation: "Bay 4",
      division: "02",
      customerName: "Acme Hire",
      agreementNumber: "AGR-42",
      itemNumber: "ITEM-42",
      description: "Excavator",
      daysOffHire: 27,
    });

    renderTable(createProps({ assets: [asset], getAssetProfilePath }));

    const link = screen.getByRole("link", { name: "A/B C?" });
    const row = link.closest("tr");
    expect(getAssetProfilePath).toHaveBeenCalledWith(asset.id);
    expect(link).toHaveAttribute("href", "/assets/A%2FB%20C%3F");
    expect(row).not.toBeNull();
    [
      "ITEM-42",
      "Excavator",
      "MAN",
      "02",
      "Acme Hire",
      "27",
      formatDate(asset.deliveryDate!),
      formatDate(asset.agreementLineValidFromDate!),
      formatDate(asset.agreementLineValidToDate!),
      formatDate(asset.terminationDate!),
      formatDate(asset.collectionDate!),
    ].forEach((value) => expect(within(row!).getByText(value)).toBeInTheDocument());
    expect(within(row!).queryByText("Bay 4")).not.toBeInTheDocument();
    expect(within(row!).queryByText("AGR-42")).not.toBeInTheDocument();
    expect(within(row!).queryByText("South Operations")).not.toBeInTheDocument();
  });

  it("forwards row checkbox changes without a details control", async () => {
    const user = userEvent.setup();
    const onSelectAsset = vi.fn();
    const asset = createAsset("ASSET-7");
    const props = createProps({
      assets: [asset],
      selectedAssetIds: new Set([asset.id]),
      selectedAssetsOnPageCount: 1,
      onSelectAsset,
    });
    renderTable(props);

    const row = screen.getByRole("link", { name: asset.id }).closest("tr");
    expect(row).not.toBeNull();
    const rowCheckbox = within(row!).getByRole("checkbox");
    expect(rowCheckbox).toBeChecked();

    await user.click(rowCheckbox);
    expect(onSelectAsset).toHaveBeenCalledWith(asset.id, false);
    expect(within(row!).queryByTitle("Show details")).not.toBeInTheDocument();
  });

  it("derives select-all from the current page and forwards both checked states", async () => {
    const user = userEvent.setup();
    const assets = [createAsset("PAGE-1"), createAsset("PAGE-2")];
    const onSelectAll = vi.fn();
    const selectedProps = createProps({
      assets,
      selectedAssetIds: new Set(["OFF-PAGE", "PAGE-1", "PAGE-2"]),
      selectedAssetsOnPageCount: 2,
      onSelectAll,
    });
    const { container, rerender } = renderTable(selectedProps);
    const getSelectAll = () => container.querySelector("thead input[type='checkbox']") as HTMLInputElement;

    expect(getSelectAll()).toBeChecked();
    await user.click(getSelectAll());
    expect(onSelectAll).toHaveBeenLastCalledWith(false);

    rerender(
      <MemoryRouter>
        <AssetsTable {...selectedProps} selectedAssetIds={new Set(["OFF-PAGE"])} selectedAssetsOnPageCount={0} />
      </MemoryRouter>,
    );
    expect(getSelectAll()).not.toBeChecked();
    await user.click(getSelectAll());
    expect(onSelectAll).toHaveBeenLastCalledWith(true);
  });

  it("renders every former detail date as a dedicated column and removes the detail-only values", () => {
    const asset = createAsset("DETAIL-1", {
      warehouseLocation: "Rack 9",
      facility: "Service depot",
      terminationDate: null,
      individualItemNumber: "IND-999",
    });

    renderTable(createProps({ assets: [asset] }));

    const row = screen.getByRole("link", { name: asset.id }).closest("tr");
    expect(row).not.toBeNull();
    [
      formatDate(asset.deliveryDate!),
      formatDate(asset.agreementLineValidFromDate!),
      formatDate(asset.agreementLineValidToDate!),
      formatDate(asset.collectionDate!),
      "—",
    ].forEach((value) => expect(within(row!).getByText(value)).toBeInTheDocument());
    expect(screen.queryByText("Warehouse Location")).not.toBeInTheDocument();
    expect(screen.queryByText("Facility")).not.toBeInTheDocument();
    expect(screen.queryByText("IND-999")).not.toBeInTheDocument();
  });

  it("maps every sort and column-filter control to its Asset field callback", () => {
    const onColumnFilterChange = vi.fn();
    const onDateColumnFilterChange = vi.fn();
    const onSort = vi.fn();
    const onStatusFilterChange = vi.fn();
    const onDivisionFilterChange = vi.fn();
    const props = createProps({
      columnFilters: { description: [] },
      onColumnFilterChange,
      onDateColumnFilterChange,
      onDivisionFilterChange,
      onSort,
      onStatusFilterChange,
      sortDirection: "desc",
      sortField: "warehouse",
    });
    const { rerender } = renderTable(props);

    const sortButtons = screen.getAllByRole("button", { name: /^Sort by / });
    expect(sortButtons.map((button) => button.getAttribute("aria-label"))).toEqual([
      "Sort by Status, currently not sorted",
      "Sort by Asset ID, currently not sorted",
      "Sort by Item #, currently not sorted",
      "Sort by Description, currently not sorted",
      "Sort by WHS, currently descending",
      "Sort by DIV, currently not sorted",
      "Sort by Customer, currently not sorted",
      "Sort by Delivery, currently not sorted",
      "Sort by Valid From, currently not sorted",
      "Sort by Valid To, currently not sorted",
      "Sort by Termination, currently not sorted",
      "Sort by Collection, currently not sorted",
      "Sort by DOH, currently not sorted",
    ]);
    sortButtons.forEach((button) => fireEvent.click(button));
    expect(onSort.mock.calls).toEqual([
      ["status"],
      ["id"],
      ["itemNumber"],
      ["description"],
      ["warehouse"],
      ["division"],
      ["customerName"],
      ["deliveryDate"],
      ["agreementLineValidFromDate"],
      ["agreementLineValidToDate"],
      ["terminationDate"],
      ["collectionDate"],
      ["daysOffHire"],
    ]);

    fireEvent.click(screen.getByRole("button", { name: "Filter Status: All" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Repair" }));
    fireEvent.change(screen.getByLabelText("Filter Asset ID"), { target: { value: "A-10" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Asset ID"), { key: "Enter" });
    fireEvent.change(screen.getByLabelText("Filter Item number"), { target: { value: "I-20" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Item number"), { key: "Enter" });
    fireEvent.change(screen.getByLabelText("Filter Description"), { target: { value: "lift" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Description"), { key: "Enter" });
    fireEvent.click(screen.getByRole("button", { name: "Filter Warehouse: All" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "MAN" }));
    fireEvent.click(screen.getByRole("button", { name: "Filter Division: 01" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "02" }));
    fireEvent.change(screen.getByLabelText("Filter Customer"), { target: { value: "Acme" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Customer"), { key: "Enter" });
    fireEvent.change(screen.getByLabelText("Filter Days off hire"), { target: { value: "12" } });
    expect(onColumnFilterChange.mock.calls).toEqual([
      ["id", ["A-10"]],
      ["itemNumber", ["I-20"]],
      ["description", ["lift"]],
      ["warehouse", ["MAN"]],
      ["customerName", ["Acme"]],
      ["daysOffHire", "12"],
    ]);
    expect(onStatusFilterChange).toHaveBeenCalledWith("Repair");
    expect(onDivisionFilterChange).toHaveBeenCalledWith("01,02");
    rerender(
      <MemoryRouter>
        <AssetsTable {...props} statusFilter="Available,Repair" />
      </MemoryRouter>,
    );
    expect(screen.getByRole("button", { name: "Filter Status: 2 selected" })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("More date options for Delivery Date"), { target: { value: "today" } });
    expect(onDateColumnFilterChange).toHaveBeenCalledWith("deliveryDate", { operator: "today" });
    expect(
      within(screen.getByRole("dialog", { name: "Options for Status" }))
        .getAllByRole("checkbox")
        .map((option) => option.parentElement?.textContent),
    ).toEqual(statusOptions);
  });

  it("keeps column filters available when the current filter has no matching assets", () => {
    renderTable(createProps({ assets: [], emptyStateMessage: "No results found" }));

    expect(screen.getByText("No results found")).toBeInTheDocument();
    expect(screen.getByLabelText("Filter Asset ID")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Filter Status: All" })).toBeInTheDocument();
  });

  it("preserves pagination states, callbacks, and print markers", async () => {
    const user = userEvent.setup();
    const onNextPage = vi.fn();
    const onPageSizeChange = vi.fn();
    const onPreviousPage = vi.fn();
    const props = createProps({ currentPage: 1, totalPages: 3, onNextPage, onPageSizeChange, onPreviousPage });
    const { container, rerender } = renderTable(props);

    expect(container.querySelector('[data-print-table="assets"]')).toBeInTheDocument();
    expect(container.querySelectorAll("table")).toHaveLength(2);
    expect(screen.getByRole("region", { name: "Asset rows" })).toHaveAttribute("tabindex", "0");
    expect(container.querySelector('[data-print-table="assets"] [data-print-hidden]')).toBeInTheDocument();
    expect(screen.getByText("Page 1 of 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "← Prev" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Next →" })).toBeEnabled();
    const pageSizeSelect = screen.getByRole("combobox", { name: "Rows per page" });
    expect(pageSizeSelect).toHaveValue("50");
    expect(
      within(pageSizeSelect)
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["50", "250", "500", "1000"]);
    await user.selectOptions(pageSizeSelect, "250");
    expect(onPageSizeChange).toHaveBeenCalledWith(250);

    await user.click(screen.getByRole("button", { name: "Next →" }));
    expect(onNextPage).toHaveBeenCalledOnce();
    expect(onPreviousPage).not.toHaveBeenCalled();

    rerender(
      <MemoryRouter>
        <AssetsTable {...props} currentPage={3} />
      </MemoryRouter>,
    );
    expect(screen.getByText("Page 3 of 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "← Prev" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Next →" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "← Prev" }));
    expect(onPreviousPage).toHaveBeenCalledOnce();

    rerender(
      <MemoryRouter>
        <AssetsTable {...props} totalPages={1} />
      </MemoryRouter>,
    );
    expect(screen.getByText("Page 1 of 1")).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Rows per page" })).toBeInTheDocument();
    expect(container.querySelector('[data-print-table="assets"] [data-print-hidden]')).toBeInTheDocument();
  });
});
