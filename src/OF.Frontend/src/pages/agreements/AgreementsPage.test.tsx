import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import { moveTableColumn, repairTableColumnLayout } from "../../lib/tableColumnLayout";
import { fetchAgreements, type AgreementListItem } from "../../services/agreementsService";
import { fetchDivisions } from "../../services/lookupsService";
import { listSavedViews, type PersistedSavedView } from "../../services/viewsService";
import { ApiFulfilmentStatus } from "../../types";
import { parseAgreementsSavedViewState, type AgreementsSavedViewState } from "../../types/savedViews";
import type { AgreementControlsProps } from "./AgreementControls";
import { AGREEMENT_COLUMN_CATALOG, AGREEMENT_TIMELINE_COLUMN_CATALOG } from "./agreementColumnCatalog";
import type { AgreementSavedViewControlsProps } from "./AgreementSavedViewControls";
import { AgreementsPage } from "./AgreementsPage";
import { writeSessionPageState } from "../../lib/sessionPageState";
import type { AgreementsTableProps } from "./AgreementsTable";
import type { AgreementsTimelineProps } from "./AgreementsTimeline";

const routeMock = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

const authMock = vi.hoisted(() => ({
  value: {
    user: {
      loginName: "developer@example.com",
      displayName: "Developer",
      division: "01, 02",
      isAdmin: false,
      isSuperAdmin: false,
      isReadOnly: false,
      language: "en",
    },
    isLoading: false,
    error: null,
  },
}));

const childMock = vi.hoisted(() => ({
  controlsProps: null as unknown,
  savedViewProps: null as unknown,
  tableProps: null as unknown,
  timelineProps: null as unknown,
}));

vi.mock("react-router-dom", () => ({
  useNavigate: () => routeMock.navigate,
}));

vi.mock("../../contexts/auth", () => ({
  useAuth: () => authMock.value,
}));

vi.mock("../../services/agreementsService", () => ({
  fetchAgreements: vi.fn(),
}));

vi.mock("../../services/lookupsService", () => ({
  fetchDivisions: vi.fn(),
}));

vi.mock("../../services/viewsService", () => ({
  createSavedView: vi.fn(),
  deleteSavedView: vi.fn(),
  listSavedViews: vi.fn(),
  updateSavedView: vi.fn(),
}));

vi.mock("./AgreementControls", () => ({
  AgreementControls: (props: AgreementControlsProps) => {
    childMock.controlsProps = props;

    return (
      <section data-testid="agreement-controls">
        <output data-testid="agreement-count">{props.agreementCount} agreements</output>
        {props.savedViewControls}
        {props.tableActions}
        <button type="button" onClick={() => props.onViewModeChange("table")}>
          Show table
        </button>
        <button type="button" onClick={() => props.onViewModeChange("timeline")}>
          Show timeline
        </button>
        <button type="button" onClick={() => props.onSearchTermChange("crane")}>
          Set search
        </button>
        <button type="button" onClick={props.onRefresh}>
          Refresh agreements
        </button>
      </section>
    );
  },
}));

vi.mock("./AgreementSavedViewControls", () => ({
  AgreementSavedViewControls: (props: AgreementSavedViewControlsProps) => {
    childMock.savedViewProps = props;
    return <output data-testid="saved-view-feedback">{props.feedback?.message ?? ""}</output>;
  },
}));

vi.mock("./AgreementsTable", () => ({
  AgreementsTable: (props: AgreementsTableProps) => {
    childMock.tableProps = props;

    return (
      <section data-testid="agreements-table">
        <output data-testid="table-page">
          Page {props.currentPage} of {props.totalPages}
        </output>
        <select
          aria-label="Rows per page"
          value={props.pageSize}
          onChange={(event) => props.onPageSizeChange(Number(event.target.value))}
        >
          {[50, 250, 500, 1000].map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
        {props.agreements.length === 0 && <output>No results found</output>}
        {props.agreements.map((agreement) => (
          <div key={agreement.id}>
            <button type="button" onClick={() => props.onRowClick(agreement.id)}>
              Open table agreement {agreement.id}
            </button>
          </div>
        ))}
        <button type="button" onClick={props.onPreviousPage} disabled={props.currentPage === 1}>
          Previous table page
        </button>
        <button type="button" onClick={props.onNextPage} disabled={props.currentPage === props.totalPages}>
          Next table page
        </button>
      </section>
    );
  },
}));

vi.mock("./AgreementsTimeline", () => ({
  AgreementsTimeline: (props: AgreementsTimelineProps) => {
    childMock.timelineProps = props;

    return (
      <section data-testid="agreements-timeline">
        <output data-testid="timeline-page">
          Page {props.currentPage} of {props.totalPages}
        </output>
        {props.agreements.map((agreement) => (
          <button key={agreement.id} type="button" onClick={() => props.onAgreementOpen(agreement.id)}>
            Open timeline agreement {agreement.id}
          </button>
        ))}
        <button type="button" onClick={props.onPreviousPage} disabled={props.currentPage === 1}>
          Previous timeline page
        </button>
        <button type="button" onClick={props.onNextPage} disabled={props.currentPage === props.totalPages}>
          Next timeline page
        </button>
      </section>
    );
  },
}));

const mockedFetchAgreements = vi.mocked(fetchAgreements);
const mockedFetchDivisions = vi.mocked(fetchDivisions);
const mockedListSavedViews = vi.mocked(listSavedViews);

function createAgreement(id: number, overrides: Partial<AgreementListItem> = {}): AgreementListItem {
  return {
    id,
    agreementNumber: `A-${id}`,
    customerName: `Customer ${id}`,
    customerNumber: `C-${id}`,
    division: "01",
    warehouse: "ED1",
    fulfilmentStatus: ApiFulfilmentStatus.Unfulfilled,
    onHireDate: "2099-08-01T12:00:00.000Z",
    offHireDate: "2099-08-31T12:00:00.000Z",
    isDeleted: false,
    orderSource: "D365",
    lineCount: 2,
    deliveryDate: "2099-08-02T12:00:00.000Z",
    validFromDate: "2099-08-03T12:00:00.000Z",
    validToDate: "2099-08-30T12:00:00.000Z",
    terminationDate: "2099-08-29T12:00:00.000Z",
    collectionDate: "2099-09-01T12:00:00.000Z",
    customerAddress: "1 Test Street",
    lastUpdatedByName: "Alex Developer",
    opportunityName: `Opportunity ${id}`,
    ...overrides,
  };
}

function createSavedState(overrides: Partial<AgreementsSavedViewState> = {}): AgreementsSavedViewState {
  return {
    stateVersion: 1,
    viewMode: "table",
    searchTerm: "",
    showHistorical: false,
    selectedDivision: "01",
    orderTypeFilter: "",
    statusFilter: "",
    advancedFilters: {},
    columnFilters: {},
    sortField: "onHireDate",
    sortDirection: "desc",
    ...overrides,
  };
}

function createDefaultView(state: AgreementsSavedViewState): PersistedSavedView<AgreementsSavedViewState> {
  return {
    id: "default-view",
    name: "Default operations",
    page: "agreements",
    scope: "personal",
    owner: "developer@example.com",
    isOwner: true,
    canEdit: true,
    canDelete: true,
    isDefault: true,
    source: "api",
    recipients: [],
    state,
  };
}

function getControlsProps(): AgreementControlsProps {
  return childMock.controlsProps as AgreementControlsProps;
}

function getSavedViewProps(): AgreementSavedViewControlsProps {
  return childMock.savedViewProps as AgreementSavedViewControlsProps;
}

function getTableProps(): AgreementsTableProps {
  return childMock.tableProps as AgreementsTableProps;
}

function getTimelineProps(): AgreementsTimelineProps {
  return childMock.timelineProps as AgreementsTimelineProps;
}

const initialQuery = {
  showHistorical: false,
  division: undefined,
  search: undefined,
  orderTypes: undefined,
  statuses: undefined,
};

describe("AgreementsPage", () => {
  beforeEach(async () => {
    sessionStorage.clear();
    vi.resetAllMocks();
    childMock.controlsProps = null;
    childMock.savedViewProps = null;
    childMock.tableProps = null;
    childMock.timelineProps = null;
    window.localStorage.clear();
    mockedFetchAgreements.mockResolvedValue([]);
    mockedFetchDivisions.mockResolvedValue([
      { code: "01", name: "North" },
      { code: "02", name: "South" },
    ]);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [] });
    await i18n.changeLanguage("en");
  });

  afterEach(cleanup);

  it("loads with the user's exact division query and publishes the successful agreement count", async () => {
    const warehouses = ["ED1", "CN1", "EM1"];
    const agreements = Array.from({ length: 26 }, (_, index) =>
      createAgreement(index + 1, {
        division: index % 2 === 0 ? "01" : "02",
        warehouse: warehouses[index % warehouses.length],
      }),
    );
    mockedFetchAgreements.mockResolvedValue(agreements);

    render(<AgreementsPage />);

    expect(screen.getByRole("status", { name: "Loading agreements table" })).toBeInTheDocument();
    await screen.findByTestId("agreements-table");

    expect(mockedFetchAgreements).toHaveBeenCalledOnce();
    expect(mockedFetchAgreements).toHaveBeenCalledWith(initialQuery);
    expect(mockedFetchDivisions).toHaveBeenCalledOnce();
    expect(getTableProps().warehouseOptions).toEqual([
      { label: "CN1", value: "CN1" },
      { label: "ED1", value: "ED1" },
      { label: "EM1", value: "EM1" },
    ]);
    expect(screen.getByTestId("agreement-count")).toHaveTextContent("26 agreements");
    expect(getTableProps().agreements).toHaveLength(26);
    expect(getTableProps()).toMatchObject({ currentPage: 1, totalPages: 1 });
  });

  it("restores working state before the first request without issuing a default-state request", async () => {
    writeSessionPageState(
      "developer@example.com",
      "agreements",
      createSavedState({ searchTerm: "restored", currentPage: 2, pageSize: 50 }),
    );
    mockedFetchAgreements.mockResolvedValue(Array.from({ length: 60 }, (_, index) => createAgreement(index + 1)));

    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    expect(mockedFetchAgreements).toHaveBeenCalledOnce();
    expect(mockedFetchAgreements).toHaveBeenCalledWith(expect.objectContaining({ search: "restored" }));
    expect(getTableProps()).toMatchObject({ currentPage: 2, pageSize: 50, totalPages: 2 });
  });

  it.each(["agreements.savedViewSelection.v3", "orders.savedViewSelection.v2"])(
    "restores working state when stale saved-view selection %s remains",
    async (selectionKey) => {
      localStorage.setItem(selectionKey, "missing-view");
      writeSessionPageState(
        "developer@example.com",
        "agreements",
        createSavedState({ searchTerm: "restored agreement" }),
      );

      render(<AgreementsPage />);
      await screen.findByTestId("agreements-table");

      expect(mockedFetchAgreements).toHaveBeenCalledOnce();
      expect(mockedFetchAgreements).toHaveBeenCalledWith(expect.objectContaining({ search: "restored agreement" }));
    },
  );

  it("restores an active column-filter draft after immediate detail navigation", async () => {
    mockedFetchAgreements.mockResolvedValue([createAgreement(1), createAgreement(2)]);
    const firstRender = render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    act(() => getTableProps().onColumnFilterDraftChange?.("opportunityName", "2"));
    firstRender.unmount();

    mockedFetchAgreements.mockClear();
    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    expect(mockedFetchAgreements).toHaveBeenCalledOnce();
    expect(getTableProps().columnFilters.opportunityName).toEqual(["2"]);
    expect(getTableProps().agreements.map((agreement) => agreement.id)).toEqual([2]);
  });

  it("keeps the table available when filters return no agreements", async () => {
    render(<AgreementsPage />);

    await screen.findByTestId("agreements-table");
    expect(getTableProps().agreements).toEqual([]);
  });

  it("keeps load failures generic and visible without exposing the service error", async () => {
    mockedListSavedViews.mockResolvedValue({
      source: "api",
      views: [{ ...createDefaultView(createSavedState()), scope: "users", isOwner: false }],
    });
    mockedFetchAgreements.mockRejectedValue(new Error("Sensitive backend failure"));

    render(<AgreementsPage />);

    expect(await screen.findByText("An error occurred")).toBeInTheDocument();
    expect(screen.queryByText("Sensitive backend failure")).not.toBeInTheDocument();
    expect(screen.queryByText(/outside your divisions/i)).not.toBeInTheDocument();
    expect(screen.getByTestId("agreement-count")).toHaveTextContent("0 agreements");
    expect(screen.queryByTestId("agreements-table")).not.toBeInTheDocument();
    expect(screen.queryByTestId("agreements-timeline")).not.toBeInTheDocument();
    expect(mockedFetchAgreements).toHaveBeenCalledWith(initialQuery);
  });

  it("explains an empty received view and offers to reset its filters", async () => {
    mockedListSavedViews.mockResolvedValue({
      source: "api",
      views: [{ ...createDefaultView(createSavedState({ searchTerm: "outside" })), scope: "users", isOwner: false }],
    });
    render(<AgreementsPage />);

    const explanation = await screen.findByText(/outside your divisions/i);
    expect(explanation).toBeInTheDocument();
    expect(explanation.closest("[data-print-hidden]")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reset filters" })).toBeInTheDocument();
  });

  it("keeps the table mounted so an empty column filter can be edited and cleared", async () => {
    mockedFetchAgreements.mockResolvedValue([createAgreement(1)]);

    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    act(() => getTableProps().onColumnFilterChange("opportunityName", "dhuvbad"));

    expect(screen.getByTestId("agreements-table")).toBeInTheDocument();
    expect(screen.getByText("No results found")).toBeInTheDocument();
    expect(getTableProps().agreements).toHaveLength(0);
    expect(getTableProps().columnFilters.opportunityName).toBe("dhuvbad");

    act(() => getTableProps().onColumnFilterChange("opportunityName", ""));

    expect(screen.getByTestId("agreements-table")).toBeInTheDocument();
    expect(getTableProps().agreements).toHaveLength(1);
  });

  it("resets every active basic and column filter to the user's default view", async () => {
    mockedFetchAgreements.mockResolvedValue([createAgreement(1)]);

    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    act(() => {
      getControlsProps().onSearchTermChange("crane");
      getControlsProps().onSelectedDivisionChange("02");
      getControlsProps().onOrderTypeFilterChange("agreement");
      getControlsProps().onStatusFilterChange(String(ApiFulfilmentStatus.PartiallyFulfilled));
      getControlsProps().onShowHistoricalChange(true);
      getTableProps().onColumnFilterChange("warehouse", "ED1");
    });

    await waitFor(() => expect(getControlsProps().hasActiveFilters).toBe(true));

    act(() => getControlsProps().onResetFilters());

    await waitFor(() =>
      expect(getControlsProps()).toMatchObject({
        hasActiveFilters: false,
        orderTypeFilter: "",
        searchTerm: "",
        selectedDivision: "",
        showHistorical: false,
        statusFilter: "",
      }),
    );
    expect(getTableProps().columnFilters).toEqual({});
  });

  it("keeps the top-row and column-header status filters synchronized across both views", async () => {
    mockedFetchAgreements.mockResolvedValue([
      createAgreement(1, { fulfilmentStatus: ApiFulfilmentStatus.Unfulfilled }),
      createAgreement(2, { fulfilmentStatus: ApiFulfilmentStatus.PartiallyFulfilled }),
      createAgreement(3, { fulfilmentStatus: ApiFulfilmentStatus.FullyFulfilled }),
    ]);

    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    act(() => getTableProps().onStatusFilterChange("0,1"));
    await waitFor(() => expect(getControlsProps().statusFilter).toBe("0,1"));
    expect(getTableProps().statusFilter).toBe("0,1");
    expect(getTableProps().agreements.map((agreement) => agreement.id)).toEqual([1, 2]);

    fireEvent.click(screen.getByRole("button", { name: "Show timeline" }));
    await screen.findByTestId("agreements-timeline");
    expect(getTimelineProps().statusFilter).toBe("0,1");
    expect(getTimelineProps().agreements.map((agreement) => agreement.id)).toEqual([1, 2]);

    act(() => getTimelineProps().onStatusFilterChange("3"));
    await waitFor(() => expect(getControlsProps().statusFilter).toBe("3"));
    expect(getTimelineProps().statusFilter).toBe("3");
    expect(getTimelineProps().agreements.map((agreement) => agreement.id)).toEqual([3]);

    fireEvent.click(screen.getByRole("button", { name: "Show table" }));
    expect(getTableProps().statusFilter).toBe("3");
    expect(getTableProps().agreements.map((agreement) => agreement.id)).toEqual([3]);
  });

  it("promotes a legacy Agreement status column filter into the shared status filter", async () => {
    writeSessionPageState(
      "developer@example.com",
      "agreements",
      createSavedState({ columnFilters: { fulfilmentStatus: ["1", "3"] } }),
    );
    mockedFetchAgreements.mockResolvedValue([createAgreement(1)]);

    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    expect(mockedFetchAgreements).toHaveBeenCalledWith(expect.objectContaining({ statuses: "1,3" }));
    expect(getControlsProps().statusFilter).toBe("1,3");
    expect(getTableProps().statusFilter).toBe("1,3");
    expect(getTableProps().columnFilters.fulfilmentStatus).toBeUndefined();
  });

  it("applies the default saved state without restoring retired advanced filters", async () => {
    const savedState = createSavedState({
      viewMode: "timeline",
      searchTerm: "north",
      showHistorical: true,
      selectedDivision: "02",
      orderTypeFilter: "quote",
      statusFilter: String(ApiFulfilmentStatus.PartiallyFulfilled),
      advancedFilters: {
        warehouse: "MAN",
        offHireDateFrom: "2099-08-01",
      },
      columnFilters: { customerName: "north", division: "999" },
      sortField: "offHireDate",
      sortDirection: "asc",
    });
    const defaultView = createDefaultView(savedState);
    const savedAgreements = [
      createAgreement(100, {
        agreementNumber: "Q-100",
        customerName: "North later",
        warehouse: "MAN",
        fulfilmentStatus: ApiFulfilmentStatus.PartiallyFulfilled,
        offHireDate: "2099-08-31T12:00:00.000Z",
      }),
      createAgreement(101, {
        agreementNumber: "Q-101",
        customerName: "North earlier",
        warehouse: "MAN",
        fulfilmentStatus: ApiFulfilmentStatus.PartiallyFulfilled,
        offHireDate: "2099-08-15T12:00:00.000Z",
      }),
      createAgreement(102, {
        agreementNumber: "Q-102",
        customerName: "South excluded",
        warehouse: "MAN",
        fulfilmentStatus: ApiFulfilmentStatus.PartiallyFulfilled,
        offHireDate: "2099-08-01T12:00:00.000Z",
      }),
    ];
    mockedFetchAgreements.mockResolvedValueOnce([createAgreement(1)]).mockResolvedValue(savedAgreements);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [defaultView] });

    render(<AgreementsPage />);

    await waitFor(() => expect(mockedFetchAgreements).toHaveBeenCalledTimes(2));
    await screen.findByTestId("agreements-timeline");

    expect(mockedFetchAgreements).toHaveBeenNthCalledWith(1, initialQuery);
    expect(mockedFetchAgreements).toHaveBeenNthCalledWith(2, {
      showHistorical: true,
      division: "02",
      search: "north",
      orderTypes: "quote",
      statuses: String(ApiFulfilmentStatus.PartiallyFulfilled),
    });
    expect(mockedFetchAgreements.mock.invocationCallOrder[0]).toBeLessThan(
      mockedListSavedViews.mock.invocationCallOrder[0],
    );
    expect(mockedListSavedViews).toHaveBeenCalledWith("agreements", parseAgreementsSavedViewState);
    expect(getControlsProps()).toMatchObject({
      agreementCount: 2,
      orderTypeFilter: "quote",
      searchTerm: "north",
      selectedDivision: "02",
      showHistorical: true,
      statusFilter: String(ApiFulfilmentStatus.PartiallyFulfilled),
      viewMode: "timeline",
    });
    expect(getTimelineProps().agreements.map((agreement) => agreement.id)).toEqual([101, 100]);
    expect(getTimelineProps().tasks.map((task) => task.id)).toEqual(["101", "100"]);
    expect(getSavedViewProps().feedback).toEqual({
      tone: "success",
      message: "Default view applied: Default operations",
    });
    expect(window.localStorage.getItem("agreements.showHistorical")).toBe("true");
  });

  it("clears a column filter when its Agreement column is hidden", async () => {
    const user = userEvent.setup();
    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    act(() => getTableProps().onColumnFilterChange("opportunityName", "renewal"));
    await waitFor(() => expect(getTableProps().columnFilters.opportunityName).toBe("renewal"));
    await user.click(screen.getByRole("button", { name: "Columns, 13 visible" }));
    await user.click(screen.getByRole("checkbox", { name: "Opportunity" }));

    await waitFor(() => expect(getTableProps().columnFilters.opportunityName).toBeUndefined());
    expect(getTableProps().columnLayout.find((column) => column.key === "opportunityName")?.visible).toBe(false);
  });

  it("keeps timeline columns independent and clears a filter when its context column is hidden", async () => {
    const user = userEvent.setup();
    mockedFetchAgreements.mockResolvedValue([createAgreement(1)]);
    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");
    await user.click(screen.getByRole("button", { name: "Show timeline" }));
    await screen.findByTestId("agreements-timeline");

    act(() => getTimelineProps().onColumnFilterChange("customerOrOpportunity", "Expo"));
    await waitFor(() => expect(getTimelineProps().columnFilters.customerOrOpportunity).toBe("Expo"));
    await user.click(screen.getByRole("button", { name: "Columns, 5 visible" }));
    expect(screen.getByRole("checkbox", { name: "Opportunity" })).toBeEnabled();
    await user.click(screen.getByRole("checkbox", { name: "Customer" }));

    await waitFor(() => expect(getTimelineProps().columnFilters.customerOrOpportunity).toBeUndefined());
    expect(getTimelineProps().columnLayout.find((column) => column.key === "customerName")?.visible).toBe(false);
    expect(getTableProps().columnLayout).toEqual(repairTableColumnLayout(AGREEMENT_COLUMN_CATALOG));
    expect(getTimelineProps().columnLayout).not.toEqual(repairTableColumnLayout(AGREEMENT_TIMELINE_COLUMN_CATALOG));
  });

  it("applies a saved Agreement column order and width with the rest of the view state", async () => {
    const savedColumns = moveTableColumn(
      AGREEMENT_COLUMN_CATALOG,
      repairTableColumnLayout(AGREEMENT_COLUMN_CATALOG),
      "opportunityName",
      "left",
    ).map((column) => (column.key === "customerName" ? { ...column, width: 410 } : column));
    mockedListSavedViews.mockResolvedValue({
      source: "api",
      views: [createDefaultView(createSavedState({ columns: savedColumns }))],
    });

    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    await waitFor(() => expect(getTableProps().columnLayout).toEqual(savedColumns));
  });

  it("keeps table and timeline paging client-side and forwards row navigation", async () => {
    const user = userEvent.setup();
    const agreements = Array.from({ length: 51 }, (_, index) => createAgreement(index + 1));
    mockedFetchAgreements.mockResolvedValue(agreements);

    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    await user.click(screen.getByRole("button", { name: "Next table page" }));
    expect(screen.getByTestId("table-page")).toHaveTextContent("Page 2 of 2");
    expect(getTableProps().agreements.map((agreement) => agreement.id)).toEqual([51]);
    await user.click(screen.getByRole("button", { name: "Open table agreement 51" }));
    expect(routeMock.navigate).toHaveBeenLastCalledWith("/agreements/51");

    await user.selectOptions(screen.getByRole("combobox", { name: "Rows per page" }), "250");
    expect(screen.getByTestId("table-page")).toHaveTextContent("Page 1 of 1");
    expect(getTableProps().agreements).toHaveLength(51);
    expect(getTableProps().pageSize).toBe(250);

    await user.click(screen.getByRole("button", { name: "Show timeline" }));
    expect(screen.getByTestId("timeline-page")).toHaveTextContent("Page 1 of 2");
    expect(getTimelineProps().agreements).toHaveLength(50);
    await user.click(screen.getByRole("button", { name: "Next timeline page" }));
    expect(screen.getByTestId("timeline-page")).toHaveTextContent("Page 2 of 2");
    expect(getTimelineProps().agreements.map((agreement) => agreement.id)).toEqual([51]);
    await user.click(screen.getByRole("button", { name: "Open timeline agreement 51" }));
    expect(routeMock.navigate).toHaveBeenLastCalledWith("/agreements/51");

    await user.click(screen.getByRole("button", { name: "Show table" }));
    expect(screen.getByTestId("table-page")).toHaveTextContent("Page 1 of 1");
    expect(mockedFetchAgreements).toHaveBeenCalledOnce();
  });

  it("reloads immediately for query-state changes and explicit refresh repeats the current query", async () => {
    const user = userEvent.setup();
    mockedFetchAgreements.mockResolvedValue([createAgreement(1)]);

    render(<AgreementsPage />);
    await screen.findByTestId("agreements-table");

    await user.click(screen.getByRole("button", { name: "Set search" }));
    await waitFor(() => expect(mockedFetchAgreements).toHaveBeenCalledTimes(2));
    expect(mockedFetchAgreements).toHaveBeenNthCalledWith(2, {
      ...initialQuery,
      search: "crane",
    });

    fireEvent.click(screen.getByRole("button", { name: "Refresh agreements" }));
    await waitFor(() => expect(mockedFetchAgreements).toHaveBeenCalledTimes(3));
    expect(mockedFetchAgreements).toHaveBeenNthCalledWith(3, {
      ...initialQuery,
      search: "crane",
    });
  });
});
