import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type FC,
  type KeyboardEvent,
  type PointerEvent,
  type ReactNode,
} from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import {
  Button,
  DataGridColumnGroup,
  DataGridColumnHeaders,
  DataGridPagination,
  TableColumnFilter,
  TableColumnHeader,
} from "../../components/common";
import { toMultiValueTextFilter } from "../../components/common/TableColumnControls/multiValueTextFilterModel";
import { FrappeGantt, type FrappeGanttNavigation, type GanttTask } from "../../components/timeline/FrappeGantt";
import { useTimelineStickyHeaders } from "../../components/timeline/useTimelineStickyHeaders";
import { parseFilterSelection, serializeFilterSelection } from "../../lib/filterSelection";
import {
  fitTableColumns,
  visibleTableColumns,
  type TableColumnDefinition,
  type TableColumnLayoutItem,
} from "../../lib/tableColumnLayout";
import type { AssetsSortField, SavedColumnFilterValue, SavedViewSortDirection } from "../../types/savedViews";
import { ASSET_TIMELINE_COLUMN_CATALOG, type AssetTimelineColumnKey } from "./assetColumnCatalog";
import styles from "./AssetsPage.module.css";
import { AssetNotesIndicator, StatusIcon, formatTelemetryStatus } from "./AssetsTable";
import {
  toAssetTimelineDateValue,
  type AssetTimelineColumnFilters,
  type AssetTimelineFilterField,
  type AssetTimelineRow,
} from "./assetsTimelineModel";

const TIMELINE_TABLE_WIDTH_STORAGE_KEY = "assets.timelineTableWidth.v1";
const TIMELINE_TABLE_DEFAULT_WIDTH = 420;
const TIMELINE_TABLE_MIN_WIDTH = 320;
const TIMELINE_TABLE_MAX_WIDTH = 720;
const TIMELINE_CHART_MIN_WIDTH = 240;
const TIMELINE_TABLE_MAX_RATIO = 0.8;
const TIMELINE_RESIZE_HANDLE_WIDTH = 12;

interface TimelineResizeState {
  pointerId: number;
  startX: number;
  startWidth: number;
  maxWidth: number;
}

export interface AssetsTimelineProps {
  columnFilters: Readonly<AssetTimelineColumnFilters>;
  columnFilterDrafts?: Readonly<Partial<Record<AssetTimelineFilterField, string>>>;
  columnLayout: readonly TableColumnLayoutItem<AssetTimelineColumnKey>[];
  currentPage: number;
  divisionFilter: string;
  divisionOptions: ReadonlyArray<{ label: string; value: string }>;
  historyStart?: Date;
  rangeStart: Date;
  rows: readonly AssetTimelineRow[];
  selectionEnabled?: boolean;
  showAllDivisionOption: boolean;
  sortDirection: SavedViewSortDirection;
  sortField: AssetsSortField;
  tasks: GanttTask[];
  totalPages: number;
  warehouseOptions: ReadonlyArray<{ label: string; value: string }>;
  getAssetProfilePath: (assetId: string) => string;
  selectedAssetIds: ReadonlySet<string>;
  selectedAssetsOnPageCount: number;
  statusOptions: readonly string[];
  statusFilter: string;
  timelineEndDate?: string;
  timelineStartDate?: string;
  onColumnFilterChange: (field: AssetTimelineFilterField, value: SavedColumnFilterValue) => void;
  onColumnFilterDraftChange?: (field: AssetTimelineFilterField, value: string) => void;
  onColumnLayoutChange: (layout: TableColumnLayoutItem<AssetTimelineColumnKey>[]) => void;
  onDivisionFilterChange: (division: string) => void;
  onStatusFilterChange: (status: string) => void;
  onTimelinePeriodApply: (startDate: string, endDate: string) => void;
  onNextPage: () => void;
  onPreviousPage: () => void;
  onSelectAll: (checked: boolean) => void;
  onSelectAsset: (assetId: string, checked: boolean) => void;
  onSort: (field: AssetsSortField) => void;
}

export const AssetsTimeline: FC<AssetsTimelineProps> = ({
  columnFilters,
  columnFilterDrafts = {},
  columnLayout,
  currentPage,
  divisionFilter,
  divisionOptions,
  historyStart,
  rangeStart,
  rows,
  selectionEnabled = true,
  showAllDivisionOption,
  sortDirection,
  sortField,
  tasks,
  totalPages,
  warehouseOptions,
  getAssetProfilePath,
  selectedAssetIds,
  selectedAssetsOnPageCount,
  statusOptions,
  statusFilter,
  timelineEndDate = "",
  timelineStartDate = "",
  onColumnFilterChange,
  onColumnFilterDraftChange,
  onColumnLayoutChange,
  onDivisionFilterChange,
  onStatusFilterChange,
  onTimelinePeriodApply,
  onNextPage,
  onPreviousPage,
  onSelectAll,
  onSelectAsset,
  onSort,
}) => {
  const { t } = useTranslation();
  const assetGroups = useMemo(() => {
    const groups = new Map<string, { row: AssetTimelineRow; rowIds: string[]; laneIndexes: Set<number> }>();
    rows.forEach((row) => {
      const group = groups.get(row.asset.id);
      if (group) {
        group.rowIds.push(row.rowId);
        group.laneIndexes.add(row.laneIndex ?? group.rowIds.length - 1);
      } else {
        groups.set(row.asset.id, { row, rowIds: [row.rowId], laneIndexes: new Set([row.laneIndex ?? 0]) });
      }
    });
    return [...groups.values()];
  }, [rows]);
  const timelineAssetCount = assetGroups.length;
  const layoutRef = useRef<HTMLDivElement>(null);
  const metaBarRef = useRef<HTMLDivElement>(null);
  const tableRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<HTMLDivElement>(null);
  const ganttNavigationRef = useRef<FrappeGanttNavigation>(null);
  const hasJumpedToTodayRef = useRef(false);
  const [tableWidth, setTableWidth] = useState(readTimelineTableWidth);
  const [tableWidthLimit, setTableWidthLimit] = useState(TIMELINE_TABLE_MAX_WIDTH);
  const tableWidthRef = useRef(tableWidth);
  const resizeStateRef = useRef<TimelineResizeState | null>(null);
  const [isResizing, setIsResizing] = useState(false);
  const [headerSpacerHeight, setHeaderSpacerHeight] = useState(47);
  const [hoveredRowId, setHoveredRowId] = useState<string | null>(null);
  const stickyHeaderOffset = useTimelineStickyHeaders(metaBarRef, tableRef, rows);

  useEffect(() => {
    if (hasJumpedToTodayRef.current || tasks.length === 0) return;
    hasJumpedToTodayRef.current = true;
    const frame = requestAnimationFrame(() => ganttNavigationRef.current?.jumpToToday());
    return () => cancelAnimationFrame(frame);
  }, [tasks.length]);
  const [draftStartDate, setDraftStartDate] = useState(timelineStartDate);
  const [draftEndDate, setDraftEndDate] = useState(timelineEndDate);
  const isInvalidPeriod = Boolean(draftStartDate && draftEndDate && draftStartDate > draftEndDate);

  useEffect(() => setDraftStartDate(timelineStartDate), [timelineStartDate]);
  useEffect(() => setDraftEndDate(timelineEndDate), [timelineEndDate]);
  const selectionWidth = selectionEnabled ? 32 : 0;
  const preferredColumns = visibleTableColumns(columnLayout);
  const columns = useMemo(
    () => fitTableColumns(ASSET_TIMELINE_COLUMN_CATALOG, columnLayout, tableWidth - selectionWidth),
    [columnLayout, selectionWidth, tableWidth],
  );
  const contextTableWidth = columns.reduce((total, column) => total + column.width, selectionWidth);

  const getTableMaxWidth = useCallback(() => {
    const layoutWidth = layoutRef.current?.clientWidth;
    if (!layoutWidth) return TIMELINE_TABLE_MAX_WIDTH;
    return Math.max(
      240,
      Math.min(
        Math.floor(layoutWidth * TIMELINE_TABLE_MAX_RATIO) - TIMELINE_RESIZE_HANDLE_WIDTH,
        layoutWidth - TIMELINE_CHART_MIN_WIDTH - TIMELINE_RESIZE_HANDLE_WIDTH,
      ),
    );
  }, []);

  const updateTableWidth = useCallback(
    (width: number, maxWidth = getTableMaxWidth()) => {
      const nextWidth = Math.round(clamp(width, TIMELINE_TABLE_MIN_WIDTH, maxWidth));
      tableWidthRef.current = nextWidth;
      setTableWidth(nextWidth);
    },
    [getTableMaxWidth],
  );

  const handleResizeStart = useCallback(
    (event: PointerEvent<HTMLDivElement>) => {
      if (event.button !== 0) return;
      const maxWidth = getTableMaxWidth();
      setTableWidthLimit(maxWidth);
      event.preventDefault();
      event.currentTarget.setPointerCapture(event.pointerId);
      resizeStateRef.current = {
        pointerId: event.pointerId,
        startX: event.clientX,
        startWidth: tableWidthRef.current,
        maxWidth,
      };
      setIsResizing(true);
    },
    [getTableMaxWidth],
  );

  const handleResizeMove = useCallback(
    (event: PointerEvent<HTMLDivElement>) => {
      const state = resizeStateRef.current;
      if (!state || state.pointerId !== event.pointerId) return;
      updateTableWidth(state.startWidth + event.clientX - state.startX, state.maxWidth);
    },
    [updateTableWidth],
  );

  const handleResizeEnd = useCallback((event: PointerEvent<HTMLDivElement>) => {
    const state = resizeStateRef.current;
    if (!state || state.pointerId !== event.pointerId) return;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    persistTimelineTableWidth(tableWidthRef.current);
    resizeStateRef.current = null;
    setIsResizing(false);
  }, []);

  const handleResizeKeyDown = useCallback(
    (event: KeyboardEvent<HTMLDivElement>) => {
      const direction = event.key === "ArrowLeft" ? -1 : event.key === "ArrowRight" ? 1 : 0;
      if (!direction) return;
      event.preventDefault();
      updateTableWidth(tableWidthRef.current + direction * (event.shiftKey ? 80 : 24));
      persistTimelineTableWidth(tableWidthRef.current);
    },
    [updateTableWidth],
  );

  useEffect(() => {
    const alignRows = () => {
      const tableHead = tableRef.current?.querySelector("thead") as HTMLElement | null;
      const firstTableRow = tableRef.current?.querySelector("tbody tr[data-timeline-row='true']") as HTMLElement | null;
      const firstGridRow = chartRef.current?.querySelector(".gantt .grid-row") as SVGGraphicsElement | null;

      if (!tableHead || !firstTableRow || !firstGridRow) {
        return;
      }

      const headBottom = tableHead.getBoundingClientRect().bottom;
      const firstGridRowTop = firstGridRow.getBoundingClientRect().top;

      // Spacer sits between header and first data row; align to gantt grid lanes.
      const nextSpacer = Math.max(0, Math.round(firstGridRowTop - headBottom));

      // Keep a stable fallback if measurement briefly fails during render/layout churn.
      if (nextSpacer >= 0 && nextSpacer <= 90) {
        setHeaderSpacerHeight(nextSpacer);
      }
    };

    const constrainPane = () => {
      const maxWidth = getTableMaxWidth();
      setTableWidthLimit(maxWidth);
      updateTableWidth(tableWidthRef.current, maxWidth);
    };
    const raf = requestAnimationFrame(() => {
      constrainPane();
      alignRows();
    });
    const resizeHandler = () =>
      requestAnimationFrame(() => {
        constrainPane();
        alignRows();
      });
    const settle1 = window.setTimeout(alignRows, 120);
    const settle2 = window.setTimeout(alignRows, 360);
    window.addEventListener("resize", resizeHandler);
    const printMedia = window.matchMedia?.("print");
    const alignPrintRows = () => {
      alignRows();
      requestAnimationFrame(alignRows);
    };
    printMedia?.addEventListener("change", alignPrintRows);
    window.addEventListener("beforeprint", alignPrintRows);
    window.addEventListener("afterprint", alignPrintRows);

    return () => {
      cancelAnimationFrame(raf);
      window.clearTimeout(settle1);
      window.clearTimeout(settle2);
      window.removeEventListener("resize", resizeHandler);
      printMedia?.removeEventListener("change", alignPrintRows);
      window.removeEventListener("beforeprint", alignPrintRows);
      window.removeEventListener("afterprint", alignPrintRows);
    };
  }, [currentPage, getTableMaxWidth, rows, updateTableWidth]);

  return (
    <>
      <div className={styles.timelinePanel}>
        <div className={styles.timelineMetaBar} ref={metaBarRef}>
          <div className={styles.timelineLegendGroup}>
            <span className={styles.timelineMetaLabel}>Event key</span>
            <div className={styles.legend}>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendRingfence}`}></span>
                Ringfence
              </span>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendOnHire}`}></span>
                On Hire
              </span>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendReserved}`}></span>
                Reserved
              </span>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendService}`}></span>
                Service
              </span>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendRepair}`}></span>
                Repair
              </span>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendCollection}`}></span>
                Collection
              </span>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendTransport}`}></span>
                Transport
              </span>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendOnHold}`}></span>
                On Hold
              </span>
            </div>
          </div>
          <span className={styles.timelinePeriodControls} data-print-hidden>
            <label>
              <span>{t("tableColumns.fields.fromDate")}</span>
              <input
                type="date"
                aria-label={t("tableColumns.fields.fromDate")}
                value={draftStartDate}
                max={draftEndDate || undefined}
                onChange={(event) => setDraftStartDate(event.target.value)}
              />
            </label>
            <label>
              <span>{t("tableColumns.fields.toDate")}</span>
              <input
                type="date"
                aria-label={t("tableColumns.fields.toDate")}
                value={draftEndDate}
                min={draftStartDate || undefined}
                onChange={(event) => setDraftEndDate(event.target.value)}
              />
            </label>
            <Button
              label={t("timeline.applyPeriod")}
              variant="secondary"
              size="small"
              disabled={isInvalidPeriod}
              onClick={() => onTimelinePeriodApply(draftStartDate, draftEndDate)}
            />
          </span>
        </div>
        <div className={styles.timelineLayout} ref={layoutRef} data-print-timeline>
          <div className={styles.timelineTable} ref={tableRef} style={{ width: `${tableWidth}px` }}>
            <table className={styles.compactTable} style={{ width: `${contextTableWidth}px` }}>
              <DataGridColumnGroup
                columns={columns}
                preferredColumns={preferredColumns}
                leadingColumns={
                  selectionEnabled ? [{ key: "selection", className: styles.selectColumn, printHidden: true }] : []
                }
              />
              <thead>
                <tr>
                  {selectionEnabled && (
                    <th className={styles.checkboxCol} data-print-hidden>
                      <input
                        type="checkbox"
                        checked={selectedAssetsOnPageCount === timelineAssetCount && timelineAssetCount > 0}
                        aria-label={t("assets.ringfence.timelineSelectAll")}
                        onChange={(event) => onSelectAll(event.target.checked)}
                      />
                    </th>
                  )}
                  <DataGridColumnHeaders
                    catalogue={ASSET_TIMELINE_COLUMN_CATALOG}
                    columns={columns}
                    layout={columnLayout}
                    headerClassName={styles.resizableColumnHeader}
                    getLabel={(definition) => t(definition.labelKey)}
                    getHeaderAriaLabel={(definition, label) =>
                      definition.compactLabelKey ? t(definition.compactLabelKey) : label
                    }
                    renderHeader={(column, definition, label) => (
                      <TableColumnHeader
                        label={label}
                        compactLabel={definition.compactLabelKey ? t(definition.compactLabelKey) : undefined}
                        sortDirection={sortField === column.key ? sortDirection : undefined}
                        onSort={() => onSort(column.key)}
                      />
                    )}
                    onLayoutChange={onColumnLayoutChange}
                  />
                </tr>
                <tr className={styles.timelineFilterRow} data-print-hidden>
                  {selectionEnabled && <th className={styles.checkboxCol}></th>}
                  {columns.map((column) => (
                    <th key={column.key}>
                      {renderTimelineFilter(
                        column.key,
                        columnFilters,
                        columnFilterDrafts,
                        divisionFilter,
                        divisionOptions,
                        showAllDivisionOption,
                        statusFilter,
                        statusOptions,
                        warehouseOptions,
                        onColumnFilterChange,
                        onColumnFilterDraftChange,
                        onDivisionFilterChange,
                        onStatusFilterChange,
                        t,
                      )}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {headerSpacerHeight > 0 && (
                  <tr className={styles.timelineHeaderSpacer} aria-hidden="true">
                    <td
                      colSpan={columns.length + (selectionEnabled ? 1 : 0)}
                      style={{ height: `${headerSpacerHeight}px` }}
                    ></td>
                  </tr>
                )}
                {rows.length === 0 ? (
                  <tr>
                    <td className={styles.timelineEmptyCell} colSpan={columns.length + (selectionEnabled ? 1 : 0)}>
                      No assets match the timeline filters.
                    </td>
                  </tr>
                ) : (
                  assetGroups.map(({ row, rowIds, laneIndexes }, index) => (
                    <tr
                      key={row.rowId}
                      className={`${styles.timelineRow} ${index % 5 === 4 ? styles.groupRow : ""} ${rowIds.includes(hoveredRowId ?? "") ? styles.timelineRowHovered : ""}`}
                      data-timeline-row="true"
                      style={{ height: `${laneIndexes.size * 38}px` }}
                      onMouseEnter={() => setHoveredRowId(row.rowId)}
                      onMouseLeave={() => setHoveredRowId((previous) => (previous === row.rowId ? null : previous))}
                    >
                      {selectionEnabled && (
                        <td className={styles.checkboxCol} data-print-hidden>
                          <input
                            type="checkbox"
                            checked={selectedAssetIds.has(row.asset.id)}
                            aria-label={t("assets.ringfence.timelineSelectAsset", { assetId: row.asset.id })}
                            onChange={(event) => onSelectAsset(row.asset.id, event.target.checked)}
                          />
                        </td>
                      )}
                      {columns.map((column) => renderTimelineCell(column.key, row, getAssetProfilePath(row.asset.id)))}
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          <div
            className={`${styles.timelineResizeHandle} ${isResizing ? styles.timelineResizeHandleActive : ""}`}
            role="separator"
            aria-orientation="vertical"
            aria-label={t("tableColumns.resizeAssetDetails")}
            aria-valuemin={Math.min(TIMELINE_TABLE_MIN_WIDTH, tableWidthLimit)}
            aria-valuemax={tableWidthLimit}
            aria-valuenow={tableWidth}
            tabIndex={0}
            title={t("tableColumns.resizeTimelineHint")}
            onPointerDown={handleResizeStart}
            onPointerMove={handleResizeMove}
            onPointerUp={handleResizeEnd}
            onPointerCancel={handleResizeEnd}
            onKeyDown={handleResizeKeyDown}
          />
          <div className={styles.timelineChart} ref={chartRef}>
            <FrappeGantt
              ref={ganttNavigationRef}
              tasks={tasks}
              viewMode="Day"
              minimumDate={toAssetTimelineDateValue(historyStart ?? rangeStart)}
              focusDate={timelineStartDate ? rangeStart.getTime() : null}
              stickyHeaderOffset={stickyHeaderOffset}
              readonlyDates
              hoveredTaskId={hoveredRowId}
              onHoverTaskIdChange={setHoveredRowId}
            />
          </div>
        </div>
      </div>
      {totalPages > 1 && (
        <DataGridPagination
          currentPage={currentPage}
          totalPages={totalPages}
          labels={{
            previous: "← Prev",
            next: "Next →",
            page: (current, total) => `Page ${current} of ${total}`,
          }}
          onNextPage={onNextPage}
          onPreviousPage={onPreviousPage}
        />
      )}
    </>
  );
};

function renderTimelineFilter(
  column: AssetTimelineColumnKey,
  filters: Readonly<AssetTimelineColumnFilters>,
  drafts: Readonly<Partial<Record<AssetTimelineFilterField, string>>>,
  divisionFilter: string,
  divisionOptions: ReadonlyArray<{ label: string; value: string }>,
  showAllDivisionOption: boolean,
  statusFilter: string,
  statusOptions: readonly string[],
  warehouseOptions: ReadonlyArray<{ label: string; value: string }>,
  onChange: (field: AssetTimelineFilterField, value: SavedColumnFilterValue) => void,
  onDraftChange: ((field: AssetTimelineFilterField, value: string) => void) | undefined,
  onDivisionFilterChange: (division: string) => void,
  onStatusFilterChange: (status: string) => void,
  t: ReturnType<typeof useTranslation>["t"],
): ReactNode {
  const definition: TableColumnDefinition<AssetTimelineColumnKey> = ASSET_TIMELINE_COLUMN_CATALOG.find(
    (item) => item.key === column,
  )!;
  const label = t(definition.labelKey);
  if (column === "division")
    return (
      <TableColumnFilter
        includeAllOption={showAllDivisionOption}
        label={label}
        multiValue
        value={parseFilterSelection(divisionFilter)}
        onChange={(value) => onDivisionFilterChange(serializeFilterSelection(value))}
        options={divisionOptions}
      />
    );
  if (column === "warehouse")
    return (
      <TableColumnFilter
        label={label}
        multiValue
        value={toMultiValueTextFilter(filters.warehouse)}
        onChange={(value) => onChange(column, value)}
        options={warehouseOptions}
      />
    );
  if (
    column === "deliveryDate" ||
    column === "agreementLineValidFromDate" ||
    column === "agreementLineValidToDate" ||
    column === "terminationDate" ||
    column === "collectionDate" ||
    column === "estimatedReadyDate"
  )
    return (
      <TableColumnFilter
        label={label}
        type="date"
        value={typeof filters[column] === "string" ? filters[column] : ""}
        onChange={(value) => onChange(column, value)}
      />
    );
  const isMultiValue =
    column === "id" ||
    column === "itemNumber" ||
    column === "description" ||
    column === "warehouseName" ||
    column === "agreementNumber" ||
    column === "customerNumber" ||
    column === "customerName" ||
    column === "facility" ||
    column === "warehouseLocation" ||
    column === "productGroup" ||
    column === "productCategory" ||
    column === "size" ||
    column === "telemetryStatus" ||
    column === "remark" ||
    column === "status";
  return (
    <TableColumnFilter
      label={label}
      {...(isMultiValue
        ? {
            multiValue: true as const,
            value: column === "status" ? parseFilterSelection(statusFilter) : toMultiValueTextFilter(filters[column]),
            onChange: (value: string[]) =>
              column === "status" ? onStatusFilterChange(serializeFilterSelection(value)) : onChange(column, value),
            ...(column === "status"
              ? { options: statusOptions.map((status) => ({ label: status, value: status })) }
              : onDraftChange
                ? {
                    draftValue: drafts[column] ?? "",
                    onDraftChange: (value: string) => onDraftChange(column, value),
                  }
                : {}),
          }
        : {
            value: typeof filters[column] === "string" ? filters[column] : "",
            onChange: (value: string) => onChange(column, value),
          })}
    />
  );
}

function renderTimelineCell(
  column: AssetTimelineColumnKey,
  row: AssetTimelineRow,
  assetProfilePath: string,
): ReactNode {
  const { asset } = row;
  switch (column) {
    case "status":
      return (
        <td key={column} className={styles.statusCell}>
          <StatusIcon status={asset.status} />
        </td>
      );
    case "id":
      return (
        <td key={column} className={styles.assetId}>
          <span className={styles.identityWithIndicator}>
            <Link className={styles.assetLink} to={assetProfilePath}>
              {asset.id}
            </Link>
            <AssetNotesIndicator count={asset.noteCount} />
          </span>
        </td>
      );
    case "itemNumber":
      return (
        <td key={column} className={styles.mono}>
          {asset.itemNumber ?? "—"}
        </td>
      );
    case "description":
      return (
        <td key={column} className={styles.descriptionCell}>
          {asset.description ?? "—"}
        </td>
      );
    case "warehouse":
      return (
        <td key={column} title={asset.warehouse ?? undefined}>
          {asset.warehouse ?? "—"}
        </td>
      );
    case "warehouseName":
      return <td key={column}>{asset.warehouseName ?? "—"}</td>;
    case "division":
      return <td key={column}>{asset.division ?? "—"}</td>;
    case "agreementNumber":
      return <td key={column}>{asset.agreementNumber ?? "—"}</td>;
    case "customerNumber":
      return <td key={column}>{asset.customerNumber ?? "—"}</td>;
    case "customerName":
      return <td key={column}>{asset.customerName ?? "—"}</td>;
    case "facility":
      return <td key={column}>{asset.facility ?? "—"}</td>;
    case "warehouseLocation":
      return <td key={column}>{asset.warehouseLocation ?? "—"}</td>;
    case "deliveryDate":
      return <td key={column}>{formatDate(asset.deliveryDate)}</td>;
    case "agreementLineValidFromDate":
      return <td key={column}>{formatDate(asset.agreementLineValidFromDate)}</td>;
    case "agreementLineValidToDate":
      return <td key={column}>{formatDate(asset.agreementLineValidToDate)}</td>;
    case "terminationDate":
      return <td key={column}>{formatDate(asset.terminationDate)}</td>;
    case "collectionDate":
      return <td key={column}>{formatDate(asset.collectionDate)}</td>;
    case "daysOffHire":
      return <td key={column}>{asset.daysOffHire ?? "—"}</td>;
    case "estimatedReadyDate":
      return <td key={column}>{formatDate(asset.estimatedReadyDate)}</td>;
    case "productGroup":
      return <td key={column}>{asset.productGroup ?? "—"}</td>;
    case "productCategory":
      return <td key={column}>{asset.productCategory ?? "—"}</td>;
    case "runHours":
      return <td key={column}>{asset.runHours ?? "—"}</td>;
    case "size":
      return <td key={column}>{asset.size ?? "—"}</td>;
    case "telemetryStatus":
      return <td key={column}>{formatTelemetryStatus(asset.telemetryStatus)}</td>;
    case "remark":
      return <td key={column}>{asset.remark ?? "—"}</td>;
  }
}

function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function readTimelineTableWidth(): number {
  if (typeof window === "undefined") return TIMELINE_TABLE_DEFAULT_WIDTH;
  try {
    const stored = window.localStorage.getItem(TIMELINE_TABLE_WIDTH_STORAGE_KEY);
    const width = stored == null ? Number.NaN : Number(stored);
    if (Number.isFinite(width)) return Math.round(clamp(width, TIMELINE_TABLE_MIN_WIDTH, TIMELINE_TABLE_MAX_WIDTH));
  } catch {
    // Keep the default when browser storage is unavailable.
  }
  return TIMELINE_TABLE_DEFAULT_WIDTH;
}

function persistTimelineTableWidth(width: number): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(TIMELINE_TABLE_WIDTH_STORAGE_KEY, String(width));
  } catch {
    // Resizing remains available when browser storage is unavailable.
  }
}
