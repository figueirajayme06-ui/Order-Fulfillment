import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import { moveTableColumn, repairTableColumnLayout } from "../../lib/tableColumnLayout";
import { fetchAssets } from "../../services/assetsService";
import { listSavedViews, type SavedViewsListResult } from "../../services/viewsService";
import { invalidateCachedAssetLists } from "../../services/assetListCache";
import { fetchAssetEvents } from "../../services/eventsService";
import { fetchDivisions } from "../../services/lookupsService";
import {
  addRingfenceItems,
  fetchRingfences,
  preflightRingfenceItems,
  type RingfenceItemBatchResult,
  type RingfenceListItem,
} from "../../services/ringfenceService";
import type { Asset, AssetEvent } from "../../types";
import type { AssetsSavedViewState } from "../../types/savedViews";
import { ASSET_COLUMN_CATALOG, ASSET_TIMELINE_COLUMN_CATALOG } from "./assetColumnCatalog";
import type { AssetControlsProps } from "./AssetControls";
import type { AssetRingfenceActionsProps } from "./AssetRingfenceActions";
import type { AssetSavedViewControlsProps } from "./AssetSavedViewControls";
import { AssetsPage, parseRingfenceAssetsContext } from "./AssetsPage";
import { getSessionPageStateKey, writeSessionPageState } from "../../lib/sessionPageState";
import type { AssetsTableProps } from "./AssetsTable";
import type { AssetsTimelineProps } from "./AssetsTimeline";
import type { AssetSavedViewsController, UseAssetSavedViewsOptions } from "./useAssetSavedViews";

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
  ringfenceProps: null as unknown,
  savedViewProps: null as unknown,
  tableProps: null as unknown,
  timelineProps: null as unknown,
}));

const savedViewsMock = vi.hoisted(() => ({
  useRealHook: false,
  options: null as unknown,
  controller: {
    isInitialized: true,
    canDeleteSelectedView: false,
    canEditSelectedView: false,
    feedback: null,
    isDirty: false,
    recipientCandidates: [],
    recipientError: null,
    recipients: [],
    isRecipientLoading: false,
    isSelectedViewReceived: false,
    isSharingEditorOpen: false,
    isSharingDirty: false,
    pendingAction: null,
    name: "",
    scope: "personal",
    selectedViewId: "",
    views: [],
    cancelSharingEdit: vi.fn(),
    deleteSelected: vi.fn(),
    editSharing: vi.fn(),
    searchRecipients: vi.fn(),
    saveNew: vi.fn(),
    saveSharing: vi.fn(),
    selectView: vi.fn(),
    setName: vi.fn(),
    setRecipients: vi.fn(),
    setScope: vi.fn(),
    retryRecipients: vi.fn(),
    updateSelected: vi.fn(),
  },
}));

vi.mock("../../contexts/auth", () => ({
  useAuth: () => authMock.value,
}));

vi.mock("../../services/assetsService", () => ({
  fetchAssets: vi.fn(),
}));

vi.mock("../../services/eventsService", () => ({
  fetchAssetEvents: vi.fn(),
}));

vi.mock("../../services/lookupsService", () => ({
  fetchDivisions: vi.fn(),
}));

vi.mock("../../services/ringfenceService", () => ({
  addRingfenceItems: vi.fn(),
  fetchRingfences: vi.fn(),
  preflightRingfenceItems: vi.fn(),
}));

vi.mock("../../services/viewsService", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../../services/viewsService")>()),
  listSavedViews: vi.fn(),
}));

vi.mock("./useAssetSavedViews", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./useAssetSavedViews")>();
  return {
    useAssetSavedViews: (options: UseAssetSavedViewsOptions) => {
      if (savedViewsMock.useRealHook) return actual.useAssetSavedViews(options);
      savedViewsMock.options = options;
      return savedViewsMock.controller as AssetSavedViewsController;
    },
  };
});

vi.mock("./AssetControls", () => ({
  ASSET_STATUSES: ["Available", "OnHire", "Service", "Repair", "Collection", "In Transit"],
  AssetControls: (props: AssetControlsProps) => {
    childMock.controlsProps = props;

    return (
      <section data-testid="asset-controls">
        <output data-testid="asset-count">{props.assetCount} assets</output>
        {props.savedViewControls}
        {props.tableActions}
        <button type="button" onClick={() => props.onViewModeChange("table")}>
          Show table
        </button>
        <button type="button" onClick={() => props.onViewModeChange("timeline")}>
          Show timeline
        </button>
        <button type="button" onClick={() => props.onSearchTermChange("latest")}>
          Set search
        </button>
        <button type="button" onClick={() => props.onSelectedDivisionChange("02")}>
          Select division 02
        </button>
        <button type="button" onClick={props.onRefresh}>
          Refresh assets
        </button>
        <button type="button" disabled={!props.hasActiveFilters} onClick={props.onResetFilters}>
          Reset asset filters
        </button>
      </section>
    );
  },
}));

vi.mock("./AssetSavedViewControls", () => ({
  AssetSavedViewControls: (props: AssetSavedViewControlsProps) => {
    childMock.savedViewProps = props;
    return <output data-testid="saved-view-feedback">{props.feedback?.message ?? ""}</output>;
  },
}));

vi.mock("./AssetRingfenceActions", () => ({
  AssetRingfenceActions: (props: AssetRingfenceActionsProps) => {
    childMock.ringfenceProps = props;

    return (
      <section data-testid="asset-ringfence-actions">
        <output data-testid="ringfence-selection-count">{props.selectedAssetCount}</output>
        <output data-testid="ringfence-target">{props.selectedRingfenceId}</output>
        <output data-testid="ringfence-busy">{String(props.isBusy)}</output>
        <output data-testid="ringfence-return-path">{props.contextualReturnPath ?? ""}</output>
        <output data-testid="ringfence-is-contextual">{String(props.isContextualTarget)}</output>
        <button type="button" onClick={() => props.onSelectedRingfenceChange(7)}>
          Target ringfence 7
        </button>
        <button type="button" onClick={props.onAdd}>
          Add selected assets
        </button>
        <button type="button" onClick={props.onClear}>
          Clear selected assets
        </button>
      </section>
    );
  },
}));

vi.mock("./AssetsTable", () => ({
  AssetsTable: (props: AssetsTableProps) => {
    childMock.tableProps = props;
    const firstAsset = props.assets[0];

    return (
      <section data-testid="assets-table">
        <output data-testid="table-page">
          Page {props.currentPage} of {props.totalPages}
        </output>
        <output data-testid="table-asset-ids">{props.assets.map((asset) => asset.id).join(",")}</output>
        <output data-testid="table-selected-count">{props.selectedAssetIds.size}</output>
        <output data-testid="table-first-path">{firstAsset ? props.getAssetProfilePath(firstAsset.id) : ""}</output>
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
        {!firstAsset && props.emptyStateMessage && <output>{props.emptyStateMessage}</output>}
        {firstAsset && (
          <>
            <button type="button" onClick={() => props.onSelectAsset(firstAsset.id, true)}>
              Select first table asset
            </button>
          </>
        )}
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

vi.mock("./AssetsTimeline", () => ({
  AssetsTimeline: (props: AssetsTimelineProps) => {
    childMock.timelineProps = props;
    const firstRow = props.rows[0];

    return (
      <section data-testid="assets-timeline">
        <output data-testid="timeline-page">
          Page {props.currentPage} of {props.totalPages}
        </output>
        <output data-testid="timeline-row-ids">{props.rows.map((row) => row.rowId).join(",")}</output>
        <output data-testid="timeline-task-ids">{props.tasks.map((task) => task.id).join(",")}</output>
        <output data-testid="timeline-first-path">
          {firstRow ? props.getAssetProfilePath(firstRow.asset.id) : ""}
        </output>
        <output data-testid="timeline-selected-count">{props.selectedAssetIds.size}</output>
        {firstRow && (
          <button type="button" onClick={() => props.onSelectAsset(firstRow.asset.id, true)}>
            Select first timeline asset
          </button>
        )}
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

const mockedAddRingfenceItems = vi.mocked(addRingfenceItems);
const mockedFetchAssets = vi.mocked(fetchAssets);
const mockedFetchAssetEvents = vi.mocked(fetchAssetEvents);
const mockedFetchDivisions = vi.mocked(fetchDivisions);
const mockedFetchRingfences = vi.mocked(fetchRingfences);
const mockedPreflightRingfenceItems = vi.mocked(preflightRingfenceItems);

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
    assetId: "ASSET-001",
    eventType: "ONHIRE",
    startDate: "2026-08-10T12:00:00.000Z",
    endDate: "2026-08-20T12:00:00.000Z",
    title: "Planned hire",
    cssClass: "onhire_event",
    ...overrides,
  };
}

function createRingfence(id: number): RingfenceListItem {
  return {
    id,
    title: `Ringfence ${id}`,
    fromDate: "2026-08-01T00:00:00.000Z",
    toDate: "2026-08-31T00:00:00.000Z",
    divisions: "01",
    warehouse: "GLA",
    owner: "developer@example.com",
    assetCount: 0,
    createdBy: "developer@example.com",
    createdAt: "2026-07-01T00:00:00.000Z",
  };
}

function createBatchResult(overrides: Partial<RingfenceItemBatchResult> = {}): RingfenceItemBatchResult {
  return {
    readyAssetIds: ["ASSET-001"],
    alreadyAssignedAssetIds: [],
    unavailableAssetIds: [],
    addedAssetIds: ["ASSET-001"],
    overlaps: [],
    requiresOverlapAcknowledgement: false,
    ...overrides,
  };
}

function createSavedState(overrides: Partial<AssetsSavedViewState> = {}): AssetsSavedViewState {
  return {
    stateVersion: 1,
    viewMode: "table",
    searchTerm: "",
    statusFilter: "",
    warehouseFilter: "",
    hideRemovedStock: true,
    selectedDivision: "01,02",
    advancedFilters: {},
    columnFilters: {},
    sortField: "id",
    sortDirection: "asc",
    ...overrides,
  };
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, reject, resolve };
}

function getRingfenceProps(): AssetRingfenceActionsProps {
  return childMock.ringfenceProps as AssetRingfenceActionsProps;
}

function getTableProps(): AssetsTableProps {
  return childMock.tableProps as AssetsTableProps;
}

function getTimelineProps(): AssetsTimelineProps {
  return childMock.timelineProps as AssetsTimelineProps;
}

function getSavedViewOptions(): UseAssetSavedViewsOptions {
  return savedViewsMock.options as UseAssetSavedViewsOptions;
}

function LocationOutput() {
  const location = useLocation();
  return <output data-testid="current-location">{`${location.pathname}${location.search}`}</output>;
}

function renderAssetsPage(initialEntry = "/assets") {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <AssetsPage />
      <LocationOutput />
    </MemoryRouter>,
  );
}

const initialQuery = {
  search: undefined,
  statuses: undefined,
  warehouse: undefined,
  division: "01,02",
  excludeStatuses: "RemovedStock,Scrap,Sold",
};

describe("AssetsPage", () => {
  beforeEach(async () => {
    savedViewsMock.useRealHook = false;
    localStorage.clear();
    invalidateCachedAssetLists();
    sessionStorage.clear();
    vi.resetAllMocks();
    childMock.controlsProps = null;
    childMock.ringfenceProps = null;
    childMock.savedViewProps = null;
    childMock.tableProps = null;
    childMock.timelineProps = null;
    savedViewsMock.options = null;
    savedViewsMock.controller.isSelectedViewReceived = false;
    authMock.value.user.isReadOnly = false;
    mockedFetchAssets.mockResolvedValue([]);
    mockedFetchAssetEvents.mockResolvedValue({});
    mockedFetchDivisions.mockResolvedValue([
      { code: "01", name: "North" },
      { code: "02", name: "South" },
    ]);
    mockedFetchRingfences.mockResolvedValue([createRingfence(7)]);
    mockedPreflightRingfenceItems.mockResolvedValue(createBatchResult({ addedAssetIds: [] }));
    mockedAddRingfenceItems.mockResolvedValue(createBatchResult());
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(2026, 7, 14, 12, 0, 0));
    await i18n.changeLanguage("en");
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it("waits for the default view and reuses its cached query on remount without requesting initial divisions", async () => {
    savedViewsMock.useRealHook = true;
    const views = createDeferred<SavedViewsListResult<AssetsSavedViewState>>();
    vi.mocked(listSavedViews).mockReturnValue(views.promise);
    mockedFetchAssets.mockResolvedValue([createAsset("CACHED")]);
    const { unmount } = renderAssetsPage();
    expect(mockedFetchAssets).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Refresh assets" }));
    expect(mockedFetchAssets).not.toHaveBeenCalled();
    await act(async () =>
      views.resolve({
        source: "api",
        views: [
          {
            id: "1",
            name: "All Assets",
            page: "assets",
            scope: "global",
            owner: "planner",
            isOwner: false,
            canEdit: false,
            canDelete: false,
            isDefault: true,
            source: "api",
            recipients: [],
            state: createSavedState({ selectedDivision: "" }),
          },
        ],
      }),
    );
    await screen.findByTestId("assets-table");
    expect(mockedFetchAssets).toHaveBeenCalledExactlyOnceWith({ ...initialQuery, division: undefined });
    unmount();
    renderAssetsPage();
    await screen.findByTestId("assets-table");
    expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("CACHED");
    expect(mockedFetchAssets).toHaveBeenCalledOnce();
  });

  it("restores working state before the first request without issuing a default-state request", async () => {
    writeSessionPageState(
      "developer@example.com",
      "assets",
      createSavedState({ searchTerm: "restored", currentPage: 2, pageSize: 50 }),
    );
    mockedFetchAssets.mockResolvedValue(Array.from({ length: 60 }, (_, index) => createAsset(`ASSET-${index + 1}`)));

    renderAssetsPage();
    await screen.findByTestId("assets-table");

    expect(mockedFetchAssets).toHaveBeenCalledOnce();
    expect(mockedFetchAssets).toHaveBeenCalledWith(expect.objectContaining({ search: "restored" }));
    expect(getTableProps()).toMatchObject({ currentPage: 2, pageSize: 50, totalPages: 2 });
  });

  it("restores an active column-filter draft from the cached result after the assets page remounts", async () => {
    mockedFetchAssets.mockResolvedValue([createAsset("ASSET-001"), createAsset("ASSET-002")]);
    const { unmount } = renderAssetsPage();
    await screen.findByTestId("assets-table");

    act(() => getTableProps().onColumnFilterDraftChange?.("id", "002"));
    unmount();
    const rawState = sessionStorage.getItem(getSessionPageStateKey("developer@example.com", "assets"));
    const envelope = JSON.parse(rawState ?? "null") as { state?: AssetsSavedViewState } | null;
    expect(envelope?.state?.columnFilters.id).toEqual(["002"]);

    mockedFetchAssets.mockClear();
    renderAssetsPage();
    await screen.findByTestId("assets-table");

    expect(mockedFetchAssets).not.toHaveBeenCalled();
    expect(getTableProps().columnFilters.id).toEqual(["002"]);
    expect(getTableProps().assets.map((asset) => asset.id)).toEqual(["ASSET-002"]);
  });

  it("shows the received-view empty explanation only after a successful load", async () => {
    savedViewsMock.controller.isSelectedViewReceived = true;
    const { unmount } = renderAssetsPage();
    const explanation = await screen.findByText(/outside your divisions/i);
    expect(explanation).toBeInTheDocument();
    expect(explanation.closest("[data-print-hidden]")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reset filters" })).toBeInTheDocument();

    unmount();
    invalidateCachedAssetLists();
    mockedFetchAssets.mockRejectedValue(new Error("failed"));
    renderAssetsPage();
    await waitFor(() => expect(mockedFetchAssets).toHaveBeenCalled());
    expect(screen.queryByText(/outside your divisions/i)).not.toBeInTheDocument();
  });

  it("keeps asset browsing available without exposing ringfence mutations to read-only users", async () => {
    authMock.value.user.isReadOnly = true;
    mockedFetchAssets.mockResolvedValue([createAsset("READ-ONLY-ASSET")]);

    renderAssetsPage();

    await waitFor(() => expect(mockedFetchAssets).toHaveBeenCalled());
    expect(screen.getByTestId("assets-table")).toBeInTheDocument();
    expect(screen.queryByTestId("asset-ringfence-actions")).not.toBeInTheDocument();
    expect(getTableProps().selectionEnabled).toBe(false);
    expect(getSavedViewOptions().canPersistViews).toBe(false);
  });

  it("keeps the latest Asset list when an older request completes last", async () => {
    const staleRequest = createDeferred<Asset[]>();
    const latestRequest = createDeferred<Asset[]>();
    mockedFetchAssets.mockReturnValueOnce(staleRequest.promise).mockReturnValueOnce(latestRequest.promise);

    renderAssetsPage();
    await waitFor(() => expect(mockedFetchAssets).toHaveBeenCalledOnce());

    fireEvent.click(screen.getByRole("button", { name: "Set search" }));
    await waitFor(() => expect(mockedFetchAssets).toHaveBeenCalledTimes(2));
    expect(mockedFetchAssets).toHaveBeenNthCalledWith(1, initialQuery);
    expect(mockedFetchAssets).toHaveBeenNthCalledWith(2, { ...initialQuery, search: "latest" });

    await act(async () => {
      latestRequest.resolve([createAsset("LATEST")]);
      await latestRequest.promise;
    });
    await screen.findByTestId("assets-table");
    expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("LATEST");

    await act(async () => {
      staleRequest.resolve([createAsset("STALE")]);
      await staleRequest.promise;
    });
    expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("LATEST");
    expect(screen.getByTestId("table-asset-ids")).not.toHaveTextContent("STALE");
    expect(screen.getByTestId("asset-count")).toHaveTextContent("1 assets");
  });

  it("keeps loaded rows visible while a saved-view or filter refresh is pending", async () => {
    const pendingRefresh = createDeferred<Asset[]>();
    mockedFetchAssets.mockResolvedValueOnce([createAsset("EXISTING")]).mockReturnValueOnce(pendingRefresh.promise);

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("EXISTING");

    fireEvent.click(screen.getByRole("button", { name: "Set search" }));
    await waitFor(() => expect(mockedFetchAssets).toHaveBeenCalledTimes(2));

    expect(screen.queryByRole("status", { name: "Loading assets table" })).not.toBeInTheDocument();
    expect(screen.getByTestId("assets-table")).toBeInTheDocument();
    expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("EXISTING");

    await act(async () => {
      pendingRefresh.resolve([createAsset("LATEST")]);
      await pendingRefresh.promise;
    });
    await waitFor(() => expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("LATEST"));
  });

  it("keeps the latest Asset events when an older timeline request completes last", async () => {
    const asset = createAsset("ASSET-001");
    const staleEvents = createDeferred<Record<string, AssetEvent[]>>();
    const latestEvents = createDeferred<Record<string, AssetEvent[]>>();
    const refreshedAssets = createDeferred<Asset[]>();
    mockedFetchAssets.mockResolvedValueOnce([asset]).mockReturnValueOnce(refreshedAssets.promise);
    mockedFetchAssetEvents.mockReturnValueOnce(staleEvents.promise).mockReturnValueOnce(latestEvents.promise);

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    fireEvent.click(screen.getByRole("button", { name: "Show timeline" }));
    await waitFor(() => expect(mockedFetchAssetEvents).toHaveBeenCalledOnce());

    fireEvent.click(screen.getByRole("button", { name: "Refresh assets" }));
    await waitFor(() => expect(mockedFetchAssets).toHaveBeenCalledTimes(2));
    await act(async () => {
      refreshedAssets.resolve([asset]);
      await refreshedAssets.promise;
    });
    await waitFor(() => expect(mockedFetchAssetEvents).toHaveBeenCalledTimes(2));

    await act(async () => {
      latestEvents.resolve({
        "ASSET-001": [createEvent({ eventType: "SERVICE", cssClass: "service_event", title: "Latest" })],
      });
      await latestEvents.promise;
    });
    await screen.findByTestId("assets-timeline");
    expect(screen.getByTestId("timeline-row-ids")).toHaveTextContent(/ASSET-001::event-service::/);

    await act(async () => {
      staleEvents.resolve({ "ASSET-001": [createEvent({ title: "Stale" })] });
      await staleEvents.promise;
    });
    expect(screen.getByTestId("timeline-row-ids")).toHaveTextContent(/ASSET-001::event-service::/);
    expect(screen.getByTestId("timeline-row-ids")).not.toHaveTextContent("event-onhire");
  });

  it("preserves the existing list on failure and exposes safe retry controls", async () => {
    mockedFetchAssets
      .mockResolvedValueOnce([createAsset("EXISTING")])
      .mockRejectedValueOnce(new Error("Sensitive Asset service failure"));

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("EXISTING");
    fireEvent.click(screen.getByRole("button", { name: "Refresh assets" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("An error occurred");
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reset filters" })).toBeDisabled();
    expect(screen.getByTestId("asset-count")).toHaveTextContent("1 assets");
    expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("EXISTING");
    expect(screen.queryByText("Sensitive Asset service failure")).not.toBeInTheDocument();
    expect(screen.getByTestId("assets-table")).toBeInTheDocument();
    expect(screen.queryByTestId("assets-timeline")).not.toBeInTheDocument();
    expect(mockedFetchAssets).toHaveBeenCalledTimes(2);
    expect(mockedFetchAssets).toHaveBeenNthCalledWith(1, initialQuery);
    expect(mockedFetchAssets).toHaveBeenNthCalledWith(2, initialQuery);
  });

  it("lets a user reset a failed search and return to the default list", async () => {
    const existing = createAsset("EXISTING");
    mockedFetchAssets
      .mockResolvedValueOnce([existing])
      .mockRejectedValueOnce(new Error("Search unavailable"))
      .mockResolvedValueOnce([existing]);

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    fireEvent.click(screen.getByRole("button", { name: "Set search" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("An error occurred");
    expect(screen.getByRole("button", { name: "Reset asset filters" })).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: "Reset asset filters" }));

    await waitFor(() => expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("EXISTING"));
    expect(mockedFetchAssets).toHaveBeenNthCalledWith(2, { ...initialQuery, search: "latest" });
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("EXISTING");
  });

  it("keeps the table mounted so an empty column filter can be edited and cleared", async () => {
    mockedFetchAssets.mockResolvedValue([createAsset("ASSET-001", { description: "Generator" })]);

    renderAssetsPage();
    await screen.findByTestId("assets-table");

    act(() => getTableProps().onColumnFilterChange("description", "dhuvbad"));

    expect(screen.getByTestId("assets-table")).toBeInTheDocument();
    expect(screen.getAllByText("No results found")).not.toHaveLength(0);
    expect(getTableProps().assets).toHaveLength(0);
    expect(getTableProps().columnFilters.description).toBe("dhuvbad");

    act(() => getTableProps().onColumnFilterChange("description", ""));

    expect(screen.getByTestId("assets-table")).toBeInTheDocument();
    expect(getTableProps().assets).toHaveLength(1);
  });

  it("keeps the top-row and column-header status filters synchronized across both views", async () => {
    mockedFetchAssets.mockResolvedValue([
      createAsset("ASSET-001", { status: "Available" }),
      createAsset("ASSET-002", { status: "Repair" }),
      createAsset("ASSET-003", { status: "Service" }),
    ]);

    renderAssetsPage();
    await screen.findByTestId("assets-table");

    act(() => getTableProps().onStatusFilterChange("Available,Repair"));
    await waitFor(() => expect((childMock.controlsProps as AssetControlsProps).statusFilter).toBe("Available,Repair"));
    expect(getTableProps().statusFilter).toBe("Available,Repair");
    expect(getTableProps().assets.map((asset) => asset.id)).toEqual(["ASSET-001", "ASSET-002"]);

    fireEvent.click(screen.getByRole("button", { name: "Show timeline" }));
    await screen.findByTestId("assets-timeline");
    expect(getTimelineProps().statusFilter).toBe("Available,Repair");
    expect(getTimelineProps().rows.map((row) => row.asset.id)).toEqual(["ASSET-001", "ASSET-002"]);

    act(() => getTimelineProps().onStatusFilterChange("Service"));
    await waitFor(() => expect((childMock.controlsProps as AssetControlsProps).statusFilter).toBe("Service"));
    expect(getTimelineProps().statusFilter).toBe("Service");
    expect(getTimelineProps().rows.map((row) => row.asset.id)).toEqual(["ASSET-003"]);

    fireEvent.click(screen.getByRole("button", { name: "Show table" }));
    expect(getTableProps().statusFilter).toBe("Service");
    expect(getTableProps().assets.map((asset) => asset.id)).toEqual(["ASSET-003"]);
  });

  it("supplies Asset WHS choices and synchronizes the multi-select DIV filter across both views", async () => {
    mockedFetchAssets.mockResolvedValue([
      createAsset("ASSET-001", { division: "01", warehouse: "GLA" }),
      createAsset("ASSET-002", { division: "02", warehouse: "MAN" }),
      createAsset("ASSET-003", { division: "02", warehouse: "EDI" }),
    ]);

    renderAssetsPage();
    await screen.findByTestId("assets-table");

    expect(getTableProps().divisionOptions).toEqual([
      { label: "01", value: "01" },
      { label: "02", value: "02" },
    ]);
    expect(getTableProps().warehouseOptions).toEqual([
      { label: "EDI", value: "EDI" },
      { label: "GLA", value: "GLA" },
      { label: "MAN", value: "MAN" },
    ]);

    act(() => getTableProps().onDivisionFilterChange("02"));
    await waitFor(() => expect((childMock.controlsProps as AssetControlsProps).selectedDivision).toBe("02"));
    expect(getTableProps().divisionFilter).toBe("02");
    expect(getTableProps().assets.map((asset) => asset.id)).toEqual(["ASSET-002", "ASSET-003"]);
    expect(getTableProps().warehouseOptions).toEqual([
      { label: "EDI", value: "EDI" },
      { label: "MAN", value: "MAN" },
    ]);

    fireEvent.click(screen.getByRole("button", { name: "Show timeline" }));
    await screen.findByTestId("assets-timeline");
    expect(getTimelineProps().divisionFilter).toBe("02");
    expect(getTimelineProps().divisionOptions).toEqual(getTableProps().divisionOptions);
    expect(getTimelineProps().warehouseOptions).toEqual(getTableProps().warehouseOptions);

    act(() => getTimelineProps().onDivisionFilterChange("01,02"));
    await waitFor(() => expect((childMock.controlsProps as AssetControlsProps).selectedDivision).toBe("01,02"));
    fireEvent.click(screen.getByRole("button", { name: "Show table" }));
    expect(getTableProps().divisionFilter).toBe("01,02");
  });

  it("ignores the retired top warehouse/location value in legacy saved state", async () => {
    writeSessionPageState("developer@example.com", "assets", createSavedState({ warehouseFilter: "GLA" }));
    mockedFetchAssets.mockResolvedValue([
      createAsset("ASSET-001", { warehouse: "GLA" }),
      createAsset("ASSET-002", { warehouse: "MAN" }),
    ]);

    renderAssetsPage();
    await screen.findByTestId("assets-table");

    expect(mockedFetchAssets).toHaveBeenCalledWith(expect.objectContaining({ warehouse: undefined }));
    expect(getTableProps().assets.map((asset) => asset.id)).toEqual(["ASSET-001", "ASSET-002"]);
    expect(getSavedViewOptions().captureState().warehouseFilter).toBe("");
  });

  it("promotes a legacy Asset status column filter into the shared status filter", async () => {
    writeSessionPageState(
      "developer@example.com",
      "assets",
      createSavedState({ columnFilters: { status: ["Repair", "Service"] } }),
    );
    mockedFetchAssets.mockResolvedValue([createAsset("ASSET-001")]);

    renderAssetsPage();
    await screen.findByTestId("assets-table");

    expect(mockedFetchAssets).toHaveBeenCalledWith(expect.objectContaining({ statuses: "Repair,Service" }));
    expect((childMock.controlsProps as AssetControlsProps).statusFilter).toBe("Repair,Service");
    expect(getTableProps().statusFilter).toBe("Repair,Service");
    expect(getTableProps().columnFilters.status).toBeUndefined();
  });

  it("fetches events only in timeline mode with the exact period/divisions and keeps failures as no-event rows", async () => {
    mockedFetchAssets.mockResolvedValue([createAsset("ASSET-001")]);
    mockedFetchAssetEvents.mockRejectedValue(new Error("Sensitive Event service failure"));

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    expect(mockedFetchAssetEvents).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Show timeline" }));
    await screen.findByTestId("assets-timeline");

    expect(mockedFetchAssetEvents).toHaveBeenCalledOnce();
    expect(mockedFetchAssetEvents).toHaveBeenCalledWith({
      startDate: "2025-08-14",
      endDate: "2026-10-31",
      divisions: "01,02",
    });
    expect(screen.getByTestId("timeline-row-ids")).toHaveTextContent("ASSET-001::none");
    expect(screen.getByTestId("timeline-task-ids")).toHaveTextContent("ASSET-001::none");
    expect(getTimelineProps().rows[0]).toMatchObject({
      rowId: "ASSET-001::none",
      event: null,
      eventClass: "event-none",
      eventLabel: "",
    });
    expect(getTimelineProps().tasks[0]).toMatchObject({
      id: "ASSET-001::none",
      name: "",
      progress: 0,
      custom_class: "event-none",
    });
    expect(screen.queryByText("Sensitive Event service failure")).not.toBeInTheDocument();
  });

  it("does not mount a transient timeline before its initial events have loaded", async () => {
    const events = createDeferred<Record<string, AssetEvent[]>>();
    mockedFetchAssets.mockResolvedValue([createAsset("ASSET-001")]);
    mockedFetchAssetEvents.mockReturnValue(events.promise);

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    fireEvent.click(screen.getByRole("button", { name: "Show timeline" }));
    await waitFor(() => expect(mockedFetchAssetEvents).toHaveBeenCalledOnce());

    expect(childMock.timelineProps).toBeNull();
    expect(screen.queryByTestId("assets-timeline")).not.toBeInTheDocument();

    await act(async () => {
      events.resolve({});
      await events.promise;
    });
    await screen.findByTestId("assets-timeline");
  });

  it("clears selected Assets and resets table paging when saved state is applied", async () => {
    const assets = Array.from({ length: 51 }, (_, index) => createAsset(`ASSET-${String(index + 1).padStart(3, "0")}`));
    mockedFetchAssets.mockResolvedValue(assets);

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    fireEvent.click(screen.getByRole("button", { name: "Next table page" }));
    expect(screen.getByTestId("table-page")).toHaveTextContent("Page 2 of 2");
    expect(screen.getByTestId("table-asset-ids")).toHaveTextContent("ASSET-051");

    fireEvent.click(screen.getByRole("button", { name: "Select first table asset" }));
    fireEvent.click(screen.getByRole("button", { name: "Target ringfence 7" }));
    expect(screen.getByTestId("table-selected-count")).toHaveTextContent("1");
    expect(screen.getByTestId("ringfence-selection-count")).toHaveTextContent("1");
    expect(screen.getByTestId("ringfence-target")).toHaveTextContent("7");

    const savedColumns = moveTableColumn(
      ASSET_COLUMN_CATALOG,
      repairTableColumnLayout(ASSET_COLUMN_CATALOG),
      "description",
      "left",
    );
    const savedTimelineColumns = repairTableColumnLayout(ASSET_TIMELINE_COLUMN_CATALOG).map((column) =>
      column.key === "warehouse" ? { ...column, visible: false } : column,
    );
    act(() =>
      getSavedViewOptions().applyState(
        createSavedState({ columns: savedColumns, timelineColumns: savedTimelineColumns }),
      ),
    );

    await waitFor(() => expect(screen.getByTestId("table-page")).toHaveTextContent("Page 1 of 2"));
    expect(screen.getByTestId("table-selected-count")).toHaveTextContent("0");
    expect(screen.getByTestId("ringfence-selection-count")).toHaveTextContent("0");
    expect(screen.getByTestId("ringfence-target")).toHaveTextContent("7");
    expect(getTableProps().columnLayout).toEqual(savedColumns);
    expect(getSavedViewOptions().captureState().columns).toEqual(savedColumns);
    expect(getSavedViewOptions().captureState().timelineColumns).toEqual(savedTimelineColumns);
  });

  it("clears a column filter when its column is hidden", async () => {
    const user = userEvent.setup();
    renderAssetsPage();
    await screen.findByTestId("assets-table");

    act(() => getTableProps().onColumnFilterChange("description", "boom"));
    await waitFor(() => expect(getTableProps().columnFilters.description).toBe("boom"));
    await user.click(screen.getByRole("button", { name: "Columns, 13 visible" }));
    await user.click(screen.getByRole("checkbox", { name: "Description" }));

    await waitFor(() => expect(getTableProps().columnFilters.description).toBeUndefined());
    expect(getTableProps().columnLayout.find((column) => column.key === "description")?.visible).toBe(false);
  });

  it("clears a timeline filter when its context column is hidden", async () => {
    const user = userEvent.setup();
    renderAssetsPage();
    await screen.findByTestId("assets-table");
    fireEvent.click(screen.getByRole("button", { name: "Show timeline" }));
    await screen.findByTestId("assets-timeline");

    act(() => getTimelineProps().onColumnFilterChange("warehouse", "GLA"));
    await waitFor(() => expect(getTimelineProps().columnFilters.warehouse).toBe("GLA"));
    await user.click(screen.getByRole("button", { name: "Columns, 4 visible" }));
    expect(screen.getByRole("checkbox", { name: "Description" })).toBeEnabled();
    await user.click(screen.getByRole("checkbox", { name: "WHS" }));

    await waitFor(() => expect(getTimelineProps().columnFilters.warehouse).toBeUndefined());
    expect(getTimelineProps().columnLayout.find((column) => column.key === "warehouse")?.visible).toBe(false);
  });

  it("keeps table and timeline paging client-side and publishes encoded profile paths", async () => {
    const assets = Array.from({ length: 51 }, (_, index) => createAsset(`ASSET-${String(index + 1).padStart(3, "0")}`));
    mockedFetchAssets.mockResolvedValue(assets);

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    expect(getTableProps().getAssetProfilePath("A/B C?")).toBe("/assets/A%2FB%20C%3F");

    fireEvent.click(screen.getByRole("button", { name: "Next table page" }));
    expect(screen.getByTestId("table-page")).toHaveTextContent("Page 2 of 2");
    expect(getTableProps().assets.map((asset) => asset.id)).toEqual(["ASSET-051"]);

    fireEvent.click(screen.getByRole("button", { name: "Show timeline" }));
    await screen.findByTestId("assets-timeline");
    expect(screen.getByTestId("timeline-page")).toHaveTextContent("Page 1 of 2");
    expect(getTimelineProps().rows).toHaveLength(50);
    expect(getTimelineProps().getAssetProfilePath("A/B C?")).toBe("/assets/A%2FB%20C%3F");

    fireEvent.click(screen.getByRole("button", { name: "Next timeline page" }));
    expect(screen.getByTestId("timeline-page")).toHaveTextContent("Page 2 of 2");
    expect(getTimelineProps().rows.map((row) => row.asset.id)).toEqual(["ASSET-051"]);

    fireEvent.click(screen.getByRole("button", { name: "Show table" }));
    expect(screen.getByTestId("table-page")).toHaveTextContent("Page 2 of 2");
    expect(mockedFetchAssets).toHaveBeenCalledOnce();
  });

  it("changes table rows per page and returns to the first page", async () => {
    const assets = Array.from({ length: 251 }, (_, index) =>
      createAsset(`ASSET-${String(index + 1).padStart(3, "0")}`),
    );
    mockedFetchAssets.mockResolvedValue(assets);

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    expect(screen.getByRole("combobox", { name: "Rows per page" })).toHaveValue("50");
    expect(getTableProps().assets).toHaveLength(50);
    expect(screen.getByTestId("table-page")).toHaveTextContent("Page 1 of 6");

    fireEvent.click(screen.getByRole("button", { name: "Next table page" }));
    expect(screen.getByTestId("table-page")).toHaveTextContent("Page 2 of 6");

    fireEvent.change(screen.getByRole("combobox", { name: "Rows per page" }), { target: { value: "250" } });
    expect(screen.getByTestId("table-page")).toHaveTextContent("Page 1 of 2");
    expect(getTableProps().pageSize).toBe(250);
    expect(getTableProps().assets).toHaveLength(250);
  });

  it("keeps ringfence actions available and selects assets in the timeline", async () => {
    mockedFetchAssets.mockResolvedValue([createAsset("ASSET-001")]);

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    fireEvent.click(screen.getByRole("button", { name: "Show timeline" }));
    await screen.findByTestId("assets-timeline");

    expect(screen.getByTestId("asset-ringfence-actions")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Select first timeline asset" }));
    expect(screen.getByTestId("timeline-selected-count")).toHaveTextContent("1");
    expect(screen.getByTestId("ringfence-selection-count")).toHaveTextContent("1");
  });

  it("preflights the selected assets, adds them as one batch, and clears selection on success", async () => {
    mockedFetchAssets.mockResolvedValue([createAsset("ASSET-001")]);
    const mutation = createDeferred<RingfenceItemBatchResult>();
    mockedAddRingfenceItems.mockReturnValue(mutation.promise);

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    fireEvent.click(screen.getByRole("button", { name: "Select first table asset" }));
    expect(getRingfenceProps().selectedAssetCount).toBe(1);

    fireEvent.click(screen.getByRole("button", { name: "Target ringfence 7" }));
    expect(getRingfenceProps().selectedRingfenceId).toBe(7);
    fireEvent.click(screen.getByRole("button", { name: "Add selected assets" }));

    await waitFor(() => expect(mockedPreflightRingfenceItems).toHaveBeenCalledWith(7, ["ASSET-001"]));
    await waitFor(() => expect(mockedAddRingfenceItems).toHaveBeenCalledWith(7, ["ASSET-001"], false));
    expect(screen.getByTestId("ringfence-busy")).toHaveTextContent("true");
    expect(screen.getByTestId("ringfence-selection-count")).toHaveTextContent("1");
    await act(async () => {
      mutation.resolve(createBatchResult());
      await mutation.promise;
    });
    await waitFor(() => expect(screen.getByTestId("ringfence-selection-count")).toHaveTextContent("0"));
    expect(screen.getByTestId("ringfence-busy")).toHaveTextContent("false");
    expect(screen.getByTestId("ringfence-target")).toHaveTextContent("7");
  });

  it("reads only a valid contextual Ringfence handoff from the query string", () => {
    expect(parseRingfenceAssetsContext("?ringfenceId=7&returnTo=%2Fringfence%3FringfenceId%3D7")).toEqual({
      ringfenceId: 7,
      returnTo: "/ringfence?ringfenceId=7",
    });
    expect(parseRingfenceAssetsContext("?ringfenceId=0&returnTo=%2Fringfence")).toEqual({
      ringfenceId: null,
      returnTo: null,
    });
    expect(parseRingfenceAssetsContext("?ringfenceId=7&returnTo=https%3A%2F%2Fexample.com")).toEqual({
      ringfenceId: 7,
      returnTo: null,
    });
  });

  it("preselects the contextual Ringfence after loading and returns there after a successful add", async () => {
    mockedFetchAssets.mockResolvedValue([createAsset("ASSET-001")]);

    renderAssetsPage("/assets?ringfenceId=7&returnTo=%2Fringfence%3FringfenceId%3D7");
    await screen.findByTestId("assets-table");

    await waitFor(() => expect(screen.getByTestId("ringfence-target")).toHaveTextContent("7"));
    expect(screen.getByTestId("ringfence-return-path")).toHaveTextContent("/ringfence?ringfenceId=7");
    expect(screen.getByTestId("ringfence-is-contextual")).toHaveTextContent("true");

    fireEvent.click(screen.getByRole("button", { name: "Select first table asset" }));
    fireEvent.click(screen.getByRole("button", { name: "Add selected assets" }));

    await waitFor(() => expect(mockedPreflightRingfenceItems).toHaveBeenCalledWith(7, ["ASSET-001"]));
    await waitFor(() => expect(mockedAddRingfenceItems).toHaveBeenCalledWith(7, ["ASSET-001"], false));
    await waitFor(() => expect(screen.getByTestId("current-location")).toHaveTextContent("/ringfence?ringfenceId=7"));
  });

  it("clears and explains a contextual Ringfence that is no longer available", async () => {
    mockedFetchRingfences.mockResolvedValue([]);

    renderAssetsPage("/assets?ringfenceId=99&returnTo=%2Fringfence%3FringfenceId%3D99");
    await screen.findByTestId("assets-table");

    await waitFor(() => expect(screen.getByTestId("ringfence-target")).toBeEmptyDOMElement());
    expect(
      screen.getByText("The requested Ringfence is no longer available. Select another target."),
    ).toBeInTheDocument();
  });

  it("clears a stale target and explains when Ringfence targets cannot be refreshed", async () => {
    const refresh = createDeferred<RingfenceListItem[]>();
    mockedFetchRingfences.mockReturnValue(refresh.promise);

    renderAssetsPage();
    fireEvent.click(screen.getByRole("button", { name: "Target ringfence 7" }));
    expect(screen.getByTestId("ringfence-target")).toHaveTextContent("7");
    refresh.reject(new Error("network unavailable"));

    await waitFor(() => expect(screen.getByTestId("ringfence-target")).toBeEmptyDOMElement());
    expect(
      screen.getByText("Ringfence targets could not be refreshed. Select a target after trying again."),
    ).toBeInTheDocument();
  });

  it("requires an explicit acknowledgement before adding assets with an overlap", async () => {
    mockedFetchAssets.mockResolvedValue([createAsset("ASSET-001")]);
    mockedPreflightRingfenceItems.mockResolvedValue(
      createBatchResult({
        addedAssetIds: [],
        overlaps: [
          {
            ringfenceId: 12,
            assetIds: ["ASSET-001"],
            title: "Existing reserve",
            fromDate: "2026-08-10T00:00:00.000Z",
            toDate: "2026-08-20T00:00:00.000Z",
            owner: "other@example.com",
          },
        ],
        requiresOverlapAcknowledgement: true,
      }),
    );

    renderAssetsPage();
    await screen.findByTestId("assets-table");
    fireEvent.click(screen.getByRole("button", { name: "Select first table asset" }));
    fireEvent.click(screen.getByRole("button", { name: "Target ringfence 7" }));
    fireEvent.click(screen.getByRole("button", { name: "Add selected assets" }));

    await waitFor(() => expect(mockedPreflightRingfenceItems).toHaveBeenCalledWith(7, ["ASSET-001"]));
    expect(mockedAddRingfenceItems).not.toHaveBeenCalled();
    fireEvent.click(screen.getByText("Add despite overlap"));
    await waitFor(() => expect(mockedAddRingfenceItems).toHaveBeenCalledWith(7, ["ASSET-001"], true));
  });
});
