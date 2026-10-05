import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type FC } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../contexts/auth";
import { Alert, Button, Spinner, TableColumnsMenu, TableSkeleton } from "../../components/common";
import type { DateColumnFilterValue } from "../../components/common/TableColumnControls/dateColumnFilterModel";
import {
  applyMultiValueTextFilterDrafts,
  toMultiValueTextFilter,
  type MultiValueTextFilterDrafts,
} from "../../components/common/TableColumnControls/multiValueTextFilterModel";
import { clearSessionPageState, readSessionPageState, writeSessionPageState } from "../../lib/sessionPageState";
import {
  haveSameFilterSelection,
  normalizeFilterSelection,
  parseFilterSelection,
  serializeFilterSelection,
} from "../../lib/filterSelection";
import type { GanttTask } from "../../components/timeline/FrappeGantt";
import {
  repairTableColumnLayout,
  setTableColumnVisibility,
  type TableColumnLayoutItem,
} from "../../lib/tableColumnLayout";
import { fetchAgreements, type AgreementListItem } from "../../services/agreementsService";
import { fetchDivisions, type DivisionLookup } from "../../services/lookupsService";
import {
  parseAgreementsSavedViewState,
  MAX_VISIBLE_TIMELINE_COLUMNS,
  type AgreementsSavedViewState,
  type AgreementsSortField,
  type SavedColumnFilterValue,
} from "../../types/savedViews";
import { AgreementControls } from "./AgreementControls";
import {
  AGREEMENT_COLUMN_CATALOG,
  AGREEMENT_TIMELINE_COLUMN_CATALOG,
  type AgreementColumnKey,
  type AgreementTimelineColumnKey,
} from "./agreementColumnCatalog";
import { AgreementSavedViewControls } from "./AgreementSavedViewControls";
import { AgreementsTable } from "./AgreementsTable";
import { AgreementsTimeline } from "./AgreementsTimeline";
import { filterAgreements, sortAgreements } from "./agreementsListModel";
import { buildAgreementFilterParams } from "./agreementsQuery";
import {
  buildAgreementTimelineTasks,
  filterAgreementTimelineDateRange,
  filterAgreementTimelineAgreements,
  filterAgreementTimelineHistory,
  getAgreementTimelineHistoryStart,
  type AgreementTimelineColumnFilters,
  type AgreementTimelineFilterField,
} from "./agreementsTimelineModel";
import { useAgreementSavedViews } from "./useAgreementSavedViews";
import styles from "./AgreementsPage.module.css";

type SortField = AgreementsSortField;
type SortDirection = "asc" | "desc";

const AGREEMENT_ORDER_TYPES = ["quote", "temporaryAgreement", "agreement"] as const;

function toOrderTypeFilter(value: string): string {
  return normalizeFilterSelection(value, AGREEMENT_ORDER_TYPES);
}

function removeRetiredColumnFilters(
  filters: Readonly<Partial<Record<SortField, SavedColumnFilterValue>>>,
): Partial<Record<SortField, SavedColumnFilterValue>> {
  const supportedFilters = { ...filters };
  delete supportedFilters.division;
  delete supportedFilters.fulfilmentStatus;
  return supportedFilters;
}

function removeSyncedAgreementTimelineFilters(
  filters: Readonly<AgreementTimelineColumnFilters>,
): AgreementTimelineColumnFilters {
  const supportedFilters = { ...filters };
  delete supportedFilters.fulfilmentStatus;
  return supportedFilters;
}

function getAgreementStatusFilter(state: AgreementsSavedViewState | null): string {
  if (!state) return "";
  if (state.statusFilter) return state.statusFilter;
  return serializeFilterSelection(
    toMultiValueTextFilter(state.columnFilters.fulfilmentStatus ?? state.timelineColumnFilters?.fulfilmentStatus),
  );
}

export const AgreementsPage: FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const restoredWorkingState = useMemo(
    () => readSessionPageState(user?.loginName, "agreements", parseAgreementsSavedViewState),
    [user?.loginName],
  );

  const [agreements, setAgreements] = useState<AgreementListItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<"table" | "timeline">(() => restoredWorkingState?.viewMode ?? "table");

  // Basic filters
  const [searchTerm, setSearchTerm] = useState(() => restoredWorkingState?.searchTerm ?? "");
  const [showHistorical, setShowHistorical] = useState<boolean>(() => {
    if (restoredWorkingState) return restoredWorkingState.showHistorical;
    if (typeof window === "undefined") {
      return false;
    }

    const persisted =
      window.localStorage.getItem("agreements.showHistorical") ?? window.localStorage.getItem("orders.showHistorical");
    if (persisted != null) {
      return persisted === "true";
    }

    // Migrate from prior hide-based preference if present.
    const previousHideValue = window.localStorage.getItem("orders.hideHistorical");
    if (previousHideValue != null) {
      return previousHideValue !== "true";
    }

    return false;
  });
  const [selectedDivision, setSelectedDivision] = useState<string>(() => {
    if (restoredWorkingState) return serializeFilterSelection(restoredWorkingState.selectedDivision.split(","));
    if (user?.isSuperAdmin) {
      return "";
    }

    const divs =
      user?.division
        ?.split(",")
        .map((d) => d.trim())
        .filter(Boolean) ?? [];
    return divs.length === 1 ? divs[0] : "";
  });
  const [orderTypeFilter, setOrderTypeFilter] = useState<string>(() =>
    toOrderTypeFilter(restoredWorkingState?.orderTypeFilter ?? ""),
  );
  const [statusFilter, setStatusFilter] = useState<string>(() => getAgreementStatusFilter(restoredWorkingState));

  // Pagination
  const [currentPage, setCurrentPage] = useState(() => restoredWorkingState?.currentPage ?? 1);
  const [timelinePage, setTimelinePage] = useState(() => restoredWorkingState?.timelinePage ?? 1);
  const [pageSize, setPageSize] = useState(() => restoredWorkingState?.pageSize ?? 50);
  const timelinePageSize = 50;

  // Sorting
  const [sortField, setSortField] = useState<SortField>(() => restoredWorkingState?.sortField ?? "onHireDate");
  const [sortDirection, setSortDirection] = useState<SortDirection>(
    () => restoredWorkingState?.sortDirection ?? "desc",
  );
  const [columnFilters, setColumnFilters] = useState<Partial<Record<SortField, SavedColumnFilterValue>>>(() =>
    removeRetiredColumnFilters(restoredWorkingState?.columnFilters ?? {}),
  );
  const [columnFilterDrafts, setColumnFilterDrafts] = useState<MultiValueTextFilterDrafts<SortField>>({});
  const [dateColumnFilters, setDateColumnFilters] = useState<Partial<Record<SortField, DateColumnFilterValue>>>(
    () => restoredWorkingState?.dateColumnFilters ?? {},
  );
  const [columnLayout, setColumnLayout] = useState<TableColumnLayoutItem<AgreementColumnKey>[]>(() =>
    repairTableColumnLayout(AGREEMENT_COLUMN_CATALOG, restoredWorkingState?.columns),
  );
  const [timelineColumnLayout, setTimelineColumnLayout] = useState<TableColumnLayoutItem<AgreementTimelineColumnKey>[]>(
    () => repairTableColumnLayout(AGREEMENT_TIMELINE_COLUMN_CATALOG, restoredWorkingState?.timelineColumns),
  );
  const [timelineColumnFilters, setTimelineColumnFilters] = useState<AgreementTimelineColumnFilters>(() =>
    removeSyncedAgreementTimelineFilters(restoredWorkingState?.timelineColumnFilters ?? {}),
  );
  const [timelineColumnFilterDrafts, setTimelineColumnFilterDrafts] = useState<
    MultiValueTextFilterDrafts<AgreementTimelineFilterField>
  >({});
  const [timelineStartDate, setTimelineStartDate] = useState("");
  const [timelineEndDate, setTimelineEndDate] = useState("");
  const timelineToday = useMemo(() => new Date().toISOString().split("T")[0], []);
  const timelineHistoryStart = useMemo(() => getAgreementTimelineHistoryStart(timelineToday), [timelineToday]);

  const canPersistViews = !user?.isReadOnly;
  const canManageSharedViews = Boolean(user?.isAdmin && canPersistViews);

  const buildParams = useCallback(
    () =>
      buildAgreementFilterParams({
        showHistorical,
        selectedDivision,
        searchTerm,
        orderTypeFilter,
        statusFilter,
        advancedFilters: {},
      }),
    [showHistorical, selectedDivision, searchTerm, orderTypeFilter, statusFilter],
  );

  const loadAgreements = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await fetchAgreements(buildParams());
      setAgreements(data);
    } catch {
      setError(t("common.error"));
    } finally {
      setIsLoading(false);
    }
  }, [buildParams, t]);

  useEffect(() => {
    loadAgreements();
  }, [loadAgreements]);

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    window.localStorage.setItem("agreements.showHistorical", showHistorical ? "true" : "false");
    window.localStorage.removeItem("orders.showHistorical");
    window.localStorage.removeItem("orders.showFulfilled");
    window.localStorage.removeItem("orders.hideHistorical");
  }, [showHistorical]);

  // Division lookups from API
  const [divisionLookups, setDivisionLookups] = useState<DivisionLookup[]>([]);

  useEffect(() => {
    fetchDivisions()
      .then(setDivisionLookups)
      .catch(() => {});
  }, []);

  const divisions = useMemo(() => {
    return (
      user?.division
        ?.split(",")
        .map((d) => d.trim())
        .filter(Boolean) ?? []
    );
  }, [user]);

  const visibleDivisionLookups = useMemo(
    () =>
      user?.isSuperAdmin ? divisionLookups : divisionLookups.filter((division) => divisions.includes(division.code)),
    [divisionLookups, divisions, user?.isSuperAdmin],
  );

  const divisionOptions = useMemo(
    () => visibleDivisionLookups.map((division) => ({ label: division.code, value: division.code })),
    [visibleDivisionLookups],
  );
  const showAllDivisionOption = Boolean(user?.isSuperAdmin || divisions.length > 1);
  const defaultDivision = user?.isSuperAdmin ? "" : divisions.length === 1 ? divisions[0] : "";

  const hasActiveFilters =
    searchTerm !== "" ||
    !haveSameFilterSelection(selectedDivision, defaultDivision) ||
    orderTypeFilter !== "" ||
    statusFilter !== "" ||
    showHistorical ||
    Object.values(columnFilters).some((value) => (Array.isArray(value) ? value.length > 0 : Boolean(value))) ||
    Object.values(columnFilterDrafts).some(Boolean) ||
    Object.values(dateColumnFilters).some(Boolean) ||
    Object.values(timelineColumnFilters).some(Boolean) ||
    Object.values(timelineColumnFilterDrafts).some(Boolean);

  const effectiveColumnFilters = useMemo(
    () => applyMultiValueTextFilterDrafts(columnFilters, columnFilterDrafts),
    [columnFilterDrafts, columnFilters],
  );
  const effectiveTimelineColumnFilters = useMemo(
    () => applyMultiValueTextFilterDrafts(timelineColumnFilters, timelineColumnFilterDrafts),
    [timelineColumnFilterDrafts, timelineColumnFilters],
  );

  const warehouseOptions = useMemo(() => {
    const selectedDivisionCodes = new Set(
      selectedDivision
        .split(",")
        .map((division) => division.trim())
        .filter(Boolean),
    );
    return Array.from(
      new Set(
        agreements
          .filter(
            (agreement) => selectedDivisionCodes.size === 0 || selectedDivisionCodes.has(agreement.division.trim()),
          )
          .map((agreement) => agreement.warehouse?.trim())
          .filter((warehouse): warehouse is string => Boolean(warehouse)),
      ),
    )
      .sort((first, second) => first.localeCompare(second, undefined, { numeric: true, sensitivity: "base" }))
      .map((warehouse) => ({ label: warehouse, value: warehouse }));
  }, [agreements, selectedDivision]);

  const filteredAgreements = useMemo(() => {
    const selectedStatuses = parseFilterSelection(statusFilter);
    const filtersWithStatus =
      selectedStatuses.length > 0
        ? { ...effectiveColumnFilters, fulfilmentStatus: selectedStatuses }
        : effectiveColumnFilters;

    return filterAgreements(agreements, {
      columnFilters: filtersWithStatus,
      dateColumnFilters,
      orderTypeFilter,
      showHistorical,
    });
  }, [agreements, dateColumnFilters, effectiveColumnFilters, orderTypeFilter, showHistorical, statusFilter]);

  const sortedAgreements = useMemo(
    () => sortAgreements(filteredAgreements, sortField, sortDirection),
    [filteredAgreements, sortField, sortDirection],
  );

  const totalPages = Math.max(1, Math.ceil(sortedAgreements.length / pageSize));
  useEffect(() => {
    if (!isLoading) setCurrentPage((page) => Math.min(page, totalPages));
  }, [isLoading, totalPages]);
  const paginatedAgreements = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return sortedAgreements.slice(start, start + pageSize);
  }, [sortedAgreements, currentPage, pageSize]);

  const timelineSourceAgreements = useMemo(
    () =>
      filterAgreementTimelineHistory(
        filterAgreementTimelineDateRange(
          filterAgreementTimelineAgreements(sortedAgreements, effectiveTimelineColumnFilters),
          timelineStartDate,
          timelineEndDate,
          timelineToday,
        ),
        timelineToday,
      ),
    [effectiveTimelineColumnFilters, sortedAgreements, timelineEndDate, timelineStartDate, timelineToday],
  );

  const timelineTotalPages = Math.max(1, Math.ceil(timelineSourceAgreements.length / timelinePageSize));
  const timelineAgreements = useMemo(() => {
    const start = (timelinePage - 1) * timelinePageSize;
    return timelineSourceAgreements.slice(start, start + timelinePageSize);
  }, [timelineSourceAgreements, timelinePage]);

  useEffect(() => {
    if (!isLoading) setTimelinePage((prev) => Math.min(prev, timelineTotalPages));
  }, [isLoading, timelineTotalPages]);

  const handleSort = useCallback(
    (field: SortField) => {
      if (sortField === field) {
        setSortDirection((d) => (d === "asc" ? "desc" : "asc"));
      } else {
        setSortField(field);
        setSortDirection("asc");
      }
      setCurrentPage(1);
      setTimelinePage(1);
    },
    [sortField],
  );

  const handleRowClick = useCallback(
    (headerId: number) => {
      navigate(`/agreements/${headerId}`);
    },
    [navigate],
  );

  const handleOrderNumberClick = useCallback(
    (headerId: number) => {
      navigate(`/agreements/${headerId}/timeline`);
    },
    [navigate],
  );

  const handleColumnFilterChange = useCallback((field: SortField, value: SavedColumnFilterValue) => {
    setColumnFilters((prev) => ({ ...prev, [field]: value }));
    setCurrentPage(1);
    setTimelinePage(1);
  }, []);

  const handleColumnFilterDraftChange = useCallback((field: SortField, value: string) => {
    setColumnFilterDrafts((previous) => ({ ...previous, [field]: value }));
    setCurrentPage(1);
  }, []);

  const handleDateColumnFilterChange = useCallback((field: SortField, value: DateColumnFilterValue | undefined) => {
    setDateColumnFilters((previous) => {
      const next = { ...previous };
      if (value) next[field] = value;
      else delete next[field];
      return next;
    });
    setCurrentPage(1);
    setTimelinePage(1);
  }, []);

  const handleColumnVisibilityChange = useCallback(
    (key: AgreementColumnKey, visible: boolean) => {
      setColumnLayout((layout) => setTableColumnVisibility(AGREEMENT_COLUMN_CATALOG, layout, key, visible));
      if (!visible) {
        if (key === "division") {
          setSelectedDivision(user?.isSuperAdmin ? "" : divisions.length === 1 ? divisions[0] : "");
        }
        if (key === "fulfilmentStatus") setStatusFilter("");
        setColumnFilters((filters) => {
          const next = { ...filters };
          delete next[key];
          return next;
        });
        setColumnFilterDrafts((drafts) => {
          const next = { ...drafts };
          delete next[key];
          return next;
        });
        setDateColumnFilters((filters) => {
          const next = { ...filters };
          delete next[key];
          return next;
        });
        setCurrentPage(1);
      }
    },
    [divisions, user?.isSuperAdmin],
  );

  const handleSelectedDivisionChange = useCallback((division: string) => {
    setSelectedDivision(division);
    setColumnFilters((previous) => ({ ...previous, warehouse: "" }));
    setCurrentPage(1);
    setTimelinePage(1);
  }, []);

  const handleStatusFilterChange = useCallback((status: string) => {
    setStatusFilter(status);
    setCurrentPage(1);
    setTimelinePage(1);
  }, []);

  const handlePageSizeChange = useCallback((nextPageSize: number) => {
    setPageSize(nextPageSize);
    setCurrentPage(1);
  }, []);

  const handleTimelineColumnFilterChange = useCallback(
    (field: AgreementTimelineFilterField, value: SavedColumnFilterValue) => {
      setTimelineColumnFilters((previous) => ({ ...previous, [field]: value }));
      setTimelinePage(1);
    },
    [],
  );

  const handleTimelineColumnFilterDraftChange = useCallback((field: AgreementTimelineFilterField, value: string) => {
    setTimelineColumnFilterDrafts((previous) => ({ ...previous, [field]: value }));
    setTimelinePage(1);
  }, []);

  const handleTimelineColumnLayoutChange = useCallback(
    (layout: TableColumnLayoutItem<AgreementTimelineColumnKey>[]) => {
      setTimelineColumnLayout(layout);
      const hidden = new Set(layout.filter((column) => !column.visible).map((column) => column.key));
      setTimelineColumnFilters((filters) => {
        const next = { ...filters };
        hidden.forEach((key) => delete next[key]);
        if (hidden.has("customerName")) delete next.customerOrOpportunity;
        return next;
      });
      setTimelineColumnFilterDrafts((drafts) => {
        const next = { ...drafts };
        hidden.forEach((key) => delete next[key]);
        if (hidden.has("customerName")) delete next.customerOrOpportunity;
        return next;
      });
      if (hidden.has("fulfilmentStatus")) setStatusFilter("");
    },
    [],
  );

  const handleTimelineColumnVisibilityChange = useCallback((key: AgreementTimelineColumnKey, visible: boolean) => {
    setTimelineColumnLayout((layout) =>
      setTableColumnVisibility(AGREEMENT_TIMELINE_COLUMN_CATALOG, layout, key, visible),
    );
    if (!visible) {
      if (key === "fulfilmentStatus") setStatusFilter("");
      setTimelineColumnFilters((filters) => {
        const next = { ...filters };
        delete next[key];
        if (key === "customerName") delete next.customerOrOpportunity;
        return next;
      });
      setTimelineColumnFilterDrafts((drafts) => {
        const next = { ...drafts };
        delete next[key];
        if (key === "customerName") delete next.customerOrOpportunity;
        return next;
      });
      setTimelinePage(1);
    }
  }, []);

  const handleResetFilters = useCallback(() => {
    clearSessionPageState(user?.loginName, "agreements");
    setSearchTerm("");
    setSelectedDivision(user?.isSuperAdmin ? "" : divisions.length === 1 ? divisions[0] : "");
    setOrderTypeFilter("");
    setStatusFilter("");
    setShowHistorical(false);
    setColumnFilters({});
    setColumnFilterDrafts({});
    setDateColumnFilters({});
    setTimelineColumnFilters({});
    setTimelineColumnFilterDrafts({});
    setTimelineStartDate("");
    setTimelineEndDate("");
    setCurrentPage(1);
    setTimelinePage(1);
  }, [divisions, user?.isSuperAdmin]);

  const captureSavedViewState = useCallback((): AgreementsSavedViewState => {
    return {
      stateVersion: 5,
      viewMode,
      searchTerm,
      showHistorical,
      selectedDivision,
      orderTypeFilter,
      statusFilter,
      advancedFilters: {},
      columnFilters: removeRetiredColumnFilters(effectiveColumnFilters),
      timelineColumnFilters: removeSyncedAgreementTimelineFilters(effectiveTimelineColumnFilters),
      dateColumnFilters,
      columns: columnLayout,
      timelineColumns: timelineColumnLayout,
      sortField,
      sortDirection,
      currentPage,
      timelinePage,
      pageSize,
    };
  }, [
    columnLayout,
    currentPage,
    dateColumnFilters,
    effectiveColumnFilters,
    effectiveTimelineColumnFilters,
    orderTypeFilter,
    pageSize,
    searchTerm,
    selectedDivision,
    showHistorical,
    sortDirection,
    sortField,
    statusFilter,
    timelineColumnLayout,
    timelinePage,
    viewMode,
  ]);

  const applySavedViewState = useCallback((state: AgreementsSavedViewState) => {
    setViewMode(state.viewMode);
    setSearchTerm(state.searchTerm);
    setShowHistorical(state.showHistorical);
    setSelectedDivision(serializeFilterSelection(state.selectedDivision.split(",")));
    setOrderTypeFilter(toOrderTypeFilter(state.orderTypeFilter));
    setStatusFilter(getAgreementStatusFilter(state));
    setColumnFilters(removeRetiredColumnFilters(state.columnFilters));
    setColumnFilterDrafts({});
    setDateColumnFilters(state.dateColumnFilters ?? {});
    setColumnLayout(repairTableColumnLayout(AGREEMENT_COLUMN_CATALOG, state.columns));
    setTimelineColumnLayout(repairTableColumnLayout(AGREEMENT_TIMELINE_COLUMN_CATALOG, state.timelineColumns));
    setTimelineColumnFilters(removeSyncedAgreementTimelineFilters(state.timelineColumnFilters ?? {}));
    setTimelineColumnFilterDrafts({});
    setSortField(state.sortField);
    setSortDirection(state.sortDirection);
    setCurrentPage(state.currentPage ?? 1);
    setTimelinePage(state.timelinePage ?? 1);
    setPageSize(state.pageSize ?? 50);
  }, []);

  const agreementSavedViews = useAgreementSavedViews({
    applyState: applySavedViewState,
    canManageSharedViews,
    canPersistViews,
    captureState: captureSavedViewState,
  });

  const workingStateRef = useRef({
    canPersist: false,
    userIdentifier: user?.loginName,
    state: captureSavedViewState(),
  });
  useLayoutEffect(() => {
    workingStateRef.current = {
      canPersist: !agreementSavedViews.selectedViewId,
      userIdentifier: user?.loginName,
      state: captureSavedViewState(),
    };
  }, [agreementSavedViews.selectedViewId, captureSavedViewState, user?.loginName]);

  useEffect(
    () => () => {
      if (workingStateRef.current.canPersist) {
        writeSessionPageState(workingStateRef.current.userIdentifier, "agreements", workingStateRef.current.state);
      }
    },
    [],
  );

  useEffect(() => {
    if (agreementSavedViews.selectedViewId) return;
    const timeout = window.setTimeout(
      () => writeSessionPageState(user?.loginName, "agreements", captureSavedViewState()),
      250,
    );
    return () => window.clearTimeout(timeout);
  }, [agreementSavedViews.selectedViewId, captureSavedViewState, user?.loginName]);

  // Build Gantt tasks from agreements (for timeline view)
  const ganttTasks: GanttTask[] = useMemo(() => {
    if (viewMode !== "timeline") return [];
    return buildAgreementTimelineTasks(timelineAgreements, timelineToday);
  }, [timelineAgreements, timelineToday, viewMode]);

  return (
    <div className={`${styles.page} ${viewMode === "table" ? styles.tableViewPage : ""}`}>
      <AgreementControls
        agreementCount={sortedAgreements.length}
        divisionLookups={divisionLookups}
        hasActiveFilters={hasActiveFilters}
        isReadOnly={Boolean(user?.isReadOnly)}
        isSuperAdmin={Boolean(user?.isSuperAdmin)}
        orderTypeFilter={orderTypeFilter}
        savedViewControls={
          <AgreementSavedViewControls
            canDeleteSelectedView={agreementSavedViews.canDeleteSelectedView}
            canEditSelectedView={agreementSavedViews.canEditSelectedView}
            canManageSharedViews={canManageSharedViews}
            canPersistViews={canPersistViews}
            feedback={agreementSavedViews.feedback}
            isDirty={agreementSavedViews.isDirty}
            isRecipientLoading={agreementSavedViews.isRecipientLoading}
            isSelectedViewReceived={agreementSavedViews.isSelectedViewReceived}
            isSharingEditorOpen={agreementSavedViews.isSharingEditorOpen}
            isSharingDirty={agreementSavedViews.isSharingDirty}
            name={agreementSavedViews.name}
            recipientCandidates={agreementSavedViews.recipientCandidates}
            recipientError={agreementSavedViews.recipientError}
            recipients={agreementSavedViews.recipients}
            pendingAction={agreementSavedViews.pendingAction}
            scope={agreementSavedViews.scope}
            selectedViewId={agreementSavedViews.selectedViewId}
            views={agreementSavedViews.views}
            onCancelSharingEdit={agreementSavedViews.cancelSharingEdit}
            onDelete={() => void agreementSavedViews.deleteSelected()}
            onEditSharing={agreementSavedViews.editSharing}
            onNameChange={agreementSavedViews.setName}
            onRecipientsChange={agreementSavedViews.setRecipients}
            onRetryRecipients={agreementSavedViews.retryRecipients}
            onSearchRecipients={agreementSavedViews.searchRecipients}
            onSaveNew={() => void agreementSavedViews.saveNew()}
            onSaveSharing={() => void agreementSavedViews.saveSharing()}
            onScopeChange={agreementSavedViews.setScope}
            onSelectionChange={agreementSavedViews.selectView}
            onUpdate={() => void agreementSavedViews.updateSelected()}
          />
        }
        tableActions={
          viewMode === "table" ? (
            <TableColumnsMenu
              catalogue={AGREEMENT_COLUMN_CATALOG}
              layout={columnLayout}
              onChange={setColumnLayout}
              onVisibilityChange={handleColumnVisibilityChange}
            />
          ) : (
            <TableColumnsMenu
              catalogue={AGREEMENT_TIMELINE_COLUMN_CATALOG}
              layout={timelineColumnLayout}
              maxVisibleColumns={MAX_VISIBLE_TIMELINE_COLUMNS}
              onChange={handleTimelineColumnLayoutChange}
              onVisibilityChange={handleTimelineColumnVisibilityChange}
            />
          )
        }
        searchTerm={searchTerm}
        selectedDivision={selectedDivision}
        showHistorical={showHistorical}
        statusFilter={statusFilter}
        userDivisionCodes={divisions}
        viewMode={viewMode}
        onOrderTypeFilterChange={(value) => {
          setOrderTypeFilter(value);
          setCurrentPage(1);
          setTimelinePage(1);
        }}
        onRefresh={() => void loadAgreements()}
        onResetFilters={handleResetFilters}
        onSearchTermChange={(value) => {
          setSearchTerm(value);
          setCurrentPage(1);
          setTimelinePage(1);
        }}
        onSelectedDivisionChange={handleSelectedDivisionChange}
        onShowHistoricalChange={(value) => {
          setShowHistorical(value);
          setCurrentPage(1);
          setTimelinePage(1);
        }}
        onStatusFilterChange={handleStatusFilterChange}
        onViewModeChange={setViewMode}
      />

      {agreementSavedViews.isSelectedViewReceived && !isLoading && !error && sortedAgreements.length === 0 && (
        <div data-print-hidden>
          <Alert variant="info">
            <span>{t("savedViews.sharedEmpty")}</span>{" "}
            <Button
              label={t("savedViews.resetFilters")}
              size="small"
              variant="secondary"
              onClick={handleResetFilters}
            />
          </Alert>
        </div>
      )}

      {/* Content */}
      {isLoading ? (
        viewMode === "table" ? (
          <TableSkeleton columns={13} rows={8} ariaLabel="Loading agreements table" />
        ) : (
          <div className={styles.loadingContainer}>
            <Spinner />
          </div>
        )
      ) : error ? (
        <div className={styles.errorContainer}>{error}</div>
      ) : viewMode === "timeline" ? (
        sortedAgreements.length === 0 && !Object.values(timelineColumnFilters).some(Boolean) ? (
          <div className={styles.emptyState}>{t("common.noResults")}</div>
        ) : (
          <AgreementsTimeline
            agreements={timelineAgreements}
            columnFilters={timelineColumnFilters}
            columnFilterDrafts={timelineColumnFilterDrafts}
            columnLayout={timelineColumnLayout}
            currentPage={timelinePage}
            historyStart={timelineHistoryStart}
            sortDirection={sortDirection}
            sortField={sortField}
            timelineEndDate={timelineEndDate}
            timelineStartDate={timelineStartDate}
            statusFilter={statusFilter}
            tasks={ganttTasks}
            totalPages={timelineTotalPages}
            warehouseOptions={warehouseOptions}
            onAgreementOpen={handleRowClick}
            onColumnFilterChange={handleTimelineColumnFilterChange}
            onColumnFilterDraftChange={handleTimelineColumnFilterDraftChange}
            onColumnLayoutChange={handleTimelineColumnLayoutChange}
            onNextPage={() => setTimelinePage((page) => page + 1)}
            onPreviousPage={() => setTimelinePage((page) => page - 1)}
            onSort={handleSort}
            onStatusFilterChange={handleStatusFilterChange}
            onTimelinePeriodApply={(startDate, endDate) => {
              setTimelineStartDate(startDate);
              setTimelineEndDate(endDate);
              setTimelinePage(1);
            }}
          />
        )
      ) : (
        <AgreementsTable
          agreements={paginatedAgreements}
          columnFilters={columnFilters}
          columnFilterDrafts={columnFilterDrafts}
          columnLayout={columnLayout}
          dateColumnFilters={dateColumnFilters}
          currentPage={currentPage}
          divisionFilter={selectedDivision}
          divisionOptions={divisionOptions}
          pageSize={pageSize}
          showAllDivisionOption={showAllDivisionOption}
          sortDirection={sortDirection}
          sortField={sortField}
          statusFilter={statusFilter}
          totalPages={totalPages}
          warehouseOptions={warehouseOptions}
          onColumnFilterChange={handleColumnFilterChange}
          onColumnFilterDraftChange={handleColumnFilterDraftChange}
          onColumnLayoutChange={setColumnLayout}
          onDateColumnFilterChange={handleDateColumnFilterChange}
          onDivisionFilterChange={handleSelectedDivisionChange}
          onNextPage={() => setCurrentPage((page) => page + 1)}
          onOrderNumberClick={handleOrderNumberClick}
          onPageSizeChange={handlePageSizeChange}
          onPreviousPage={() => setCurrentPage((page) => page - 1)}
          onRowClick={handleRowClick}
          onSort={handleSort}
          onStatusFilterChange={handleStatusFilterChange}
        />
      )}
    </div>
  );
};
