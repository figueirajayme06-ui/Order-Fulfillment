import { type FC } from "react";
import styles from "./TableSkeleton.module.css";

export interface TableSkeletonProps {
  columns: number;
  rows?: number;
  ariaLabel?: string;
  className?: string;
}

const headerWidth = (columnIndex: number): string => `${58 + ((columnIndex * 9) % 30)}%`;

const cellWidth = (rowIndex: number, columnIndex: number): string =>
  `${46 + ((rowIndex * 17 + columnIndex * 11) % 42)}%`;

export const TableSkeleton: FC<TableSkeletonProps> = ({
  columns,
  rows = 8,
  ariaLabel = "Loading table data",
  className,
}) => {
  const rowIndices = Array.from({ length: rows }, (_, index) => index);
  const colIndices = Array.from({ length: columns }, (_, index) => index);

  return (
    <div
      className={`${styles.container} ${className ?? ""}`.trim()}
      role="status"
      aria-live="polite"
      aria-label={ariaLabel}
    >
      <span className={styles.srOnly}>{ariaLabel}</span>
      <table className={styles.table} aria-hidden="true">
        <thead>
          <tr>
            {colIndices.map((columnIndex) => (
              <th key={`header-${columnIndex}`} className={styles.headerCell}>
                <span className={styles.headerBar} style={{ width: headerWidth(columnIndex) }}></span>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rowIndices.map((rowIndex) => (
            <tr key={`row-${rowIndex}`}>
              {colIndices.map((columnIndex) => (
                <td key={`cell-${rowIndex}-${columnIndex}`} className={styles.cell}>
                  <span className={styles.cellBar} style={{ width: cellWidth(rowIndex, columnIndex) }}></span>
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};
