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
import type { AgreementListItem } from "../../services/agreementsService";
import { ApiFulfilmentStatus } from "../../types";
import type { AgreementsSortField, SavedColumnFilterValue, SavedViewSortDirection } from "../../types/savedViews";
import { AGREEMENT_TIMELINE_COLUMN_CATALOG, type AgreementTimelineColumnKey } from "./agreementColumnCatalog";
import styles from "./AgreementsPage.module.css";
import { AgreementNotesIndicator, FulfilmentStatusIcon, formatDate } from "./AgreementsTable";
import type { AgreementTimelineColumnFilters, AgreementTimelineFilterField } from "./agreementsTimelineModel";

const TIMELINE_TABLE_WIDTH_STORAGE_KEY = "agreements.timelineTableWidth.v1";
const TIMELINE_TABLE_DEFAULT_WIDTH = 500;
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

export interface AgreementsTimelineProps {
  agreements: readonly AgreementListItem[];
  columnFilters: Readonly<AgreementTimelineColumnFilters>;
  columnFilterDrafts?: Readonly<Partial<Record<AgreementTimelineFilterField, string>>>;
  columnLayout: readonly TableColumnLayoutItem<AgreementTimelineColumnKey>[];
  currentPage: number;
  historyStart: string;
  sortDirection: SavedViewSortDirection;
  sortField: AgreementsSortField;
  statusFilter: string;
  timelineEndDate?: string;
  timelineStartDate?: string;
  tasks: GanttTask[];
  totalPages: number;
  warehouseOptions: ReadonlyArray<{ label: string; value: string }>;
  onAgreementOpen: (agreementId: number) => void;
  onColumnFilterChange: (field: AgreementTimelineFilterField, value: SavedColumnFilterValue) => void;
  onColumnFilterDraftChange?: (field: AgreementTimelineFilterField, value: string) => void;
  onColumnLayoutChange: (layout: TableColumnLayoutItem<AgreementTimelineColumnKey>[]) => void;
  onStatusFilterChange: (status: string) => void;
  onTimelinePeriodApply: (startDate: string, endDate: string) => void;
  onNextPage: () => void;
  onPreviousPage: () => void;
  onSort: (field: AgreementsSortField) => void;
}

export const AgreementsTimeline: FC<AgreementsTimelineProps> = ({
  agreements,
  columnFilters,
  columnFilterDrafts = {},
  columnLayout,
  currentPage,
  historyStart,
  sortDirection,
  sortField,
  statusFilter,
  timelineEndDate = "",
  timelineStartDate = "",
  tasks,
  totalPages,
  warehouseOptions,
  onAgreementOpen,
  onColumnFilterChange,
  onColumnFilterDraftChange,
  onColumnLayoutChange,
  onStatusFilterChange,
  onTimelinePeriodApply,
  onNextPage,
  onPreviousPage,
  onSort,
}) => {
  const { t } = useTranslation();
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
  const [hoveredAgreementId, setHoveredAgreementId] = useState<string | null>(null);
  const stickyHeaderOffset = useTimelineStickyHeaders(metaBarRef, tableRef, agreements);
  const [draftStartDate, setDraftStartDate] = useState(timelineStartDate);
  const [draftEndDate, setDraftEndDate] = useState(timelineEndDate);
  const isInvalidPeriod = Boolean(draftStartDate && draftEndDate && draftStartDate > draftEndDate);

  useEffect(() => setDraftStartDate(timelineStartDate), [timelineStartDate]);
  useEffect(() => setDraftEndDate(timelineEndDate), [timelineEndDate]);

  useEffect(() => {
    if (hasJumpedToTodayRef.current || tasks.length === 0) return;
    hasJumpedToTodayRef.current = true;
    const frame = requestAnimationFrame(() => ganttNavigationRef.current?.jumpToToday());
    return () => cancelAnimationFrame(frame);
  }, [tasks.length]);

  const getTableMaxWidth = useCallback(() => {
    const layoutWidth = layoutRef.current?.clientWidth;
    if (!layoutWidth) {
      return TIMELINE_TABLE_MAX_WIDTH;
    }

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

  const preferredColumns = visibleTableColumns(columnLayout);
  const columns = useMemo(
    () => fitTableColumns(AGREEMENT_TIMELINE_COLUMN_CATALOG, columnLayout, tableWidth),
    [columnLayout, tableWidth],
  );
  const contextTableWidth = columns.reduce((total, column) => total + column.width, 0);

  const handleResizeStart = useCallback(
    (event: PointerEvent<HTMLDivElement>) => {
      if (event.button !== 0) {
        return;
      }

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
      const resizeState = resizeStateRef.current;
      if (!resizeState || resizeState.pointerId !== event.pointerId) {
        return;
      }

      updateTableWidth(resizeState.startWidth + event.clientX - resizeState.startX, resizeState.maxWidth);
    },
    [updateTableWidth],
  );

  const handleResizeEnd = useCallback((event: PointerEvent<HTMLDivElement>) => {
    const resizeState = resizeStateRef.current;
    if (!resizeState || resizeState.pointerId !== event.pointerId) {
      return;
    }

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
      if (direction === 0) {
        return;
      }

      event.preventDefault();
      const step = event.shiftKey ? 80 : 24;
      updateTableWidth(tableWidthRef.current + direction * step);
      persistTimelineTableWidth(tableWidthRef.current);
    },
    [updateTableWidth],
  );

  useEffect(() => {
    const alignRows = () => {
      const tableHead = tableRef.current?.querySelector("thead") as HTMLElement | null;
      const firstGridRow = chartRef.current?.querySelector(".gantt .grid-row") as SVGGraphicsElement | null;

      if (!tableHead || !firstGridRow) {
        return;
      }

      const headBottom = tableHead.getBoundingClientRect().bottom;
      const firstGridRowTop = firstGridRow.getBoundingClientRect().top;
      const nextSpacer = Math.max(0, Math.round(firstGridRowTop - headBottom));

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

    return () => {
      cancelAnimationFrame(raf);
      window.clearTimeout(settle1);
      window.clearTimeout(settle2);
      window.removeEventListener("resize", resizeHandler);
    };
  }, [agreements, currentPage, getTableMaxWidth, updateTableWidth]);

  return (
    <>
      <div className={styles.timelinePanel}>
        <div className={styles.timelineMetaBar} ref={metaBarRef}>
          <div className={styles.timelineLegendGroup}>
            <span className={styles.timelineMetaLabel}>{t("agreements.fulfilmentStatus")}</span>
            <div className={styles.legend}>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendUnfulfilled}`}></span>
                {t("fulfilmentStatus.unfulfilled")}
              </span>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendPartial}`}></span>
                {t("fulfilmentStatus.partiallyFulfilled")}
              </span>
              <span className={styles.legendItem}>
                <span className={`${styles.legendSwatch} ${styles.legendFulfilled}`}></span>
                {t("fulfilmentStatus.fullyFulfilled")}
              </span>
            </div>
          </div>
          <div className={styles.timelinePeriodControls} data-print-hidden>
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
          </div>
        </div>
        <div className={styles.timelineLayout} ref={layoutRef} data-print-timeline>
          <div className={styles.timelineTable} ref={tableRef} style={{ width: `${tableWidth}px` }}>
            <table className={styles.compactTable} style={{ width: `${contextTableWidth}px` }}>
              <DataGridColumnGroup columns={columns} preferredColumns={preferredColumns} />
              <thead>
                <tr>
                  <DataGridColumnHeaders
                    catalogue={AGREEMENT_TIMELINE_COLUMN_CATALOG}
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
                  {columns.map((column) => (
                    <th key={column.key}>
                      {renderTimelineFilter(
                        column.key,
                        columnFilters,
                        columnFilterDrafts,
                        statusFilter,
                        warehouseOptions,
                        onColumnFilterChange,
                        onColumnFilterDraftChange,
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
                    <td colSpan={columns.length} style={{ height: `${headerSpacerHeight}px` }}></td>
                  </tr>
                )}
                {agreements.length === 0 ? (
                  <tr>
                    <td className={styles.timelineEmptyCell} colSpan={columns.length}>
                      No agreements match the timeline filters.
                    </td>
                  </tr>
                ) : (
                  agreements.map((agreement, index) => (
                    <tr
                      key={agreement.id}
                      className={`${styles.timelineRow} ${index % 5 === 4 ? styles.groupRow : ""} ${hoveredAgreementId === String(agreement.id) ? styles.timelineRowHovered : ""}`}
                      onClick={() => onAgreementOpen(agreement.id)}
                      onMouseEnter={() => setHoveredAgreementId(String(agreement.id))}
                      onMouseLeave={() =>
                        setHoveredAgreementId((previous) => (previous === String(agreement.id) ? null : previous))
                      }
                    >
                      {columns.map((column) => renderTimelineCell(column.key, agreement))}
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
            aria-label={t("tableColumns.resizeAgreementDetails")}
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
              minimumDate={historyStart}
              focusDate={timelineStartDate ? new Date(`${timelineStartDate}T00:00:00`).getTime() : null}
              stickyHeaderOffset={stickyHeaderOffset}
              readonlyDates
              readonlyProgress
              hoveredTaskId={hoveredAgreementId}
              onHoverTaskIdChange={setHoveredAgreementId}
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
  column: AgreementTimelineColumnKey,
  filters: Readonly<AgreementTimelineColumnFilters>,
  drafts: Readonly<Partial<Record<AgreementTimelineFilterField, string>>>,
  statusFilter: string,
  warehouseOptions: ReadonlyArray<{ label: string; value: string }>,
  onChange: (field: AgreementTimelineFilterField, value: SavedColumnFilterValue) => void,
  onDraftChange: ((field: AgreementTimelineFilterField, value: string) => void) | undefined,
  onStatusFilterChange: (status: string) => void,
  t: ReturnType<typeof useTranslation>["t"],
): ReactNode {
  const definition: TableColumnDefinition<AgreementTimelineColumnKey> = AGREEMENT_TIMELINE_COLUMN_CATALOG.find(
    (item) => item.key === column,
  )!;
  const label = t(definition.filterLabelKey ?? definition.labelKey);
  if (column === "fulfilmentStatus")
    return (
      <TableColumnFilter
        label={label}
        multiValue
        value={parseFilterSelection(statusFilter)}
        onChange={(value) => onStatusFilterChange(serializeFilterSelection(value))}
        options={[
          { label: t("fulfilmentStatus.unfulfilled"), value: String(ApiFulfilmentStatus.Unfulfilled) },
          { label: t("fulfilmentStatus.partiallyFulfilled"), value: String(ApiFulfilmentStatus.PartiallyFulfilled) },
          { label: t("fulfilmentStatus.fullyFulfilled"), value: String(ApiFulfilmentStatus.FullyFulfilled) },
        ]}
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
    column === "validFromDate" ||
    column === "validToDate" ||
    column === "terminationDate" ||
    column === "collectionDate" ||
    column === "fromDate" ||
    column === "toDate" ||
    column === "lastUpdatedDate"
  )
    return (
      <TableColumnFilter
        label={label}
        type="date"
        value={typeof filters[column] === "string" ? filters[column] : ""}
        onChange={(value) => onChange(column, value)}
      />
    );
  const filterKey: AgreementTimelineFilterField = column === "customerName" ? "customerOrOpportunity" : column;
  const isMultiValue =
    column === "agreementNumber" ||
    column === "customerName" ||
    column === "customerNumber" ||
    column === "customerAddress" ||
    column === "opportunityName" ||
    column === "opportunityStage" ||
    column === "lastUpdatedByName";
  return (
    <TableColumnFilter
      label={label}
      {...(isMultiValue
        ? {
            multiValue: true as const,
            value: toMultiValueTextFilter(filters[filterKey]),
            onChange: (value: string[]) => onChange(filterKey, value),
            ...(onDraftChange
              ? {
                  draftValue: drafts[filterKey] ?? "",
                  onDraftChange: (value: string) => onDraftChange(filterKey, value),
                }
              : {}),
          }
        : {
            value: typeof filters[filterKey] === "string" ? filters[filterKey] : "",
            onChange: (value: string) => onChange(filterKey, value),
          })}
    />
  );
}

function renderTimelineCell(column: AgreementTimelineColumnKey, agreement: AgreementListItem): ReactNode {
  switch (column) {
    case "fulfilmentStatus":
      return (
        <td key={column} className={styles.statusCell}>
          <FulfilmentStatusIcon status={agreement.fulfilmentStatus} />
        </td>
      );
    case "agreementNumber":
      return (
        <td key={column} className={styles.agreementCell}>
          <span className={styles.identityWithIndicator}>
            {agreement.agreementNumber ?? "—"}
            <AgreementNotesIndicator count={agreement.noteCount} />
          </span>
        </td>
      );
    case "customerName":
      return (
        <td key={column} className={styles.timelineContextCell}>
          <span className={styles.timelineCustomerName}>{agreement.customerName ?? "—"}</span>
        </td>
      );
    case "opportunityName":
      return <td key={column}>{agreement.opportunityName ?? "—"}</td>;
    case "division":
      return <td key={column}>{agreement.division || "—"}</td>;
    case "warehouse":
      return <td key={column}>{agreement.warehouse ?? "—"}</td>;
    case "deliveryDate":
      return <td key={column}>{formatDate(agreement.deliveryDate)}</td>;
    case "validFromDate":
      return <td key={column}>{formatDate(agreement.validFromDate)}</td>;
    case "validToDate":
      return <td key={column}>{formatDate(agreement.validToDate)}</td>;
    case "terminationDate":
      return <td key={column}>{formatDate(agreement.terminationDate)}</td>;
    case "collectionDate":
      return <td key={column}>{formatDate(agreement.collectionDate)}</td>;
    case "fromDate":
      return <td key={column}>{formatDate(agreement.fromDate)}</td>;
    case "toDate":
      return <td key={column}>{formatDate(agreement.toDate)}</td>;
    case "customerNumber":
      return <td key={column}>{agreement.customerNumber ?? "—"}</td>;
    case "customerAddress":
      return <td key={column}>{agreement.customerAddress ?? "—"}</td>;
    case "lastUpdatedByName":
      return <td key={column}>{agreement.lastUpdatedByName ?? "—"}</td>;
    case "lastUpdatedDate":
      return <td key={column}>{formatDate(agreement.lastUpdatedDate)}</td>;
    case "lineCount":
      return <td key={column}>{agreement.lineCount ?? "—"}</td>;
    case "opportunityStage":
      return <td key={column}>{agreement.opportunityStage ?? "—"}</td>;
    case "probability":
      return <td key={column}>{agreement.probability == null ? "—" : `${agreement.probability}%`}</td>;
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

function readTimelineTableWidth(): number {
  if (typeof window === "undefined") {
    return TIMELINE_TABLE_DEFAULT_WIDTH;
  }

  try {
    const persistedValue = window.localStorage.getItem(TIMELINE_TABLE_WIDTH_STORAGE_KEY);
    const persisted = persistedValue == null ? Number.NaN : Number(persistedValue);
    if (Number.isFinite(persisted)) {
      return Math.round(clamp(persisted, TIMELINE_TABLE_MIN_WIDTH, TIMELINE_TABLE_MAX_WIDTH));
    }
  } catch {
    // Keep the default when browser storage is unavailable.
  }

  return TIMELINE_TABLE_DEFAULT_WIDTH;
}

function persistTimelineTableWidth(width: number): void {
  if (typeof window === "undefined") {
    return;
  }

  try {
    window.localStorage.setItem(TIMELINE_TABLE_WIDTH_STORAGE_KEY, String(width));
  } catch {
    // The resize control remains usable even when browser storage is unavailable.
  }
}
