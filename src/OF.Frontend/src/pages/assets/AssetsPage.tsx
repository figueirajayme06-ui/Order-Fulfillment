import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type FC } from "react";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router-dom";
import { Alert, Button, Spinner, TableColumnsMenu, TableSkeleton } from "../../components/common";
import type { DateColumnFilterValue } from "../../components/common/TableColumnControls/dateColumnFilterModel";
import {
  applyMultiValueTextFilterDrafts,
  type MultiValueTextFilterDrafts,
} from "../../components/common/TableColumnControls/multiValueTextFilterModel";
import { useAuth } from "../../contexts/auth";
import { haveSameFilterSelection, parseFilterSelection } from "../../lib/filterSelection";
import { clearSessionPageState, readSessionPageState, writeSessionPageState } from "../../lib/sessionPageState";
import {
  repairTableColumnLayout,
  setTableColumnVisibility,
  type TableColumnLayoutItem,
} from "../../lib/tableColumnLayout";
import { cacheAssetList, readCachedAssetList } from "../../services/assetListCache";
import { fetchAssets } from "../../services/assetsService";
import { fetchAssetEvents } from "../../services/eventsService";
import { fetchDivisions, type DivisionLookup } from "../../services/lookupsService";
import type { Asset, AssetEvent } from "../../types";
import {
  MAX_VISIBLE_TIMELINE_COLUMNS,
  parseAssetsSavedViewState,
  type AssetsSavedViewState,
  type AssetsSortField,
  type SavedColumnFilterValue,
} from "../../types/savedViews";
import {
  ASSET_COLUMN_CATALOG,
  ASSET_TIMELINE_COLUMN_CATALOG,
  type AssetColumnKey,
  type AssetTimelineColumnKey,
} from "./assetColumnCatalog";
import { ASSET_STATUSES, AssetControls } from "./AssetControls";
import { AssetRingfenceActions } from "./AssetRingfenceActions";
import { AssetRingfenceOverlapDialog } from "./AssetRingfenceOverlapDialog";
import { AssetSavedViewControls } from "./AssetSavedViewControls";
import { filterAssets, sortAssets } from "./assetsListModel";
import styles from "./AssetsPage.module.css";
import { buildAssetFilterParams } from "./assetsQuery";
import { AssetsTable } from "./AssetsTable";
import { AssetsTimeline } from "./AssetsTimeline";
import {
  buildAssetTimelinePeriod,
  buildAssetTimelineRows,
  buildAssetTimelineTasks,
  deriveAssetTimelineEventDivisions,
  filterAssetTimelineAssets,
  filterAssetTimelineAssetsByDateRange,
  normalizeAssetEventsByAssetId,
  toAssetTimelineDateValue,
  type AssetTimelineColumnFilters,
  type AssetTimelineFilterField,
} from "./assetsTimelineModel";
import {
  getAssetDivisionFilter,
  getAssetStatusFilter,
  removeSyncedAssetTimelineFilters,
  toAssetColumnFilters,
  toAssetDateColumnFilters,
} from "./assetViewState";
import { parseRingfenceAssetsContext } from "./ringfenceAssetsContext";
import { useAssetRingfence } from "./useAssetRingfence";
import { useAssetSavedViews } from "./useAssetSavedViews";
export { parseRingfenceAssetsContext } from "./ringfenceAssetsContext";

type SortField = AssetsSortField;
type SortDirection = "asc" | "desc";

export const AssetsPage: FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const canMutate = !user?.isReadOnly;
  const location = useLocation();
  const ringfenceContext = useMemo(() => parseRingfenceAssetsContext(location.search), [location.search]);
  const contextualReturnPath = useMemo(() => {
    if (ringfenceContext.ringfenceId === null) {
      return null;
    }

    return ringfenceContext.returnTo ?? `/ringfence?ringfenceId=${ringfenceContext.ringfenceId}`;
  }, [ringfenceContext]);
  const restoredWorkingState = useMemo(
    () =>
      ringfenceContext.ringfenceId !== null
        ? null
        : readSessionPageState(user?.loginName, "assets", parseAssetsSavedViewState),
    [ringfenceContext.ringfenceId, user?.loginName],
  );

  const [assets, setAssets] = useState<Asset[]>([]);
  const [assetEventsByAssetId, setAssetEventsByAssetId] = useState<Record<string, AssetEvent[]>>({});
  const [isLoading, setIsLoading] = useState(true);
  const [isEventsLoading, setIsEventsLoading] = useState(() => restoredWorkingState?.viewMode === "timeline");
  const [loadError, setLoadError] = useState<string | null>(null);
  const [eventsLoadError, setEventsLoadError] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<"table" | "timeline">(() => restoredWorkingState?.viewMode ?? "table");
  const viewModeRef = useRef(viewMode);

  useEffect(() => {
    viewModeRef.current = viewMode;
  }, [viewMode]);

  const handleViewModeChange = useCallback((nextViewMode: "table" | "timeline") => {
    if (nextViewMode === "timeline" && viewModeRef.current !== "timeline") {
      setIsEventsLoading(true);
    }
    viewModeRef.current = nextViewMode;
    setViewMode(nextViewMode);
  }, []);

  // Basic filters
  const [searchTerm, setSearchTerm] = useState(() => restoredWorkingState?.searchTerm ?? "");
  const [statusFilter, setStatusFilter] = useState(() => getAssetStatusFilter(restoredWorkingState));
  const [hideRemovedStock, setHideRemovedStock] = useState(() => restoredWorkingState?.hideRemovedStock ?? true);
  const [selectedDivision, setSelectedDivision] = useState<string>(() => {
    if (restoredWorkingState) return getAssetDivisionFilter(restoredWorkingState);
    const divs =
      user?.division
        ?.split(",")
        .map((d) => d.trim())
        .filter(Boolean) ?? [];
    return divs.length > 0 ? divs.join(",") : "";
  });

  const [selectedAssets, setSelectedAssets] = useState<Set<string>>(new Set());
  const {
    ringfences,
    selectedRingfence,
    setSelectedRingfence,
    isAddingToRingfence,
    ringfenceFeedback,
    setRingfenceFeedback,
    overlapConfirmation,
    setOverlapConfirmation,
    handleAddToRingfence,
    confirmOverlappingAssets,
  } = useAssetRingfence({
    contextualRingfenceId: ringfenceContext.ringfenceId,
    contextualReturnPath,
    selectedAssets,
    setSelectedAssets,
  });
  // Table sorting & pagination
  const [sortField, setSortField] = useState<SortField>(() => restoredWorkingState?.sortField ?? "id");
  const [sortDirection, setSortDirection] = useState<SortDirection>(() => restoredWorkingState?.sortDirection ?? "asc");
  const [columnFilters, setColumnFilters] = useState<Partial<Record<SortField, SavedColumnFilterValue>>>(() =>
    toAssetColumnFilters(restoredWorkingState?.columnFilters ?? {}),
  );
  const [columnFilterDrafts, setColumnFilterDrafts] = useState<MultiValueTextFilterDrafts<SortField>>({});
  const [dateColumnFilters, setDateColumnFilters] = useState<Partial<Record<SortField, DateColumnFilterValue>>>(() =>
    toAssetDateColumnFilters(restoredWorkingState?.dateColumnFilters ?? {}),
  );
  const [columnLayout, setColumnLayout] = useState<TableColumnLayoutItem<AssetColumnKey>[]>(() =>
    repairTableColumnLayout(ASSET_COLUMN_CATALOG, restoredWorkingState?.columns),
  );
  const [timelineColumnLayout, setTimelineColumnLayout] = useState<TableColumnLayoutItem<AssetTimelineColumnKey>[]>(
    () => repairTableColumnLayout(ASSET_TIMELINE_COLUMN_CATALOG, restoredWorkingState?.timelineColumns),
  );
  const [timelineColumnFilters, setTimelineColumnFilters] = useState<AssetTimelineColumnFilters>(() =>
    removeSyncedAssetTimelineFilters(restoredWorkingState?.timelineColumnFilters ?? {}),
  );
  const [timelineColumnFilterDrafts, setTimelineColumnFilterDrafts] = useState<
    MultiValueTextFilterDrafts<AssetTimelineFilterField>
  >({});
  const [timelineStartDate, setTimelineStartDate] = useState("");
  const [timelineEndDate, setTimelineEndDate] = useState("");
  const [currentPage, setCurrentPage] = useState(() => restoredWorkingState?.currentPage ?? 1);
  const [timelinePage, setTimelinePage] = useState(() => restoredWorkingState?.timelinePage ?? 1);
  const [pageSize, setPageSize] = useState(() => restoredWorkingState?.pageSize ?? 50);
  const timelinePageSize = 50;

  const defaultTimelineRange = useMemo(() => buildAssetTimelinePeriod(new Date()), []);
  const timelineRangeStart = useMemo(
    () => (timelineStartDate ? new Date(`${timelineStartDate}T00:00:00`) : defaultTimelineRange.start),
    [defaultTimelineRange.start, timelineStartDate],
  );
  const timelineRangeEnd = useMemo(
    () => (timelineEndDate ? new Date(`${timelineEndDate}T00:00:00`) : defaultTimelineRange.end),
    [defaultTimelineRange.end, timelineEndDate],
  );
  const latestRequestIdRef = useRef(0);
  const latestEventsRequestIdRef = useRef(0);
  const canManageSharedViews = Boolean(user?.isAdmin);

  const buildParams = useCallback(
    () =>
      buildAssetFilterParams({
        searchTerm,
        statusFilter,
        warehouseFilter: "",
        selectedDivision,
        hideRemovedStock,
        advancedFilters: {},
      }),
    [searchTerm, statusFilter, selectedDivision, hideRemovedStock],
  );

  const loadAssets = useCallback(
    async (forceRefresh = false) => {
      const requestId = ++latestRequestIdRef.current;
      const params = buildParams();
      const cachedAssets = forceRefresh ? null : readCachedAssetList(user?.loginName, params);

      if (cachedAssets) {
        setAssets(cachedAssets);
        setIsLoading(false);
        setLoadError(null);
        return;
      }

      if (viewModeRef.current === "timeline") {
        setIsEventsLoading(true);
      }
      setIsLoading(true);
      setLoadError(null);
      try {
        const data = await fetchAssets(params);
        if (requestId !== latestRequestIdRef.current) {
          return;
        }
        cacheAssetList(user?.loginName, params, data);
        setAssets(data);
      } catch {
        if (requestId !== latestRequestIdRef.current) {
          return;
        }
        setLoadError(t("common.error"));
      } finally {
        if (requestId === latestRequestIdRef.current) {
          setIsLoading(false);
        }
      }
    },
    [buildParams, t, user?.loginName],
  );

  const timelineEventDivisions = useMemo(() => {
    return deriveAssetTimelineEventDivisions(selectedDivision, assets);
  }, [assets, selectedDivision]);

  const loadAssetEvents = useCallback(async () => {
    const requestId = ++latestEventsRequestIdRef.current;

    if (assets.length === 0 || !timelineEventDivisions) {
      setAssetEventsByAssetId({});
      setEventsLoadError(null);
      setIsEventsLoading(false);
      return;
    }

    setIsEventsLoading(true);
    setEventsLoadError(null);

    try {
      const data = await fetchAssetEvents({
        startDate: timelineStartDate || toAssetTimelineDateValue(timelineRangeStart),
        endDate: timelineEndDate || toAssetTimelineDateValue(timelineRangeEnd),
        divisions: timelineEventDivisions,
      });

      if (requestId !== latestEventsRequestIdRef.current) {
        return;
      }

      setAssetEventsByAssetId(normalizeAssetEventsByAssetId(data));
    } catch {
      if (requestId !== latestEventsRequestIdRef.current) {
        return;
      }

      setAssetEventsByAssetId({});
      setEventsLoadError(t("common.error"));
    } finally {
      if (requestId === latestEventsRequestIdRef.current) {
        setIsEventsLoading(false);
      }
    }
  }, [
    assets.length,
    t,
    timelineEndDate,
    timelineEventDivisions,
    timelineRangeEnd,
    timelineRangeStart,
    timelineStartDate,
  ]);

  // Division lookups from API
  const [divisionLookups, setDivisionLookups] = useState<DivisionLookup[]>([]);

  useEffect(() => {
    let active = true;
    fetchDivisions()
      .then((divisions) => {
        if (active) setDivisionLookups(divisions);
      })
      .catch(() => {});
    return () => {
      active = false;
    };
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

  const warehouseOptions = useMemo(() => {
    const selectedDivisionCodes = new Set(parseFilterSelection(selectedDivision));
    return Array.from(
      new Set(
        assets
          .filter(
            (asset) => selectedDivisionCodes.size === 0 || selectedDivisionCodes.has(asset.division?.trim() ?? ""),
          )
          .map((asset) => asset.warehouse?.trim())
          .filter((warehouse): warehouse is string => Boolean(warehouse)),
      ),
    )
      .sort((first, second) => first.localeCompare(second, undefined, { numeric: true, sensitivity: "base" }))
      .map((warehouse) => ({ label: warehouse, value: warehouse }));
  }, [assets, selectedDivision]);

  const defaultDivision = user?.isSuperAdmin ? "" : divisions.join(",");
  const hasActiveFilters =
    searchTerm !== "" ||
    statusFilter !== "" ||
    !haveSameFilterSelection(selectedDivision, defaultDivision) ||
    !hideRemovedStock ||
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

  const filteredAssets = useMemo(() => {
    const selectedStatuses = parseFilterSelection(statusFilter);
    const selectedDivisions = parseFilterSelection(selectedDivision);
    const filtersWithSyncedSelections = { ...effectiveColumnFilters };
    if (selectedStatuses.length > 0) filtersWithSyncedSelections.status = selectedStatuses;
    if (selectedDivisions.length > 0) filtersWithSyncedSelections.division = selectedDivisions;
    return filterAssets(assets, filtersWithSyncedSelections, dateColumnFilters);
  }, [assets, dateColumnFilters, effectiveColumnFilters, selectedDivision, statusFilter]);

  const sortedAssets = useMemo(() => {
    return sortAssets(filteredAssets, sortField, sortDirection);
  }, [filteredAssets, sortField, sortDirection]);

  const totalPages = Math.max(1, Math.ceil(sortedAssets.length / pageSize));
  useEffect(() => {
    if (!isLoading) setCurrentPage((page) => Math.min(page, totalPages));
  }, [isLoading, totalPages]);
  const paginatedAssets = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return sortedAssets.slice(start, start + pageSize);
  }, [sortedAssets, currentPage, pageSize]);

  const selectedAssetCount = selectedAssets.size;
  const selectedAssetsOnPageCount = useMemo(() => {
    return paginatedAssets.reduce((count, asset) => count + (selectedAssets.has(asset.id) ? 1 : 0), 0);
  }, [paginatedAssets, selectedAssets]);

  const timelineSourceAssets = useMemo(
    () =>
      filterAssetTimelineAssetsByDateRange(
        filterAssetTimelineAssets(sortedAssets, effectiveTimelineColumnFilters),
        assetEventsByAssetId,
        timelineStartDate,
        timelineEndDate,
      ),
    [assetEventsByAssetId, effectiveTimelineColumnFilters, sortedAssets, timelineEndDate, timelineStartDate],
  );

  const timelineTotalPages = Math.max(1, Math.ceil(timelineSourceAssets.length / timelinePageSize));
  const timelineAssets = useMemo(() => {
    const start = (timelinePage - 1) * timelinePageSize;
    return timelineSourceAssets.slice(start, start + timelinePageSize);
  }, [timelineSourceAssets, timelinePage]);

  const timelineRows = useMemo(() => {
    return buildAssetTimelineRows(timelineAssets, assetEventsByAssetId);
  }, [timelineAssets, assetEventsByAssetId]);

  useEffect(() => {
    if (!isLoading) setTimelinePage((prev) => Math.min(prev, timelineTotalPages));
  }, [isLoading, timelineTotalPages]);

  useEffect(() => {
    if (viewMode !== "timeline" || isLoading) {
      return;
    }

    loadAssetEvents();
  }, [viewMode, isLoading, loadAssetEvents]);

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

  const handleResetFilters = useCallback(() => {
    clearSessionPageState(user?.loginName, "assets");
    setSearchTerm("");
    setStatusFilter("");
    setSelectedDivision(user?.isSuperAdmin ? "" : divisions.join(","));
    setHideRemovedStock(true);
    setColumnFilters({});
    setColumnFilterDrafts({});
    setDateColumnFilters({});
    setTimelineColumnFilters({});
    setTimelineColumnFilterDrafts({});
    setTimelineStartDate("");
    setTimelineEndDate("");
    setCurrentPage(1);
    setTimelinePage(1);
    setLoadError(null);
    setEventsLoadError(null);
  }, [divisions, user?.isSuperAdmin]);

  const handleColumnFilterChange = useCallback((field: SortField, value: SavedColumnFilterValue) => {
    setColumnFilters((prev) => ({ ...prev, [field]: value }));
    setCurrentPage(1);
    setTimelinePage(1);
  }, []);

  const handleStatusFilterChange = useCallback((status: string) => {
    setStatusFilter(status);
    setCurrentPage(1);
    setTimelinePage(1);
  }, []);

  const handleSelectedDivisionChange = useCallback((division: string) => {
    setSelectedDivision(division);
    setColumnFilters((previous) => {
      const next = { ...previous };
      delete next.warehouse;
      return next;
    });
    setTimelineColumnFilters((previous) => {
      const next = { ...previous };
      delete next.warehouse;
      return next;
    });
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
    (key: AssetColumnKey, visible: boolean) => {
      setColumnLayout((layout) => setTableColumnVisibility(ASSET_COLUMN_CATALOG, layout, key, visible));
      if (!visible) {
        if (key === "status") setStatusFilter("");
        if (key === "division") setSelectedDivision(defaultDivision);
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
    [defaultDivision],
  );

  const handlePageSizeChange = useCallback((nextPageSize: number) => {
    setPageSize(nextPageSize);
    setCurrentPage(1);
  }, []);

  const handleTimelineColumnFilterChange = useCallback(
    (field: AssetTimelineFilterField, value: SavedColumnFilterValue) => {
      setTimelineColumnFilters((previous) => ({ ...previous, [field]: value }));
      setTimelinePage(1);
    },
    [],
  );

  const handleTimelineColumnFilterDraftChange = useCallback((field: AssetTimelineFilterField, value: string) => {
    setTimelineColumnFilterDrafts((previous) => ({ ...previous, [field]: value }));
    setTimelinePage(1);
  }, []);

  const handleTimelineColumnLayoutChange = useCallback(
    (layout: TableColumnLayoutItem<AssetTimelineColumnKey>[]) => {
      setTimelineColumnLayout(layout);
      const hidden = new Set(layout.filter((column) => !column.visible).map((column) => column.key));
      if (hidden.has("status")) setStatusFilter("");
      if (hidden.has("division")) setSelectedDivision(defaultDivision);
      setTimelineColumnFilters((filters) => {
        const next = { ...filters };
        hidden.forEach((key) => delete next[key]);
        return next;
      });
      setTimelineColumnFilterDrafts((drafts) => {
        const next = { ...drafts };
        hidden.forEach((key) => delete next[key]);
        return next;
      });
    },
    [defaultDivision],
  );

  const handleTimelineColumnVisibilityChange = useCallback(
    (key: AssetTimelineColumnKey, visible: boolean) => {
      setTimelineColumnLayout((layout) =>
        setTableColumnVisibility(ASSET_TIMELINE_COLUMN_CATALOG, layout, key, visible),
      );
      if (!visible) {
        if (key === "status") setStatusFilter("");
        if (key === "division") setSelectedDivision(defaultDivision);
        setTimelineColumnFilters((filters) => {
          const next = { ...filters };
          delete next[key];
          return next;
        });
        setTimelineColumnFilterDrafts((drafts) => {
          const next = { ...drafts };
          delete next[key];
          return next;
        });
        setTimelinePage(1);
      }
    },
    [defaultDivision],
  );

  const handleAssetSelect = useCallback(
    (assetId: string, checked: boolean) => {
      if (isAddingToRingfence || overlapConfirmation !== null) return;
      setSelectedAssets((prev) => {
        const next = new Set(prev);
        if (checked) {
          next.add(assetId);
        } else {
          next.delete(assetId);
        }
        return next;
      });
    },
    [isAddingToRingfence, overlapConfirmation],
  );

  const handleSelectAll = useCallback(
    (checked: boolean) => {
      if (isAddingToRingfence || overlapConfirmation !== null) return;
      if (checked) {
        setSelectedAssets(new Set(paginatedAssets.map((a) => a.id)));
      } else {
        setSelectedAssets(new Set());
      }
    },
    [isAddingToRingfence, overlapConfirmation, paginatedAssets],
  );

  const selectedTimelineAssetsOnPageCount = useMemo(() => {
    const timelineAssetIds = new Set(timelineAssets.map((asset) => asset.id));
    return Array.from(selectedAssets).reduce((count, assetId) => count + (timelineAssetIds.has(assetId) ? 1 : 0), 0);
  }, [selectedAssets, timelineAssets]);

  const handleSelectAllTimelineAssets = useCallback(
    (checked: boolean) => {
      if (isAddingToRingfence || overlapConfirmation !== null) return;
      if (checked) {
        setSelectedAssets(new Set(timelineAssets.map((asset) => asset.id)));
      } else {
        setSelectedAssets(new Set());
      }
    },
    [isAddingToRingfence, overlapConfirmation, timelineAssets],
  );

  const handleClearSelectedAssets = useCallback(() => {
    setSelectedAssets(new Set());
  }, []);

  const captureSavedViewState = useCallback((): AssetsSavedViewState => {
    return {
      stateVersion: 5,
      viewMode,
      searchTerm,
      statusFilter,
      warehouseFilter: "",
      hideRemovedStock,
      selectedDivision,
      advancedFilters: {},
      columnFilters: toAssetColumnFilters(effectiveColumnFilters),
      timelineColumnFilters: removeSyncedAssetTimelineFilters(effectiveTimelineColumnFilters),
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
    hideRemovedStock,
    pageSize,
    searchTerm,
    selectedDivision,
    sortDirection,
    sortField,
    statusFilter,
    timelineColumnLayout,
    timelinePage,
    viewMode,
  ]);

  const applySavedViewState = useCallback(
    (state: AssetsSavedViewState) => {
      handleViewModeChange(state.viewMode);
      setSearchTerm(state.searchTerm);
      setStatusFilter(getAssetStatusFilter(state));
      setHideRemovedStock(state.hideRemovedStock);
      setSelectedDivision(getAssetDivisionFilter(state));
      setColumnFilters(toAssetColumnFilters(state.columnFilters));
      setColumnFilterDrafts({});
      setDateColumnFilters(toAssetDateColumnFilters(state.dateColumnFilters ?? {}));
      setColumnLayout(repairTableColumnLayout(ASSET_COLUMN_CATALOG, state.columns));
      setTimelineColumnLayout(repairTableColumnLayout(ASSET_TIMELINE_COLUMN_CATALOG, state.timelineColumns));
      setTimelineColumnFilters(removeSyncedAssetTimelineFilters(state.timelineColumnFilters ?? {}));
      setTimelineColumnFilterDrafts({});
      setSortField(state.sortField);
      setSortDirection(state.sortDirection);
      setCurrentPage(state.currentPage ?? 1);
      setTimelinePage(state.timelinePage ?? 1);
      setPageSize(state.pageSize ?? 50);
      setSelectedAssets(new Set());
    },
    [handleViewModeChange],
  );

  const {
    canDeleteSelectedView,
    canEditSelectedView,
    feedback: savedViewsFeedback,
    isInitialized: areSavedViewsInitialized,
    isDirty: isSavedViewDirty,
    isRecipientLoading,
    isSelectedViewReceived,
    isSharingEditorOpen,
    isSharingDirty,
    name: savedViewName,
    recipientCandidates,
    recipientError,
    recipients,
    pendingAction: savedViewPendingAction,
    scope: savedViewScope,
    selectedViewId: selectedSavedViewId,
    views: savedViews,
    cancelSharingEdit,
    deleteSelected: handleDeleteSavedView,
    editSharing,
    searchRecipients,
    saveNew: handleCreateSavedView,
    saveSharing: handleSaveSharing,
    selectView: handleSavedViewSelectionChange,
    setName: setSavedViewName,
    setRecipients,
    setScope: setSavedViewScope,
    retryRecipients,
    updateSelected: handleUpdateSavedView,
  } = useAssetSavedViews({
    applyState: applySavedViewState,
    canManageSharedViews,
    canPersistViews: canMutate,
    captureState: captureSavedViewState,
  });

  useEffect(() => {
    if (!areSavedViewsInitialized) return;
    void loadAssets();
    return () => {
      ++latestRequestIdRef.current;
    };
  }, [areSavedViewsInitialized, loadAssets]);

  const workingStateRef = useRef({
    canPersist: false,
    userIdentifier: user?.loginName,
    state: captureSavedViewState(),
  });
  useLayoutEffect(() => {
    workingStateRef.current = {
      canPersist: !selectedSavedViewId && ringfenceContext.ringfenceId === null,
      userIdentifier: user?.loginName,
      state: captureSavedViewState(),
    };
  }, [captureSavedViewState, ringfenceContext.ringfenceId, selectedSavedViewId, user?.loginName]);

  useEffect(
    () => () => {
      if (workingStateRef.current.canPersist) {
        writeSessionPageState(workingStateRef.current.userIdentifier, "assets", workingStateRef.current.state);
      }
    },
    [],
  );

  useEffect(() => {
    if (selectedSavedViewId || ringfenceContext.ringfenceId !== null) return;
    const timeout = window.setTimeout(
      () => writeSessionPageState(user?.loginName, "assets", captureSavedViewState()),
      250,
    );
    return () => window.clearTimeout(timeout);
  }, [captureSavedViewState, ringfenceContext.ringfenceId, selectedSavedViewId, user?.loginName]);

  // Build Gantt tasks from timeline events.
  const ganttTasks = useMemo(() => {
    if (viewMode !== "timeline") {
      return [];
    }

    return buildAssetTimelineTasks(timelineRows, defaultTimelineRange.start);
  }, [defaultTimelineRange.start, timelineRows, viewMode]);

  return (
    <div className={`${styles.page} ${viewMode === "table" ? styles.tableViewPage : ""}`}>
      <AssetControls
        savedViewControls={
          <AssetSavedViewControls
            canDeleteSelectedView={canDeleteSelectedView}
            canEditSelectedView={canEditSelectedView}
            canManageSharedViews={canManageSharedViews}
            canPersistViews={canMutate}
            feedback={savedViewsFeedback}
            isDirty={isSavedViewDirty}
            isRecipientLoading={isRecipientLoading}
            isSelectedViewReceived={isSelectedViewReceived}
            isSharingEditorOpen={isSharingEditorOpen}
            isSharingDirty={isSharingDirty}
            name={savedViewName}
            recipientCandidates={recipientCandidates}
            recipientError={recipientError}
            recipients={recipients}
            pendingAction={savedViewPendingAction}
            scope={savedViewScope}
            selectedViewId={selectedSavedViewId}
            views={savedViews}
            onCancelSharingEdit={cancelSharingEdit}
            onDelete={() => void handleDeleteSavedView()}
            onEditSharing={editSharing}
            onNameChange={setSavedViewName}
            onRecipientsChange={setRecipients}
            onRetryRecipients={retryRecipients}
            onSearchRecipients={searchRecipients}
            onSaveNew={() => void handleCreateSavedView()}
            onSaveSharing={() => void handleSaveSharing()}
            onScopeChange={setSavedViewScope}
            onSelectionChange={handleSavedViewSelectionChange}
            onUpdate={() => void handleUpdateSavedView()}
          />
        }
        tableActions={
          viewMode === "table" ? (
            <TableColumnsMenu
              catalogue={ASSET_COLUMN_CATALOG}
              layout={columnLayout}
              onChange={setColumnLayout}
              onVisibilityChange={handleColumnVisibilityChange}
            />
          ) : (
            <TableColumnsMenu
              catalogue={ASSET_TIMELINE_COLUMN_CATALOG}
              layout={timelineColumnLayout}
              maxVisibleColumns={MAX_VISIBLE_TIMELINE_COLUMNS}
              onChange={handleTimelineColumnLayoutChange}
              onVisibilityChange={handleTimelineColumnVisibilityChange}
            />
          )
        }
        assetCount={sortedAssets.length}
        canManageRemovedStock={Boolean(user?.isAdmin || user?.isSuperAdmin)}
        divisionLookups={divisionLookups}
        hideRemovedStock={hideRemovedStock}
        hasActiveFilters={hasActiveFilters}
        isSuperAdmin={Boolean(user?.isSuperAdmin)}
        searchTerm={searchTerm}
        selectedDivision={selectedDivision}
        statusFilter={statusFilter}
        userDivisionCodes={divisions}
        viewMode={viewMode}
        onHideRemovedStockChange={(value) => {
          setHideRemovedStock(value);
          setCurrentPage(1);
          setTimelinePage(1);
        }}
        onRefresh={() => {
          if (areSavedViewsInitialized) void loadAssets(true);
        }}
        onResetFilters={handleResetFilters}
        onSearchTermChange={(value) => {
          setSearchTerm(value);
          setCurrentPage(1);
          setTimelinePage(1);
        }}
        onSelectedDivisionChange={handleSelectedDivisionChange}
        onStatusFilterChange={handleStatusFilterChange}
        onViewModeChange={handleViewModeChange}
      />

      {isSelectedViewReceived && !isLoading && !loadError && sortedAssets.length === 0 && (
        <div data-print-hidden>
          <Alert variant="info">
            <div className={styles.loadErrorContent}>
              <span>{t("savedViews.sharedEmpty")}</span>
              <Button
                label={t("savedViews.resetFilters")}
                size="small"
                variant="secondary"
                onClick={handleResetFilters}
              />
            </div>
          </Alert>
        </div>
      )}

      {loadError && (
        <Alert variant="error">
          <div className={styles.loadErrorContent}>
            <span>{loadError}</span>
            <div className={styles.loadErrorActions}>
              <Button label={t("common.retry")} size="small" onClick={() => void loadAssets()} />
              <Button
                disabled={!hasActiveFilters}
                label="Reset filters"
                size="small"
                variant="secondary"
                onClick={handleResetFilters}
              />
            </div>
          </div>
        </Alert>
      )}

      {viewMode === "timeline" && eventsLoadError && (
        <Alert variant="error">
          <div className={styles.loadErrorContent}>
            <span>{eventsLoadError}</span>
            <Button label={t("common.retry")} size="small" onClick={() => void loadAssetEvents()} />
          </div>
        </Alert>
      )}

      {ringfenceFeedback && (
        <Alert variant={ringfenceFeedback.variant} onDismiss={() => setRingfenceFeedback(null)}>
          {ringfenceFeedback.message}
        </Alert>
      )}

      {/* Ringfence toolbar */}
      {canMutate && (viewMode === "table" || viewMode === "timeline") && (
        <AssetRingfenceActions
          isBusy={isAddingToRingfence}
          ringfences={ringfences}
          selectedAssetCount={selectedAssetCount}
          selectedAssetsOnPageCount={selectedAssetsOnPageCount}
          selectedRingfenceId={selectedRingfence}
          contextualReturnPath={contextualReturnPath}
          isContextualTarget={ringfenceContext.ringfenceId === selectedRingfence}
          isTargetLocked={overlapConfirmation !== null}
          onAdd={handleAddToRingfence}
          onClear={handleClearSelectedAssets}
          onSelectedRingfenceChange={setSelectedRingfence}
        />
      )}

      {/* Content */}
      {viewMode === "timeline" && (isLoading || isEventsLoading) ? (
        <div className={styles.loadingContainer}>
          <Spinner />
        </div>
      ) : viewMode === "table" && isLoading && assets.length === 0 ? (
        <TableSkeleton columns={14} rows={10} ariaLabel="Loading assets table" />
      ) : viewMode === "timeline" ? (
        <AssetsTimeline
          columnFilters={timelineColumnFilters}
          columnFilterDrafts={timelineColumnFilterDrafts}
          columnLayout={timelineColumnLayout}
          currentPage={timelinePage}
          divisionFilter={selectedDivision}
          divisionOptions={divisionOptions}
          historyStart={defaultTimelineRange.start}
          getAssetProfilePath={(assetId) => `/assets/${encodeURIComponent(assetId)}`}
          rangeStart={timelineRangeStart}
          timelineEndDate={timelineEndDate}
          timelineStartDate={timelineStartDate}
          rows={timelineRows}
          selectionEnabled={canMutate}
          showAllDivisionOption={showAllDivisionOption}
          selectedAssetIds={selectedAssets}
          selectedAssetsOnPageCount={selectedTimelineAssetsOnPageCount}
          sortDirection={sortDirection}
          sortField={sortField}
          statusFilter={statusFilter}
          statusOptions={ASSET_STATUSES}
          tasks={ganttTasks}
          totalPages={timelineTotalPages}
          warehouseOptions={warehouseOptions}
          onColumnFilterChange={handleTimelineColumnFilterChange}
          onColumnFilterDraftChange={handleTimelineColumnFilterDraftChange}
          onColumnLayoutChange={handleTimelineColumnLayoutChange}
          onDivisionFilterChange={handleSelectedDivisionChange}
          onNextPage={() => setTimelinePage((page) => page + 1)}
          onPreviousPage={() => setTimelinePage((page) => page - 1)}
          onSelectAll={handleSelectAllTimelineAssets}
          onSelectAsset={handleAssetSelect}
          onSort={handleSort}
          onStatusFilterChange={handleStatusFilterChange}
          onTimelinePeriodApply={(startDate, endDate) => {
            setTimelineStartDate(startDate);
            setTimelineEndDate(endDate);
            setTimelinePage(1);
          }}
        />
      ) : (
        <AssetsTable
          assets={paginatedAssets}
          columnFilters={columnFilters}
          columnFilterDrafts={columnFilterDrafts}
          columnLayout={columnLayout}
          dateColumnFilters={dateColumnFilters}
          currentPage={currentPage}
          divisionFilter={selectedDivision}
          divisionOptions={divisionOptions}
          emptyStateMessage={t("common.noResults")}
          getAssetProfilePath={(assetId) => `/assets/${encodeURIComponent(assetId)}`}
          selectedAssetIds={selectedAssets}
          selectedAssetsOnPageCount={selectedAssetsOnPageCount}
          pageSize={pageSize}
          selectionEnabled={canMutate}
          showAllDivisionOption={showAllDivisionOption}
          sortDirection={sortDirection}
          sortField={sortField}
          statusFilter={statusFilter}
          statusOptions={ASSET_STATUSES}
          totalPages={totalPages}
          warehouseOptions={warehouseOptions}
          onColumnFilterChange={handleColumnFilterChange}
          onColumnFilterDraftChange={handleColumnFilterDraftChange}
          onColumnLayoutChange={setColumnLayout}
          onDateColumnFilterChange={handleDateColumnFilterChange}
          onDivisionFilterChange={handleSelectedDivisionChange}
          onNextPage={() => setCurrentPage((page) => page + 1)}
          onPageSizeChange={handlePageSizeChange}
          onPreviousPage={() => setCurrentPage((page) => page - 1)}
          onSelectAll={handleSelectAll}
          onSelectAsset={handleAssetSelect}
          onSort={handleSort}
          onStatusFilterChange={handleStatusFilterChange}
        />
      )}

      <AssetRingfenceOverlapDialog
        overlapConfirmation={overlapConfirmation}
        isAddingToRingfence={isAddingToRingfence}
        onCancel={() => setOverlapConfirmation(null)}
        onConfirm={() => void confirmOverlappingAssets()}
      />
    </div>
  );
};
