import type { FC, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { BsCheckCircle, BsCircleHalf, BsDashCircle, BsPencilSquare, BsXCircle } from "react-icons/bs";
import {
  DataGridColumnGroup,
  DataGridColumnHeaders,
  DataGridPagination,
  DateColumnFilter,
  TableColumnFilter,
  TableColumnHeader,
  useResponsiveTableColumns,
} from "../../components/common";
import type { DateColumnFilterValue } from "../../components/common/TableColumnControls/dateColumnFilterModel";
import { toMultiValueTextFilter } from "../../components/common/TableColumnControls/multiValueTextFilterModel";
import {
  visibleTableColumns,
  type TableColumnDefinition,
  type TableColumnLayoutItem,
} from "../../lib/tableColumnLayout";
import type { AgreementListItem } from "../../services/agreementsService";
import { ApiFulfilmentStatus, mapFulfilmentStatus } from "../../types";
import type { AgreementsSortField, SavedColumnFilterValue, SavedViewSortDirection } from "../../types/savedViews";
import { parseFilterSelection, serializeFilterSelection } from "../../lib/filterSelection";
import { AGREEMENT_COLUMN_CATALOG, type AgreementColumnKey } from "./agreementColumnCatalog";
import styles from "./AgreementsPage.module.css";

export interface AgreementsTableProps {
  agreements: readonly AgreementListItem[];
  columnFilters: Readonly<Partial<Record<AgreementsSortField, SavedColumnFilterValue>>>;
  columnFilterDrafts?: Readonly<Partial<Record<AgreementsSortField, string>>>;
  columnLayout: readonly TableColumnLayoutItem<AgreementColumnKey>[];
  dateColumnFilters?: Readonly<Partial<Record<AgreementsSortField, DateColumnFilterValue>>>;
  currentPage: number;
  divisionFilter: string;
  divisionOptions: ReadonlyArray<{ label: string; value: string }>;
  pageSize: number;
  showAllDivisionOption: boolean;
  sortDirection: SavedViewSortDirection;
  sortField: AgreementsSortField;
  statusFilter: string;
  totalPages: number;
  warehouseOptions: ReadonlyArray<{ label: string; value: string }>;
  onColumnFilterChange: (field: AgreementsSortField, value: SavedColumnFilterValue) => void;
  onColumnFilterDraftChange?: (field: AgreementsSortField, value: string) => void;
  onColumnLayoutChange: (layout: TableColumnLayoutItem<AgreementColumnKey>[]) => void;
  onDateColumnFilterChange?: (field: AgreementsSortField, value: DateColumnFilterValue | undefined) => void;
  onDivisionFilterChange: (division: string) => void;
  onStatusFilterChange: (status: string) => void;
  onNextPage: () => void;
  onPageSizeChange: (pageSize: number) => void;
  onPreviousPage: () => void;
  onOrderNumberClick: (id: number) => void;
  onRowClick: (id: number) => void;
  onSort: (field: AgreementsSortField) => void;
}

const dateFields: ReadonlySet<AgreementsSortField> = new Set([
  "deliveryDate",
  "validFromDate",
  "validToDate",
  "terminationDate",
  "collectionDate",
  "fromDate",
  "toDate",
  "lastUpdatedDate",
]);

export const AgreementsTable: FC<AgreementsTableProps> = ({
  agreements,
  columnFilters,
  columnFilterDrafts = {},
  columnLayout,
  dateColumnFilters = {},
  currentPage,
  divisionFilter,
  divisionOptions,
  pageSize,
  showAllDivisionOption,
  sortDirection,
  sortField,
  statusFilter,
  totalPages,
  warehouseOptions,
  onColumnFilterChange,
  onColumnFilterDraftChange,
  onColumnLayoutChange,
  onDateColumnFilterChange = () => {},
  onDivisionFilterChange,
  onStatusFilterChange,
  onNextPage,
  onPageSizeChange,
  onPreviousPage,
  onOrderNumberClick,
  onRowClick,
  onSort,
}) => {
  const { t } = useTranslation();
  const preferredColumns = visibleTableColumns(columnLayout);
  const definitions = new Map<AgreementColumnKey, TableColumnDefinition<AgreementColumnKey>>(
    AGREEMENT_COLUMN_CATALOG.map((column) => [column.key, column]),
  );
  const { columns, minimumSurfaceWidth, tableWidth, viewportRef } = useResponsiveTableColumns(
    AGREEMENT_COLUMN_CATALOG,
    columnLayout,
  );

  return (
    <div
      className={styles.tableContainer}
      data-print-table="agreements"
      role="region"
      aria-label="Agreement rows"
      tabIndex={0}
    >
      <div className={styles.tableSurface} style={{ minWidth: minimumSurfaceWidth }}>
        <div className={styles.tableHeaderWrapper}>
          <table className={`${styles.table} ${styles.tableHeader}`} style={{ width: tableWidth }}>
            <DataGridColumnGroup columns={columns} preferredColumns={preferredColumns} />
            <thead>
              <tr>
                <DataGridColumnHeaders
                  catalogue={AGREEMENT_COLUMN_CATALOG}
                  columns={columns}
                  layout={columnLayout}
                  headerClassName={styles.resizableColumnHeader}
                  getLabel={(definition) => t(definition.labelKey)}
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
              <tr className={styles.columnFilterRow} data-print-hidden>
                {columns.map((column) => (
                  <th key={column.key}>
                    {renderAgreementFilter(
                      column.key,
                      t(definitions.get(column.key)!.filterLabelKey ?? definitions.get(column.key)!.labelKey),
                      {
                        columnFilters,
                        columnFilterDrafts,
                        dateColumnFilters,
                        divisionFilter,
                        divisionOptions,
                        showAllDivisionOption,
                        statusFilter,
                        warehouseOptions,
                        onColumnFilterChange,
                        onColumnFilterDraftChange,
                        onDateColumnFilterChange,
                        onDivisionFilterChange,
                        onStatusFilterChange,
                        t,
                      },
                    )}
                  </th>
                ))}
              </tr>
            </thead>
          </table>
        </div>
        <div ref={viewportRef} className={styles.tableRowsScroller} tabIndex={0}>
          <table className={`${styles.table} ${styles.tableBody}`} style={{ width: tableWidth }}>
            <DataGridColumnGroup columns={columns} preferredColumns={preferredColumns} />
            <tbody>
              {agreements.length === 0 ? (
                <tr>
                  <td className={styles.emptyState} colSpan={columns.length}>
                    {t("common.noResults")}
                  </td>
                </tr>
              ) : (
                agreements.map((agreement) => (
                  <tr
                    key={agreement.id}
                    className={`${styles.row} ${agreement.isDeleted ? styles.deleted : ""}`}
                    onClick={() => onRowClick(agreement.id)}
                  >
                    {columns.map((column) => renderAgreementCell(column.key, agreement, onOrderNumberClick))}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        <DataGridPagination
          currentPage={currentPage}
          totalPages={totalPages}
          pageSize={pageSize}
          labels={{
            previous: "← Prev",
            next: "Next →",
            page: (current, total) => `Page ${current} of ${total}`,
            pageSize: "Rows per page",
          }}
          onNextPage={onNextPage}
          onPreviousPage={onPreviousPage}
          onPageSizeChange={onPageSizeChange}
        />
      </div>
    </div>
  );
};

interface FilterContext {
  columnFilters: Readonly<Partial<Record<AgreementsSortField, SavedColumnFilterValue>>>;
  columnFilterDrafts: Readonly<Partial<Record<AgreementsSortField, string>>>;
  dateColumnFilters: Readonly<Partial<Record<AgreementsSortField, DateColumnFilterValue>>>;
  divisionFilter: string;
  divisionOptions: ReadonlyArray<{ label: string; value: string }>;
  showAllDivisionOption: boolean;
  statusFilter: string;
  warehouseOptions: ReadonlyArray<{ label: string; value: string }>;
  onColumnFilterChange: (field: AgreementsSortField, value: SavedColumnFilterValue) => void;
  onColumnFilterDraftChange?: (field: AgreementsSortField, value: string) => void;
  onDateColumnFilterChange: (field: AgreementsSortField, value: DateColumnFilterValue | undefined) => void;
  onDivisionFilterChange: (division: string) => void;
  onStatusFilterChange: (status: string) => void;
  t: ReturnType<typeof useTranslation>["t"];
}

function renderAgreementFilter(field: AgreementColumnKey, label: string, context: FilterContext): ReactNode {
  if (field === "division")
    return (
      <TableColumnFilter
        includeAllOption={context.showAllDivisionOption}
        label={label}
        multiValue
        value={parseFilterSelection(context.divisionFilter)}
        onChange={(value) => context.onDivisionFilterChange(serializeFilterSelection(value))}
        options={context.divisionOptions}
      />
    );
  if (dateFields.has(field))
    return (
      <DateColumnFilter
        label={label}
        value={context.dateColumnFilters[field]}
        onChange={(value) => context.onDateColumnFilterChange(field, value)}
      />
    );
  if (field === "fulfilmentStatus")
    return (
      <TableColumnFilter
        label={label}
        multiValue
        value={parseFilterSelection(context.statusFilter)}
        onChange={(value) => context.onStatusFilterChange(serializeFilterSelection(value))}
        options={[
          { label: context.t("fulfilmentStatus.unfulfilled"), value: String(ApiFulfilmentStatus.Unfulfilled) },
          {
            label: context.t("fulfilmentStatus.partiallyFulfilled"),
            value: String(ApiFulfilmentStatus.PartiallyFulfilled),
          },
          { label: context.t("fulfilmentStatus.fullyFulfilled"), value: String(ApiFulfilmentStatus.FullyFulfilled) },
        ]}
      />
    );
  return (
    <TableColumnFilter
      label={label}
      placeholder={field === "lineCount" ? "" : undefined}
      {...(field === "agreementNumber" ||
      field === "customerName" ||
      field === "opportunityName" ||
      field === "lastUpdatedByName"
        ? {
            multiValue: true as const,
            value: toMultiValueTextFilter(context.columnFilters[field]),
            onChange: (value: string[]) => context.onColumnFilterChange(field, value),
            ...(context.onColumnFilterDraftChange
              ? {
                  draftValue: context.columnFilterDrafts[field] ?? "",
                  onDraftChange: (value: string) => context.onColumnFilterDraftChange?.(field, value),
                }
              : {}),
          }
        : field === "warehouse"
          ? {
              multiValue: true as const,
              value: toMultiValueTextFilter(context.columnFilters[field]),
              onChange: (value: string[]) => context.onColumnFilterChange(field, value),
              options: context.warehouseOptions,
            }
          : {
              value: typeof context.columnFilters[field] === "string" ? context.columnFilters[field] : "",
              onChange: (value: string) => context.onColumnFilterChange(field, value),
            })}
    />
  );
}

function renderAgreementCell(
  column: AgreementColumnKey,
  agreement: AgreementListItem,
  onOrderNumberClick: (id: number) => void,
): ReactNode {
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
            {agreement.agreementNumber == null ? (
              "—"
            ) : (
              <a
                className={styles.agreementNumberLink}
                href={`/agreements/${agreement.id}/timeline`}
                onClick={(event) => {
                  event.stopPropagation();
                  if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
                  event.preventDefault();
                  onOrderNumberClick(agreement.id);
                }}
              >
                {agreement.agreementNumber}
              </a>
            )}
            <AgreementNotesIndicator count={agreement.noteCount} />
          </span>
        </td>
      );
    case "customerName":
      return (
        <td key={column}>
          <div className={styles.customerCell}>{agreement.customerName ?? "—"}</div>
        </td>
      );
    case "opportunityName":
      return <td key={column}>{agreement.opportunityName ?? "—"}</td>;
    case "division":
      return <td key={column}>{agreement.division}</td>;
    case "warehouse":
      return <td key={column}>{agreement.warehouse ?? "—"}</td>;
    case "deliveryDate":
      return (
        <td key={column}>
          <DateCell value={agreement.deliveryDate} />
        </td>
      );
    case "validFromDate":
      return (
        <td key={column}>
          <DateCell value={agreement.validFromDate} />
        </td>
      );
    case "validToDate":
      return (
        <td key={column}>
          <DateCell value={agreement.validToDate} />
        </td>
      );
    case "terminationDate":
      return (
        <td key={column}>
          <DateCell value={agreement.terminationDate} />
        </td>
      );
    case "collectionDate":
      return (
        <td key={column}>
          <DateCell value={agreement.collectionDate} />
        </td>
      );
    case "lastUpdatedByName":
      return <td key={column}>{agreement.lastUpdatedByName ?? "—"}</td>;
    case "lineCount":
      return <td key={column}>{agreement.lineCount ?? "—"}</td>;
    case "fromDate":
      return (
        <td key={column}>
          <DateCell value={agreement.fromDate} />
        </td>
      );
    case "toDate":
      return (
        <td key={column}>
          <DateCell value={agreement.toDate} />
        </td>
      );
    case "customerNumber":
      return <td key={column}>{agreement.customerNumber ?? "—"}</td>;
    case "customerAddress":
      return <td key={column}>{agreement.customerAddress ?? "—"}</td>;
    case "lastUpdatedDate":
      return (
        <td key={column}>
          <DateCell value={agreement.lastUpdatedDate} />
        </td>
      );
    case "opportunityStage":
      return <td key={column}>{agreement.opportunityStage ?? "—"}</td>;
    case "probability":
      return <td key={column}>{agreement.probability == null ? "—" : `${agreement.probability}%`}</td>;
  }
}

export function AgreementNotesIndicator({ count = 0 }: { count?: number }) {
  const { t } = useTranslation();
  if (count <= 0) return null;
  const label = t("notes.count", { count });
  return (
    <span className={styles.notesIndicator} title={label} aria-label={label}>
      <BsPencilSquare aria-hidden="true" />
      <span aria-hidden="true">{count}</span>
    </span>
  );
}

function DateCell({ value }: { value: string | null | undefined }) {
  const fullDate = formatDate(value);
  return (
    <span className={styles.dateCell} aria-label={fullDate} title={fullDate}>
      <span className={styles.fullDate} aria-hidden="true">
        {fullDate}
      </span>
      <span className={styles.compactDate} aria-hidden="true">
        {formatCompactDate(value)}
      </span>
    </span>
  );
}

function formatCompactDate(isoDate: string | null | undefined): string {
  if (!isoDate) return "—";
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) return isoDate;
  return date.toLocaleDateString("en-GB", { year: "2-digit", month: "short", day: "2-digit" });
}

export function FulfilmentStatusIcon({ status }: { status: number }) {
  const { t } = useTranslation();
  const statusKind = mapFulfilmentStatus(status);
  let label = "Unknown";
  let statusClass = styles.statusUnknown;
  let icon = <BsDashCircle className={`${styles.statusGlyph} ${styles.statusUnknown}`} aria-hidden="true" />;
  if (statusKind === "unfulfilled") {
    label = t("fulfilmentStatus.unfulfilled");
    statusClass = styles.statusUnfulfilled;
    icon = <BsXCircle className={`${styles.statusGlyph} ${statusClass}`} aria-hidden="true" />;
  } else if (statusKind === "partial") {
    label = t("fulfilmentStatus.partiallyFulfilled");
    statusClass = styles.statusPartial;
    icon = <BsCircleHalf className={`${styles.statusGlyph} ${statusClass}`} aria-hidden="true" />;
  } else if (statusKind === "fulfilled") {
    label = t("fulfilmentStatus.fullyFulfilled");
    statusClass = styles.statusFulfilled;
    icon = <BsCheckCircle className={`${styles.statusGlyph} ${statusClass}`} aria-hidden="true" />;
  }
  return (
    <span className={styles.statusBadge} title={label} aria-label={label}>
      {icon}
    </span>
  );
}

export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return "—";
  try {
    return new Date(isoDate).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  } catch {
    return isoDate;
  }
}
