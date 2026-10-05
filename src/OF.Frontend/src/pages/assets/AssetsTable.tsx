import type { FC, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { BsPencilSquare } from "react-icons/bs";
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
import { parseFilterSelection, serializeFilterSelection } from "../../lib/filterSelection";
import {
  visibleTableColumns,
  type TableColumnDefinition,
  type TableColumnLayoutItem,
} from "../../lib/tableColumnLayout";
import type { Asset } from "../../types";
import type { AssetsSortField, SavedColumnFilterValue, SavedViewSortDirection } from "../../types/savedViews";
import { ASSET_COLUMN_CATALOG, type AssetColumnKey } from "./assetColumnCatalog";
import type { AssetColumnFilters } from "./assetsListModel";
import styles from "./AssetsPage.module.css";

export interface AssetsTableProps {
  assets: readonly Asset[];
  columnFilters: Readonly<AssetColumnFilters>;
  columnFilterDrafts?: Readonly<Partial<Record<AssetsSortField, string>>>;
  columnLayout: readonly TableColumnLayoutItem<AssetColumnKey>[];
  dateColumnFilters?: Readonly<Partial<Record<AssetsSortField, DateColumnFilterValue>>>;
  currentPage: number;
  divisionFilter: string;
  divisionOptions: ReadonlyArray<{ label: string; value: string }>;
  emptyStateMessage?: string;
  getAssetProfilePath: (assetId: string) => string;
  selectedAssetIds: ReadonlySet<string>;
  selectedAssetsOnPageCount: number;
  pageSize: number;
  selectionEnabled?: boolean;
  showAllDivisionOption: boolean;
  sortDirection: SavedViewSortDirection;
  sortField: AssetsSortField;
  statusOptions: readonly string[];
  statusFilter: string;
  totalPages: number;
  warehouseOptions: ReadonlyArray<{ label: string; value: string }>;
  onColumnFilterChange: (field: AssetsSortField, value: SavedColumnFilterValue) => void;
  onColumnFilterDraftChange?: (field: AssetsSortField, value: string) => void;
  onColumnLayoutChange: (layout: TableColumnLayoutItem<AssetColumnKey>[]) => void;
  onDivisionFilterChange: (division: string) => void;
  onStatusFilterChange: (status: string) => void;
  onDateColumnFilterChange?: (field: AssetsSortField, value: DateColumnFilterValue | undefined) => void;
  onNextPage: () => void;
  onPageSizeChange: (pageSize: number) => void;
  onPreviousPage: () => void;
  onSelectAll: (checked: boolean) => void;
  onSelectAsset: (assetId: string, checked: boolean) => void;
  onSort: (field: AssetsSortField) => void;
}

const dateFields: ReadonlySet<AssetsSortField> = new Set([
  "deliveryDate",
  "agreementLineValidFromDate",
  "agreementLineValidToDate",
  "terminationDate",
  "collectionDate",
  "estimatedReadyDate",
]);

export const AssetsTable: FC<AssetsTableProps> = ({
  assets,
  columnFilters,
  columnFilterDrafts = {},
  columnLayout,
  dateColumnFilters = {},
  currentPage,
  divisionFilter,
  divisionOptions,
  emptyStateMessage,
  getAssetProfilePath,
  selectedAssetIds,
  selectedAssetsOnPageCount,
  pageSize,
  selectionEnabled = true,
  showAllDivisionOption,
  sortDirection,
  sortField,
  statusOptions,
  statusFilter,
  totalPages,
  warehouseOptions,
  onColumnFilterChange,
  onColumnFilterDraftChange,
  onColumnLayoutChange,
  onDivisionFilterChange,
  onStatusFilterChange,
  onDateColumnFilterChange = () => {},
  onNextPage,
  onPageSizeChange,
  onPreviousPage,
  onSelectAll,
  onSelectAsset,
  onSort,
}) => {
  const { t } = useTranslation();
  const preferredColumns = visibleTableColumns(columnLayout);
  const definitions = new Map<AssetColumnKey, TableColumnDefinition<AssetColumnKey>>(
    ASSET_COLUMN_CATALOG.map((column) => [column.key, column]),
  );
  const selectionWidth = selectionEnabled ? 32 : 0;
  const { columns, minimumSurfaceWidth, tableWidth, viewportRef } = useResponsiveTableColumns(
    ASSET_COLUMN_CATALOG,
    columnLayout,
    selectionWidth,
  );

  return (
    <div className={styles.tableContainer} data-print-table="assets" role="region" aria-label="Asset rows" tabIndex={0}>
      <div className={styles.tableSurface} style={{ minWidth: minimumSurfaceWidth }}>
        <div className={styles.tableHeaderWrapper}>
          <table className={`${styles.table} ${styles.tableHeader}`} style={{ width: tableWidth }}>
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
                      aria-label="Select all assets on this page"
                      checked={selectedAssetsOnPageCount === assets.length && assets.length > 0}
                      onChange={(event) => onSelectAll(event.target.checked)}
                    />
                  </th>
                )}
                <DataGridColumnHeaders
                  catalogue={ASSET_COLUMN_CATALOG}
                  columns={columns}
                  layout={columnLayout}
                  headerClassName={styles.resizableColumnHeader}
                  getLabel={(definition) => t(definition.labelKey)}
                  renderHeader={(column, _definition, label) => (
                    <TableColumnHeader
                      label={label}
                      sortDirection={sortField === column.key ? sortDirection : undefined}
                      onSort={() => onSort(column.key)}
                    />
                  )}
                  onLayoutChange={onColumnLayoutChange}
                />
              </tr>
              <tr className={styles.columnFilterRow} data-print-hidden>
                {selectionEnabled && <th className={styles.checkboxCol}></th>}
                {columns.map((column) => {
                  const definition = definitions.get(column.key)!;
                  const filterLabel = t(definition.filterLabelKey ?? definition.labelKey);
                  const filterValue = columnFilters[column.key];
                  return (
                    <th key={column.key}>
                      {column.key === "division" ? (
                        <TableColumnFilter
                          includeAllOption={showAllDivisionOption}
                          label={filterLabel}
                          multiValue
                          value={parseFilterSelection(divisionFilter)}
                          onChange={(value) => onDivisionFilterChange(serializeFilterSelection(value))}
                          options={divisionOptions}
                        />
                      ) : column.key === "warehouse" ? (
                        <TableColumnFilter
                          label={filterLabel}
                          multiValue
                          value={toMultiValueTextFilter(filterValue)}
                          onChange={(value) => onColumnFilterChange(column.key, value)}
                          options={warehouseOptions}
                        />
                      ) : dateFields.has(column.key) ? (
                        <DateColumnFilter
                          label={filterLabel}
                          value={dateColumnFilters[column.key]}
                          onChange={(value) => onDateColumnFilterChange(column.key, value)}
                        />
                      ) : (
                        <TableColumnFilter
                          label={filterLabel}
                          {...(column.key === "id" ||
                          column.key === "itemNumber" ||
                          column.key === "description" ||
                          column.key === "customerName" ||
                          column.key === "status"
                            ? {
                                multiValue: true as const,
                                value:
                                  column.key === "status"
                                    ? parseFilterSelection(statusFilter)
                                    : toMultiValueTextFilter(filterValue),
                                onChange: (value: string[]) =>
                                  column.key === "status"
                                    ? onStatusFilterChange(serializeFilterSelection(value))
                                    : onColumnFilterChange(column.key, value),
                                ...(column.key === "status"
                                  ? {
                                      options: statusOptions.map((status) => ({ label: status, value: status })),
                                    }
                                  : onColumnFilterDraftChange
                                    ? {
                                        draftValue: columnFilterDrafts[column.key] ?? "",
                                        onDraftChange: (value: string) => onColumnFilterDraftChange(column.key, value),
                                      }
                                    : {}),
                              }
                            : {
                                value: typeof filterValue === "string" ? filterValue : "",
                                onChange: (value: string) => onColumnFilterChange(column.key, value),
                              })}
                        />
                      )}
                    </th>
                  );
                })}
              </tr>
            </thead>
          </table>
        </div>
        <div ref={viewportRef} className={styles.tableRowsScroller} tabIndex={0}>
          <table className={`${styles.table} ${styles.tableBody}`} style={{ width: tableWidth }}>
            <DataGridColumnGroup
              columns={columns}
              preferredColumns={preferredColumns}
              leadingColumns={
                selectionEnabled ? [{ key: "selection", className: styles.selectColumn, printHidden: true }] : []
              }
            />
            <tbody>
              {assets.length === 0 ? (
                <tr>
                  <td colSpan={columns.length + (selectionEnabled ? 1 : 0)} className={styles.emptyState}>
                    {emptyStateMessage}
                  </td>
                </tr>
              ) : (
                assets.map((asset) => (
                  <AssetRow
                    key={asset.id}
                    asset={asset}
                    assetProfilePath={getAssetProfilePath(asset.id)}
                    columns={columns.map((column) => column.key)}
                    isSelected={selectedAssetIds.has(asset.id)}
                    selectionEnabled={selectionEnabled}
                    onSelect={onSelectAsset}
                  />
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

interface AssetRowProps {
  asset: Asset;
  assetProfilePath: string;
  columns: readonly AssetColumnKey[];
  isSelected: boolean;
  selectionEnabled: boolean;
  onSelect: (assetId: string, checked: boolean) => void;
}

function AssetRow({ asset, assetProfilePath, columns, isSelected, selectionEnabled, onSelect }: AssetRowProps) {
  return (
    <tr className={styles.row}>
      {selectionEnabled && (
        <td className={styles.checkboxCol} data-print-hidden>
          <input type="checkbox" checked={isSelected} onChange={(event) => onSelect(asset.id, event.target.checked)} />
        </td>
      )}
      {columns.map((column) => renderAssetCell(column, asset, assetProfilePath))}
    </tr>
  );
}

function renderAssetCell(column: AssetColumnKey, asset: Asset, assetProfilePath: string): ReactNode {
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
      return <td key={column}>{asset.warehouse ?? "—"}</td>;
    case "division":
      return <td key={column}>{asset.division ?? "—"}</td>;
    case "customerName":
      return <td key={column}>{asset.customerName ?? "—"}</td>;
    case "deliveryDate":
      return (
        <td key={column}>
          <DateCell value={asset.deliveryDate} />
        </td>
      );
    case "agreementLineValidFromDate":
      return (
        <td key={column}>
          <DateCell value={asset.agreementLineValidFromDate} />
        </td>
      );
    case "agreementLineValidToDate":
      return (
        <td key={column}>
          <DateCell value={asset.agreementLineValidToDate} />
        </td>
      );
    case "terminationDate":
      return (
        <td key={column}>
          <DateCell value={asset.terminationDate} />
        </td>
      );
    case "collectionDate":
      return (
        <td key={column}>
          <DateCell value={asset.collectionDate} />
        </td>
      );
    case "daysOffHire":
      return <td key={column}>{asset.daysOffHire ?? "—"}</td>;
    case "warehouseName":
      return <td key={column}>{asset.warehouseName ?? "—"}</td>;
    case "agreementNumber":
      return <td key={column}>{asset.agreementNumber ?? "—"}</td>;
    case "customerNumber":
      return <td key={column}>{asset.customerNumber ?? "—"}</td>;
    case "facility":
      return <td key={column}>{asset.facility ?? "—"}</td>;
    case "warehouseLocation":
      return <td key={column}>{asset.warehouseLocation ?? "—"}</td>;
    case "estimatedReadyDate":
      return (
        <td key={column}>
          <DateCell value={asset.estimatedReadyDate} />
        </td>
      );
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

export function AssetNotesIndicator({ count = 0 }: { count?: number }) {
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

export function formatTelemetryStatus(value: string | null | undefined): string {
  switch (value) {
    case "0":
      return "Not fitted";
    case "1":
      return "Ready for fitting";
    case "2":
      return "Fitted – 2G";
    case "3":
      return "Fitted – 3G/4G";
    case "4":
      return "Fitted – satellite";
    case "5":
      return "Needs inspection";
    default:
      return value || "—";
  }
}

function DateCell({ value }: { value: string | null }) {
  const displayDate = formatDate(value);
  return <span title={displayDate}>{displayDate}</span>;
}

export function StatusIcon({ status }: { status: string | null }) {
  let label = "Unknown";
  switch (status ?? "") {
    case "Available":
      label = "Available";
      break;
    case "OnHire":
      label = "On Hire";
      break;
    case "Service":
      label = "Service";
      break;
    case "Repair":
      label = "Repair";
      break;
    case "Assess":
      label = "Assess";
      break;
    case "Collection":
      label = "Collection";
      break;
    case "In Transit":
      label = "In Transit";
      break;
    case "RemovedStock":
      label = "RemovedStock";
      break;
    case "Scrap":
      label = "Scrap";
      break;
    case "Sold":
      label = "Sold";
      break;
    default:
      label = status || "Unknown";
  }
  return <span title={label}>{label}</span>;
}

function formatDate(isoDate: string | null): string {
  if (!isoDate) return "—";
  try {
    return new Date(isoDate).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  } catch {
    return isoDate;
  }
}
