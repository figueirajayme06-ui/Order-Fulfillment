import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { FrappeGanttNavigationState, GanttTask } from "../../components/timeline/FrappeGantt";
import "../../i18n";
import { repairTableColumnLayout } from "../../lib/tableColumnLayout";
import type { Asset, AssetEvent } from "../../types";
import { ASSET_TIMELINE_COLUMN_CATALOG } from "./assetColumnCatalog";
import styles from "./AssetsPage.module.css";
import { AssetsTimeline, type AssetsTimelineProps } from "./AssetsTimeline";
import type { AssetTimelineRow } from "./assetsTimelineModel";

interface MockFrappeProps {
  tasks: GanttTask[];
  viewMode?: string;
  minimumDate?: string;
  readonlyDates?: boolean;
  hoveredTaskId?: string | null;
  onHoverTaskIdChange?: (taskId: string | null) => void;
  onNavigationStateChange?: (state: FrappeGanttNavigationState) => void;
}

interface MockFrappeNavigation {
  jumpToToday: () => void;
  panEarlier: () => void;
  panLater: () => void;
}

const SECOND_ROW_ID = "NO-EVENT::none";

const frappeMock = vi.hoisted(() => ({
  latestProps: null as unknown,
  jumpToToday: vi.fn(),
  panEarlier: vi.fn(),
  panLater: vi.fn(),
}));

vi.mock("../../components/timeline/FrappeGantt", async () => {
  const React = await import("react");

  const FrappeGantt = React.forwardRef<MockFrappeNavigation, MockFrappeProps>((props, ref) => {
    frappeMock.latestProps = props;
    React.useImperativeHandle(
      ref,
      () => ({
        jumpToToday: frappeMock.jumpToToday,
        panEarlier: frappeMock.panEarlier,
        panLater: frappeMock.panLater,
      }),
      [],
    );
    return (
      <div data-testid="frappe-gantt" data-hovered-task-id={props.hoveredTaskId ?? ""}>
        <div className="gantt">
          <div className="grid-row" />
        </div>
        {props.tasks.map((task) => (
          <span key={task.id} data-testid="frappe-task-id">
            {task.id}
          </span>
        ))}
        <button type="button" onClick={() => props.onHoverTaskIdChange?.("NO-EVENT::none")}>
          Chart hover no-event row
        </button>
        <button type="button" onClick={() => props.onHoverTaskIdChange?.(null)}>
          Chart clear hover
        </button>
        <button
          type="button"
          onClick={() => props.onNavigationStateChange?.({ canPanEarlier: true, canPanLater: true })}
        >
          Enable timeline navigation
        </button>
      </div>
    );
  });

  return { FrappeGantt };
});

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
    agreementNumber: null,
    customerName: null,
    deliveryDate: null,
    agreementLineValidFromDate: null,
    agreementLineValidToDate: null,
    description: null,
    warehouseLocation: null,
    collectionDate: null,
    terminationDate: null,
    daysOffHire: null,
    ...overrides,
  };
}

function createEvent(overrides: Partial<AssetEvent> = {}): AssetEvent {
  return {
    assetId: "A/B C?",
    eventType: "ONHIRE",
    startDate: "2026-08-01",
    endDate: "2026-08-31",
    title: "Planned hire",
    cssClass: "onhire_event",
    ...overrides,
  };
}

function createRow(asset: Asset, overrides: Partial<AssetTimelineRow> = {}): AssetTimelineRow {
  return {
    rowId: `${asset.id}::event-onhire::0`,
    asset,
    event: createEvent({ assetId: asset.id }),
    eventClass: "event-onhire",
    eventLabel: "On Hire",
    ...overrides,
  };
}

function createTask(row: AssetTimelineRow): GanttTask {
  return {
    id: row.rowId,
    name: row.event ? `${row.eventLabel}: ${row.asset.itemNumber}` : "",
    start: "2026-08-01",
    end: "2026-08-31",
    progress: row.event ? 100 : 0,
    custom_class: row.eventClass,
  };
}

function createProps(overrides: Partial<AssetsTimelineProps> = {}): AssetsTimelineProps {
  const eventRow = createRow(createAsset("A/B C?"));
  const noEventAsset = createAsset("NO-EVENT", { itemNumber: null, warehouse: null, warehouseLocation: null });
  const noEventRow = createRow(noEventAsset, {
    rowId: SECOND_ROW_ID,
    event: null,
    eventClass: "event-none",
    eventLabel: "",
  });
  const rows = [eventRow, noEventRow];

  return {
    columnFilters: {},
    columnLayout: repairTableColumnLayout(ASSET_TIMELINE_COLUMN_CATALOG),
    currentPage: 1,
    divisionFilter: "01",
    divisionOptions: [
      { label: "01", value: "01" },
      { label: "02", value: "02" },
    ],
    rangeStart: new Date(2026, 7, 1),
    rows,
    showAllDivisionOption: true,
    tasks: rows.map(createTask),
    totalPages: 1,
    warehouseOptions: [
      { label: "GLA", value: "GLA" },
      { label: "MAN", value: "MAN" },
    ],
    getAssetProfilePath: (assetId) => `/assets/${encodeURIComponent(assetId)}`,
    selectedAssetIds: new Set(),
    selectedAssetsOnPageCount: 0,
    sortDirection: "asc",
    sortField: "id",
    statusFilter: "",
    statusOptions: ["Available", "OnHire", "Service"],
    onColumnFilterChange: vi.fn(),
    onColumnLayoutChange: vi.fn(),
    onDivisionFilterChange: vi.fn(),
    onNextPage: vi.fn(),
    onPreviousPage: vi.fn(),
    onSelectAll: vi.fn(),
    onSelectAsset: vi.fn(),
    onSort: vi.fn(),
    onStatusFilterChange: vi.fn(),
    onTimelinePeriodApply: vi.fn(),
    ...overrides,
  };
}

function renderTimeline(props: AssetsTimelineProps) {
  return render(
    <MemoryRouter>
      <AssetsTimeline {...props} />
    </MemoryRouter>,
  );
}

function getLatestFrappeProps(): MockFrappeProps {
  return frappeMock.latestProps as MockFrappeProps;
}

function createRect(top: number, bottom: number): DOMRect {
  return {
    x: 0,
    y: top,
    width: 0,
    height: bottom - top,
    top,
    right: 0,
    bottom,
    left: 0,
    toJSON: () => ({}),
  };
}

describe("AssetsTimeline", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    frappeMock.latestProps = null;
    window.localStorage.clear();
    vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
      callback(0);
      return 1;
    });
    vi.stubGlobal("cancelAnimationFrame", vi.fn());
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("preserves the legend, structural no-event row, task ids, encoded links, and print marker", () => {
    const getAssetProfilePath = vi.fn((assetId: string) => `/assets/${encodeURIComponent(assetId)}`);
    const props = createProps({ getAssetProfilePath });
    const { container } = renderTimeline(props);

    expect(screen.getByText("Event key")).toBeInTheDocument();
    const legend = container.querySelector(`.${styles.legend}`);
    expect(legend).not.toBeNull();
    expect(
      Array.from(legend!.querySelectorAll(`.${styles.legendItem}`)).map((item) => item.textContent?.trim()),
    ).toEqual(["Ringfence", "On Hire", "Reserved", "Service", "Repair", "Collection", "Transport", "On Hold"]);
    expect(screen.getAllByTestId("frappe-task-id").map((node) => node.textContent)).toEqual([
      "A/B C?::event-onhire::0",
      SECOND_ROW_ID,
    ]);
    expect(getLatestFrappeProps()).toMatchObject({
      tasks: props.tasks,
      viewMode: "Day",
      minimumDate: "2026-08-01",
      readonlyDates: true,
    });

    const encodedLink = screen.getByRole("link", { name: "A/B C?" });
    expect(encodedLink).toHaveAttribute("href", "/assets/A%2FB%20C%3F");
    expect(getAssetProfilePath).toHaveBeenCalledWith("A/B C?");
    const noEventRow = screen.getByRole("link", { name: "NO-EVENT" }).closest("tr");
    expect(noEventRow).not.toBeNull();
    expect(noEventRow).toHaveAttribute("data-timeline-row", "true");
    expect(within(noEventRow!).getAllByText("—")).toHaveLength(2);
    expect(screen.getByRole("columnheader", { name: "WHS" })).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Location" })).not.toBeInTheDocument();
    expect(container.querySelector("[data-print-timeline]")).toBeInTheDocument();
  });

  it("stages a period until it is applied, then sends both dates together", async () => {
    const user = userEvent.setup();
    const onTimelinePeriodApply = vi.fn();
    renderTimeline(createProps({ onTimelinePeriodApply }));

    fireEvent.change(screen.getByLabelText("From Date"), { target: { value: "2026-09-01" } });
    fireEvent.change(screen.getByLabelText("To Date"), { target: { value: "2026-09-30" } });
    expect(onTimelinePeriodApply).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Apply period" }));
    expect(onTimelinePeriodApply).toHaveBeenCalledWith("2026-09-01", "2026-09-30");
  });

  it("sorts timeline rows from a context-column header", async () => {
    const user = userEvent.setup();
    const onSort = vi.fn();

    renderTimeline(createProps({ onSort }));

    await user.click(screen.getByRole("button", { name: "Sort by Asset ID, currently ascending" }));

    expect(onSort).toHaveBeenCalledWith("id");
  });

  it("renders optional Asset context fields from the supplied layout", () => {
    const eventRow = createRow(createAsset("ASSET-1", { description: "Quiet generator" }));
    const layout = repairTableColumnLayout(ASSET_TIMELINE_COLUMN_CATALOG).map((column) => {
      if (column.key === "warehouse") return { ...column, visible: false };
      if (column.key === "description") return { ...column, visible: true };
      return column;
    });
    renderTimeline(createProps({ columnLayout: layout, rows: [eventRow], tasks: [createTask(eventRow)] }));

    expect(screen.getByRole("columnheader", { name: "Description" })).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "WHS" })).not.toBeInTheDocument();
    expect(screen.getByText("Quiet generator")).toBeInTheDocument();
  });

  it("resizes the Asset context pane with the keyboard and persists the local width", () => {
    const { container } = renderTimeline(createProps());
    const separator = screen.getByRole("separator", { name: "Resize asset details" });

    fireEvent.keyDown(separator, { key: "ArrowRight" });

    expect(separator).toHaveAttribute("aria-valuenow", "444");
    expect(container.querySelector(`.${styles.timelineTable}`)).toHaveStyle({ width: "444px" });
    expect(window.localStorage.getItem("assets.timelineTableWidth.v1")).toBe("444");
  });

  it("synchronizes exact row ids with Frappe hover in both directions", async () => {
    const user = userEvent.setup();
    renderTimeline(createProps());

    const eventRow = screen.getByRole("link", { name: "A/B C?" }).closest("tr");
    const noEventRow = screen.getByRole("link", { name: "NO-EVENT" }).closest("tr");
    expect(eventRow).not.toBeNull();
    expect(noEventRow).not.toBeNull();

    fireEvent.mouseEnter(eventRow!);
    expect(screen.getByTestId("frappe-gantt")).toHaveAttribute("data-hovered-task-id", "A/B C?::event-onhire::0");
    expect(eventRow).toHaveClass(styles.timelineRowHovered);
    await user.click(screen.getByRole("button", { name: "Chart hover no-event row" }));
    expect(noEventRow).toHaveClass(styles.timelineRowHovered);
    fireEvent.mouseLeave(eventRow!);
    expect(screen.getByTestId("frappe-gantt")).toHaveAttribute("data-hovered-task-id", SECOND_ROW_ID);
    expect(noEventRow).toHaveClass(styles.timelineRowHovered);
    await user.click(screen.getByRole("button", { name: "Chart clear hover" }));
    expect(noEventRow).not.toHaveClass(styles.timelineRowHovered);
  });

  it("does not render a duplicate visible-range label", () => {
    renderTimeline(createProps());

    expect(screen.queryByText("Aug to Oct 2026")).not.toBeInTheDocument();
    expect(screen.queryByText("Nov 2026 to Jan 2027")).not.toBeInTheDocument();
  });

  it("jumps to today when the Asset timeline opens", () => {
    renderTimeline(createProps());

    expect(frappeMock.jumpToToday).toHaveBeenCalledOnce();
  });

  it("renders filters for every left-side data column and forwards their values", () => {
    const onColumnFilterChange = vi.fn();
    const onDivisionFilterChange = vi.fn();
    const onStatusFilterChange = vi.fn();
    const columnLayout = repairTableColumnLayout(ASSET_TIMELINE_COLUMN_CATALOG).map((column) =>
      column.key === "division" ? { ...column, visible: true } : column,
    );
    const props = createProps({
      columnFilters: { id: ["A/B"] },
      columnLayout,
      onColumnFilterChange,
      onDivisionFilterChange,
      onStatusFilterChange,
    });
    const { rerender } = renderTimeline(props);

    expect(screen.getByText("A/B")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Filter Status: All" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Service" }));
    fireEvent.change(screen.getByLabelText("Filter Asset ID"), { target: { value: "ASSET-1" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Asset ID"), { key: "Enter" });
    fireEvent.change(screen.getByLabelText("Filter Item #"), { target: { value: "ITEM-2" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Item #"), { key: "Enter" });
    fireEvent.click(screen.getByRole("button", { name: "Filter WHS: All" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "MAN" }));
    fireEvent.click(screen.getByRole("button", { name: "Filter DIV: 01" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "02" }));

    expect(onColumnFilterChange.mock.calls).toEqual([
      ["id", ["A/B", "ASSET-1"]],
      ["itemNumber", ["ITEM-2"]],
      ["warehouse", ["MAN"]],
    ]);
    expect(onDivisionFilterChange).toHaveBeenCalledWith("01,02");
    expect(onStatusFilterChange).toHaveBeenCalledWith("Service");
    rerender(
      <MemoryRouter>
        <AssetsTimeline {...props} statusFilter="Available,Service" />
      </MemoryRouter>,
    );
    expect(screen.getByRole("button", { name: "Filter Status: 2 selected" })).toBeInTheDocument();
  });

  it("aligns Asset rows with the first Frappe grid row", () => {
    vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(function (this: HTMLElement) {
      if (this.tagName === "THEAD") {
        return createRect(0, 100);
      }
      if (this.classList.contains("grid-row")) {
        return createRect(148, 168);
      }
      return createRect(0, 0);
    });

    const { container } = renderTimeline(createProps());
    const spacer = container.querySelector(`.${styles.timelineHeaderSpacer}`);

    expect(spacer).not.toBeNull();
    const spacerCell = within(spacer as HTMLElement).getByRole("cell", { hidden: true });
    expect(spacerCell).toHaveAttribute("colspan", "5");
    expect(spacerCell).toHaveStyle({ height: "48px" });
  });

  it("preserves pagination callbacks, disabled states, and its print marker", async () => {
    const user = userEvent.setup();
    const onNextPage = vi.fn();
    const onPreviousPage = vi.fn();
    const props = createProps({ totalPages: 3, onNextPage, onPreviousPage });
    const { container, rerender } = renderTimeline(props);

    expect(screen.getByText("Page 1 of 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Prev/ })).toBeDisabled();
    expect(screen.getByRole("button", { name: /Next/ })).toBeEnabled();
    expect(container.querySelector("[data-print-timeline]")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /Next/ }));
    expect(onNextPage).toHaveBeenCalledOnce();
    expect(onPreviousPage).not.toHaveBeenCalled();

    rerender(
      <MemoryRouter>
        <AssetsTimeline {...props} currentPage={3} />
      </MemoryRouter>,
    );
    expect(screen.getByText("Page 3 of 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Prev/ })).toBeEnabled();
    expect(screen.getByRole("button", { name: /Next/ })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: /Prev/ }));
    expect(onPreviousPage).toHaveBeenCalledOnce();

    rerender(
      <MemoryRouter>
        <AssetsTimeline {...props} totalPages={1} />
      </MemoryRouter>,
    );
    expect(screen.queryByText(/Page 3 of 1/)).not.toBeInTheDocument();
  });

  it("keeps timeline filters available when they have no matching rows", () => {
    const props = createProps({ rows: [], tasks: [], totalPages: 1 });
    const { container } = renderTimeline(props);

    expect(screen.getByText("No assets match the timeline filters.")).toBeInTheDocument();
    expect(screen.getByText("Event key")).toBeInTheDocument();
    expect(screen.getByLabelText("Filter Asset ID")).toBeInTheDocument();
    expect(screen.getByTestId("frappe-gantt")).toBeInTheDocument();
    expect(container.querySelector("[data-print-timeline]")).toBeInTheDocument();
  });

  it("keeps the group separator class on every fifth timeline row", () => {
    const rows = Array.from({ length: 6 }, (_, index) => createRow(createAsset(`ASSET-${index + 1}`)));
    const { container } = renderTimeline(createProps({ rows, tasks: rows.map(createTask) }));
    const tableRows = Array.from(container.querySelectorAll("tbody tr[data-timeline-row='true']"));

    expect(tableRows).toHaveLength(6);
    expect(tableRows[3]).not.toHaveClass(styles.groupRow);
    expect(tableRows[4]).toHaveClass(styles.groupRow);
    expect(tableRows[5]).not.toHaveClass(styles.groupRow);
  });

  it("selects individual timeline assets and all unique assets on the current page", async () => {
    const user = userEvent.setup();
    const onSelectAsset = vi.fn();
    const onSelectAll = vi.fn();
    const duplicateAssetRows = [
      createRow(createAsset("ASSET-1")),
      createRow(createAsset("ASSET-1"), { rowId: "ASSET-1::event-service::1" }),
      createRow(createAsset("ASSET-2")),
    ];
    renderTimeline(
      createProps({
        rows: duplicateAssetRows,
        tasks: duplicateAssetRows.map(createTask),
        onSelectAsset,
        onSelectAll,
      }),
    );

    await user.click(screen.getAllByRole("checkbox", { name: "Select asset ASSET-1" })[0]);
    expect(onSelectAsset).toHaveBeenCalledWith("ASSET-1", true);

    await user.click(screen.getByRole("checkbox", { name: "Select all assets on this timeline page" }));
    expect(onSelectAll).toHaveBeenCalledWith(true);
  });
});
