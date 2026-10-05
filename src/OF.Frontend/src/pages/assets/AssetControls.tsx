import type { FC, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Button, MultiSelectFilter } from "../../components/common";
import { parseFilterSelection, serializeFilterSelection } from "../../lib/filterSelection";
import type { DivisionLookup } from "../../services/lookupsService";
import styles from "./AssetsPage.module.css";

export type AssetViewMode = "table" | "timeline";

export const ASSET_STATUSES = [
  "Available",
  "OnHire",
  "Service",
  "Repair",
  "Collection",
  "In Transit",
  "RemovedStock",
  "Scrap",
  "Sold",
] as const;

export interface AssetControlsProps {
  assetCount: number;
  canManageRemovedStock: boolean;
  divisionLookups: readonly DivisionLookup[];
  hideRemovedStock: boolean;
  hasActiveFilters: boolean;
  isSuperAdmin: boolean;
  savedViewControls: ReactNode;
  tableActions?: ReactNode;
  searchTerm: string;
  selectedDivision: string;
  statusFilter: string;
  userDivisionCodes: readonly string[];
  viewMode: AssetViewMode;
  onHideRemovedStockChange: (hideRemovedStock: boolean) => void;
  onRefresh: () => void;
  onResetFilters: () => void;
  onSearchTermChange: (searchTerm: string) => void;
  onSelectedDivisionChange: (division: string) => void;
  onStatusFilterChange: (status: string) => void;
  onViewModeChange: (viewMode: AssetViewMode) => void;
}

export const AssetControls: FC<AssetControlsProps> = ({
  assetCount,
  canManageRemovedStock,
  divisionLookups,
  hideRemovedStock,
  hasActiveFilters,
  isSuperAdmin,
  savedViewControls,
  tableActions,
  searchTerm,
  selectedDivision,
  statusFilter,
  userDivisionCodes,
  viewMode,
  onHideRemovedStockChange,
  onRefresh,
  onResetFilters,
  onSearchTermChange,
  onSelectedDivisionChange,
  onStatusFilterChange,
  onViewModeChange,
}) => {
  const { t } = useTranslation();
  const visibleDivisions = isSuperAdmin
    ? divisionLookups
    : divisionLookups.filter((division) => userDivisionCodes.includes(division.code));
  const divisionOptions = visibleDivisions.map((division) => ({
    label: `${division.code} — ${division.name}`,
    value: division.code,
  }));
  const statusOptions = ASSET_STATUSES.map((status) => ({ label: status, value: status }));

  return (
    <>
      <header className={styles.header}>
        <div className={styles.headerLeft}>
          <h1>Assets</h1>
          <span className={styles.count}>{assetCount} assets</span>
        </div>
        <div className={styles.viewControls} data-print-hidden>
          {canManageRemovedStock && (
            <label className={styles.stockVisibilityToggle}>
              <input
                type="checkbox"
                checked={hideRemovedStock}
                onChange={(event) => onHideRemovedStockChange(event.target.checked)}
              />
              Hide removed/scrapped/sold
            </label>
          )}
          <div className={styles.viewToggle}>
            <Button
              label="Table"
              variant={viewMode === "table" ? "primary" : "secondary"}
              size="small"
              onClick={() => onViewModeChange("table")}
            />
            <Button
              label="Timeline"
              variant={viewMode === "timeline" ? "primary" : "secondary"}
              size="small"
              onClick={() => onViewModeChange("timeline")}
            />
          </div>
        </div>
      </header>

      <div className={styles.controlsPanel} data-print-hidden>
        {savedViewControls}

        <div className={styles.filters}>
          <input
            type="search"
            className={styles.searchInput}
            placeholder={t("common.search")}
            value={searchTerm}
            onChange={(event) => onSearchTermChange(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter") onRefresh();
            }}
          />
          <MultiSelectFilter
            className={styles.filterSelect}
            label="Status"
            options={statusOptions}
            values={parseFilterSelection(statusFilter)}
            onChange={(values) => onStatusFilterChange(serializeFilterSelection(values))}
          />
          <MultiSelectFilter
            className={styles.filterSelect}
            allowEmpty={isSuperAdmin || userDivisionCodes.length > 1}
            searchable
            label="Division"
            options={divisionOptions}
            values={parseFilterSelection(selectedDivision)}
            onChange={(values) => onSelectedDivisionChange(serializeFilterSelection(values))}
          />
          <div className={styles.resetFiltersAction}>
            {tableActions}
            <Button label={t("common.refresh")} size="small" variant="secondary" onClick={onRefresh} />
            <Button
              disabled={!hasActiveFilters}
              label="Reset filters"
              size="small"
              variant="secondary"
              onClick={onResetFilters}
            />
          </div>
        </div>
      </div>
    </>
  );
};
