import { type CSSProperties, type ReactNode } from "react";
import {
  getTableColumnPrintWidth,
  setTableColumnWidth,
  type TableColumnDefinition,
  type TableColumnLayoutItem,
} from "../../../lib/tableColumnLayout";
import { ColumnResizeHandle } from "../TableColumns/TableColumns";
import styles from "./DataGrid.module.css";

export interface DataGridLeadingColumn {
  key: string;
  className?: string;
  printHidden?: boolean;
  width?: number;
}

interface DataGridColumnGroupProps<Key extends string> {
  columns: readonly TableColumnLayoutItem<Key>[];
  preferredColumns: readonly TableColumnLayoutItem<Key>[];
  leadingColumns?: readonly DataGridLeadingColumn[];
}

export function DataGridColumnGroup<Key extends string>({
  columns,
  preferredColumns,
  leadingColumns = [],
}: DataGridColumnGroupProps<Key>) {
  const preferredWidthByKey = new Map(preferredColumns.map((column) => [column.key, column.width]));
  const preferredTotalWidth = preferredColumns.reduce((total, column) => total + column.width, 0);

  return (
    <colgroup>
      {leadingColumns.map((column) => (
        <col
          key={column.key}
          className={column.className}
          data-print-hidden={column.printHidden ? "" : undefined}
          style={column.width == null ? undefined : { width: column.width }}
        />
      ))}
      {columns.map((column) => (
        <col
          key={column.key}
          data-column-key={column.key}
          style={
            {
              width: column.width,
              "--print-column-width": getTableColumnPrintWidth(
                preferredWidthByKey.get(column.key) ?? column.width,
                preferredTotalWidth,
              ),
            } as CSSProperties
          }
        />
      ))}
    </colgroup>
  );
}

interface DataGridColumnHeadersProps<Key extends string> {
  catalogue: readonly TableColumnDefinition<Key>[];
  columns: readonly TableColumnLayoutItem<Key>[];
  layout: readonly TableColumnLayoutItem<Key>[];
  onLayoutChange: (layout: TableColumnLayoutItem<Key>[]) => void;
  getLabel: (definition: TableColumnDefinition<Key>) => string;
  renderHeader: (
    column: TableColumnLayoutItem<Key>,
    definition: TableColumnDefinition<Key>,
    label: string,
  ) => ReactNode;
  getHeaderAriaLabel?: (definition: TableColumnDefinition<Key>, label: string) => string | undefined;
  headerClassName?: string;
}

export function DataGridColumnHeaders<Key extends string>({
  catalogue,
  columns,
  layout,
  onLayoutChange,
  getLabel,
  renderHeader,
  getHeaderAriaLabel,
  headerClassName,
}: DataGridColumnHeadersProps<Key>) {
  const definitions = new Map(catalogue.map((column) => [column.key, column]));

  return columns.map((column) => {
    const definition = definitions.get(column.key);
    if (!definition) return null;
    const label = getLabel(definition);

    return (
      <th key={column.key} className={headerClassName} aria-label={getHeaderAriaLabel?.(definition, label)}>
        {renderHeader(column, definition, label)}
        <ColumnResizeHandle
          label={label}
          width={column.width}
          minWidth={definition.minWidth}
          maxWidth={definition.maxWidth}
          onResize={(width) => {
            const preferredWidth = layout.find((item) => item.key === column.key)?.width ?? width;
            onLayoutChange(setTableColumnWidth(catalogue, layout, column.key, preferredWidth + width - column.width));
          }}
        />
      </th>
    );
  });
}

export interface DataGridPaginationLabels {
  previous: string;
  next: string;
  page: (currentPage: number, totalPages: number) => string;
  pageSize?: string;
}

interface DataGridPaginationProps {
  currentPage: number;
  labels: DataGridPaginationLabels;
  totalPages: number;
  onNextPage: () => void;
  onPreviousPage: () => void;
  pageSize?: number;
  pageSizeOptions?: readonly number[];
  onPageSizeChange?: (pageSize: number) => void;
}

export function DataGridPagination({
  currentPage,
  labels,
  totalPages,
  onNextPage,
  onPreviousPage,
  pageSize,
  pageSizeOptions = [50, 250, 500, 1000],
  onPageSizeChange,
}: DataGridPaginationProps) {
  const showPageSize = pageSize != null && onPageSizeChange != null && labels.pageSize != null;

  return (
    <nav className={styles.pagination} data-print-hidden aria-label={labels.page(currentPage, totalPages)}>
      <button className={styles.pageButton} disabled={currentPage === 1} onClick={onPreviousPage}>
        {labels.previous}
      </button>
      <span className={styles.pageInformation}>{labels.page(currentPage, totalPages)}</span>
      <button className={styles.pageButton} disabled={currentPage === totalPages} onClick={onNextPage}>
        {labels.next}
      </button>
      {showPageSize && (
        <label className={styles.pageSizeControl}>
          <span>{labels.pageSize}</span>
          <select
            className={styles.pageSizeSelect}
            aria-label={labels.pageSize}
            value={pageSize}
            onChange={(event) => onPageSizeChange(Number(event.target.value))}
          >
            {pageSizeOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </label>
      )}
    </nav>
  );
}
