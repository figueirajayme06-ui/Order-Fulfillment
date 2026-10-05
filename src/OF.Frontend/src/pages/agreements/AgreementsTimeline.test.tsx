import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import type { AgreementListItem } from "../../services/agreementsService";
import type { FrappeGanttNavigationState, GanttTask } from "../../components/timeline/FrappeGantt";
import { ApiFulfilmentStatus } from "../../types";
import { repairTableColumnLayout } from "../../lib/tableColumnLayout";
import { AGREEMENT_TIMELINE_COLUMN_CATALOG } from "./agreementColumnCatalog";
import styles from "./AgreementsPage.module.css";
import { AgreementsTimeline, type AgreementsTimelineProps } from "./AgreementsTimeline";

interface MockFrappeProps {
  tasks: GanttTask[];
  viewMode?: string;
  minimumDate?: string;
  readonlyDates?: boolean;
  readonlyProgress?: boolean;
  hoveredTaskId?: string | null;
  onHoverTaskIdChange?: (taskId: string | null) => void;
  onNavigationStateChange?: (state: FrappeGanttNavigationState) => void;
}

interface MockFrappeNavigation {
  jumpToToday: () => void;
  panEarlier: () => void;
  panLater: () => void;
}

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
    React.useEffect(() => {
      props.onNavigationStateChange?.({ canPanEarlier: true, canPanLater: true });
    }, [props.onNavigationStateChange]);

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
        <button type="button" onClick={() => props.onHoverTaskIdChange?.("2")}>
          Chart hover 2
        </button>
        <button type="button" onClick={() => props.onHoverTaskIdChange?.(null)}>
          Chart clear hover
        </button>
      </div>
    );
  });

  return { FrappeGantt };
});

const TIMELINE_TABLE_WIDTH_STORAGE_KEY = "agreements.timelineTableWidth.v1";

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
    lineCount: 2,
    deliveryDate: "2026-08-02T12:00:00.000Z",
    validFromDate: "2026-08-03T12:00:00.000Z",
    validToDate: "2026-08-30T12:00:00.000Z",
    terminationDate: "2026-08-29T12:00:00.000Z",
    collectionDate: "2026-09-01T12:00:00.000Z",
    customerAddress: "1 Test Street",
    lastUpdatedByName: "Alex Developer",
    opportunityName: `Opportunity ${id}`,
    ...overrides,
  };
}

function createTask(id: number): GanttTask {
  return {
    id: String(id),
    name: `A-${id}`,
    start: "2026-08-01",
    end: "2026-08-31",
    progress: 0,
    custom_class: "status-unfulfilled",
  };
}

function createProps(overrides: Partial<AgreementsTimelineProps> = {}): AgreementsTimelineProps {
  return {
    agreements: [createAgreement(1), createAgreement(2)],
    columnFilters: {},
    columnLayout: repairTableColumnLayout(AGREEMENT_TIMELINE_COLUMN_CATALOG),
    currentPage: 1,
    historyStart: "2025-08-14",
    sortDirection: "asc",
    sortField: "agreementNumber",
    statusFilter: "",
    tasks: [createTask(1), createTask(2)],
    totalPages: 1,
    warehouseOptions: [
      { label: "GLA", value: "GLA" },
      { label: "MAN", value: "MAN" },
    ],
    onAgreementOpen: vi.fn(),
    onColumnFilterChange: vi.fn(),
    onColumnLayoutChange: vi.fn(),
    onNextPage: vi.fn(),
    onPreviousPage: vi.fn(),
    onSort: vi.fn(),
    onStatusFilterChange: vi.fn(),
    onTimelinePeriodApply: vi.fn(),
    ...overrides,
  };
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

function firePointerEvent(
  element: Element,
  type: "pointerdown" | "pointermove" | "pointerup",
  values: { button?: number; clientX: number; pointerId: number },
): void {
  const event = new Event(type, { bubbles: true, cancelable: true });
  Object.defineProperties(event, {
    button: { value: values.button ?? 0 },
    clientX: { value: values.clientX },
    pointerId: { value: values.pointerId },
  });
  fireEvent(element, event);
}

describe("AgreementsTimeline", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    frappeMock.latestProps = null;
    window.localStorage.clear();
    vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
      callback(0);
      return 1;
    });
    vi.stubGlobal("cancelAnimationFrame", vi.fn());
    await i18n.changeLanguage("en");
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("sorts timeline rows from a context-column header", async () => {
    const user = userEvent.setup();
    const onSort = vi.fn();

    render(<AgreementsTimeline {...createProps({ onSort })} />);

    await user.click(screen.getByRole("button", { name: "Sort by Agreement No., currently ascending" }));

    expect(onSort).toHaveBeenCalledWith("agreementNumber");
  });

  it("passes the supplied task ids to Frappe and opens the agreement represented by a row", async () => {
    const user = userEvent.setup();
    const tasks = [createTask(1), createTask(2)];
    const onAgreementOpen = vi.fn();

    render(<AgreementsTimeline {...createProps({ tasks, onAgreementOpen })} />);

    expect(screen.getAllByTestId("frappe-task-id").map((node) => node.textContent)).toEqual(["1", "2"]);
    expect(screen.getByRole("columnheader", { name: "Customer" })).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /opportunity/i })).not.toBeInTheDocument();
    expect(screen.queryByText("Opportunity 1")).not.toBeInTheDocument();
    expect(screen.queryByText("Opportunity 2")).not.toBeInTheDocument();
    expect(getLatestFrappeProps()).toMatchObject({
      tasks,
      viewMode: "Day",
      minimumDate: "2025-08-14",
      readonlyDates: true,
      readonlyProgress: true,
    });

    const secondRow = screen.getByText("A-2").closest("tr");
    expect(secondRow).not.toBeNull();
    await user.click(secondRow!);

    expect(onAgreementOpen).toHaveBeenCalledOnce();
    expect(onAgreementOpen).toHaveBeenCalledWith(2);
  });

  it("stages a period until it is applied, then sends both dates together", async () => {
    const user = userEvent.setup();
    const onTimelinePeriodApply = vi.fn();
    render(<AgreementsTimeline {...createProps({ onTimelinePeriodApply })} />);

    fireEvent.change(screen.getByLabelText("From Date"), { target: { value: "2026-09-01" } });
    fireEvent.change(screen.getByLabelText("To Date"), { target: { value: "2026-09-30" } });
    expect(onTimelinePeriodApply).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Apply period" }));
    expect(onTimelinePeriodApply).toHaveBeenCalledWith("2026-09-01", "2026-09-30");
  });

  it("keeps the calendar protected while rendering configured context columns", () => {
    vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(1200);
    const { container } = render(<AgreementsTimeline {...createProps()} />);

    expect(screen.getByRole("separator", { name: "Resize agreement details" })).toHaveAttribute("aria-valuemax", "948");
    expect(container.querySelector(`.${styles.timelineTable}`)).toHaveStyle({ width: "500px" });
  });

  it("renders optional Agreement context fields from the supplied layout", () => {
    const layout = repairTableColumnLayout(AGREEMENT_TIMELINE_COLUMN_CATALOG).map((column) => {
      if (column.key === "warehouse") return { ...column, visible: false };
      if (column.key === "opportunityName") return { ...column, visible: true };
      return column;
    });

    render(<AgreementsTimeline {...createProps({ columnLayout: layout })} />);

    expect(screen.getByRole("columnheader", { name: "Opportunity" })).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "WHS" })).not.toBeInTheDocument();
    expect(screen.getByText("Opportunity 1")).toBeInTheDocument();
  });

  it("synchronizes agreement row hover with the chart in both directions", async () => {
    const user = userEvent.setup();
    render(<AgreementsTimeline {...createProps()} />);

    const firstRow = screen.getByText("A-1").closest("tr");
    const secondRow = screen.getByText("A-2").closest("tr");
    expect(firstRow).not.toBeNull();
    expect(secondRow).not.toBeNull();

    fireEvent.mouseEnter(firstRow!);
    expect(screen.getByTestId("frappe-gantt")).toHaveAttribute("data-hovered-task-id", "1");
    fireEvent.mouseLeave(firstRow!);
    expect(screen.getByTestId("frappe-gantt")).toHaveAttribute("data-hovered-task-id", "");

    await user.click(screen.getByRole("button", { name: "Chart hover 2" }));
    expect(secondRow).toHaveClass(styles.timelineRowHovered);
    await user.click(screen.getByRole("button", { name: "Chart clear hover" }));
    expect(secondRow).not.toHaveClass(styles.timelineRowHovered);
  });

  it("jumps to today when the Agreement timeline opens", () => {
    render(<AgreementsTimeline {...createProps()} />);

    expect(frappeMock.jumpToToday).toHaveBeenCalledOnce();
  });

  it("renders filters for every left-side data column and forwards their values", () => {
    const onColumnFilterChange = vi.fn();
    const onStatusFilterChange = vi.fn();
    const props = createProps({
      columnFilters: { agreementNumber: ["A-1"] },
      onColumnFilterChange,
      onStatusFilterChange,
    });
    const { rerender } = render(<AgreementsTimeline {...props} />);

    expect(screen.getByRole("button", { name: "Remove A-1 from Agreement No. filter" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Filter Status: All" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Fully Fulfilled" }));
    fireEvent.change(screen.getByLabelText("Filter Agreement No."), { target: { value: "A-2" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Agreement No."), { key: "Enter" });
    fireEvent.change(screen.getByLabelText("Filter Customer"), { target: { value: "Expo" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Customer"), { key: "Enter" });
    fireEvent.click(screen.getByRole("button", { name: "Filter WHS: All" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "MAN" }));
    fireEvent.change(screen.getByLabelText("Filter Delivery Date"), { target: { value: "2026-08-02" } });

    expect(onColumnFilterChange.mock.calls).toEqual([
      ["agreementNumber", ["A-1", "A-2"]],
      ["customerOrOpportunity", ["Expo"]],
      ["warehouse", ["MAN"]],
      ["deliveryDate", "2026-08-02"],
    ]);
    expect(onStatusFilterChange).toHaveBeenCalledWith("3");
    rerender(<AgreementsTimeline {...props} statusFilter="1,3" />);
    expect(screen.getByRole("button", { name: "Filter Status: 2 selected" })).toBeInTheDocument();
  });

  it("resizes by arrow key, uses the Shift step, clamps the width, and persists it", () => {
    const { container } = render(<AgreementsTimeline {...createProps()} />);
    const separator = screen.getByRole("separator", { name: "Resize agreement details" });
    const table = container.querySelector(`.${styles.timelineTable}`);

    expect(separator).toHaveAttribute("aria-orientation", "vertical");
    expect(separator).toHaveAttribute("aria-valuemin", "320");
    expect(separator).toHaveAttribute("aria-valuemax", "720");
    expect(separator).toHaveAttribute("aria-valuenow", "500");
    expect(separator).toHaveAttribute("tabindex", "0");
    expect(separator).toHaveAttribute(
      "title",
      "Drag to resize row details. Use Left and Right arrow keys for smaller adjustments.",
    );
    expect(table).toHaveStyle({ width: "500px" });

    fireEvent.keyDown(separator, { key: "ArrowRight" });
    expect(separator).toHaveAttribute("aria-valuenow", "524");
    expect(window.localStorage.getItem(TIMELINE_TABLE_WIDTH_STORAGE_KEY)).toBe("524");

    fireEvent.keyDown(separator, { key: "ArrowRight", shiftKey: true });
    fireEvent.keyDown(separator, { key: "ArrowRight", shiftKey: true });
    fireEvent.keyDown(separator, { key: "ArrowRight", shiftKey: true });
    expect(separator).toHaveAttribute("aria-valuenow", "720");
    expect(window.localStorage.getItem(TIMELINE_TABLE_WIDTH_STORAGE_KEY)).toBe("720");

    for (let index = 0; index < 5; index += 1) {
      fireEvent.keyDown(separator, { key: "ArrowLeft", shiftKey: true });
    }
    expect(separator).toHaveAttribute("aria-valuenow", "320");
    expect(table).toHaveStyle({ width: "320px" });
    expect(window.localStorage.getItem(TIMELINE_TABLE_WIDTH_STORAGE_KEY)).toBe("320");
  });

  it("resizes with pointer capture and persists the completed drag", () => {
    const { container } = render(<AgreementsTimeline {...createProps()} />);
    const separator = screen.getByRole("separator", { name: "Resize agreement details" });
    const table = container.querySelector(`.${styles.timelineTable}`);
    const setPointerCapture = vi.fn();
    const releasePointerCapture = vi.fn();
    Object.defineProperties(separator, {
      setPointerCapture: { configurable: true, value: setPointerCapture },
      hasPointerCapture: { configurable: true, value: () => true },
      releasePointerCapture: { configurable: true, value: releasePointerCapture },
    });

    firePointerEvent(separator, "pointerdown", { clientX: 100, pointerId: 7 });
    expect(setPointerCapture).toHaveBeenCalledWith(7);
    expect(separator).toHaveClass(styles.timelineResizeHandleActive);

    firePointerEvent(separator, "pointermove", { clientX: 200, pointerId: 7 });
    expect(separator).toHaveAttribute("aria-valuenow", "600");
    expect(table).toHaveStyle({ width: "600px" });

    firePointerEvent(separator, "pointerup", { clientX: 200, pointerId: 7 });
    expect(releasePointerCapture).toHaveBeenCalledWith(7);
    expect(separator).not.toHaveClass(styles.timelineResizeHandleActive);
    expect(window.localStorage.getItem(TIMELINE_TABLE_WIDTH_STORAGE_KEY)).toBe("600");
  });

  it("preserves pagination callbacks and disabled states", async () => {
    const user = userEvent.setup();
    const onNextPage = vi.fn();
    const onPreviousPage = vi.fn();
    const props = createProps({ totalPages: 3, onNextPage, onPreviousPage });
    const { rerender } = render(<AgreementsTimeline {...props} />);

    expect(screen.getByText("Page 1 of 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "← Prev" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Next →" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "Next →" }));
    expect(onNextPage).toHaveBeenCalledOnce();
    expect(onPreviousPage).not.toHaveBeenCalled();

    rerender(<AgreementsTimeline {...props} currentPage={3} />);
    expect(screen.getByText("Page 3 of 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "← Prev" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Next →" })).toBeDisabled();

    await user.click(screen.getByRole("button", { name: "← Prev" }));
    expect(onPreviousPage).toHaveBeenCalledOnce();

    rerender(<AgreementsTimeline {...props} totalPages={1} />);
    expect(screen.queryByText("Page 3 of 1")).not.toBeInTheDocument();
  });

  it("keeps the legend, timeline print marker, and print-hidden controls", () => {
    const { container } = render(<AgreementsTimeline {...createProps({ totalPages: 2 })} />);

    expect(screen.getByText("Fulfilment Status")).toBeInTheDocument();
    const legend = container.querySelector(`.${styles.legend}`);
    expect(legend).not.toBeNull();
    expect(within(legend as HTMLElement).getByText("Unfulfilled")).toBeInTheDocument();
    expect(within(legend as HTMLElement).getByText("Partially Fulfilled")).toBeInTheDocument();
    expect(within(legend as HTMLElement).getByText("Fully Fulfilled")).toBeInTheDocument();
    expect(container.querySelector("[data-print-timeline]")).toBeInTheDocument();
  });

  it("aligns the agreement rows with the first Frappe grid row", () => {
    vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(function (this: HTMLElement) {
      if (this.tagName === "THEAD") {
        return createRect(0, 100);
      }
      if (this.classList.contains("grid-row")) {
        return createRect(148, 168);
      }
      return createRect(0, 0);
    });

    const { container } = render(<AgreementsTimeline {...createProps()} />);
    const spacer = container.querySelector(`.${styles.timelineHeaderSpacer}`);

    expect(spacer).not.toBeNull();
    expect(within(spacer as HTMLElement).getByRole("cell", { hidden: true })).toHaveStyle({ height: "48px" });
  });
});
