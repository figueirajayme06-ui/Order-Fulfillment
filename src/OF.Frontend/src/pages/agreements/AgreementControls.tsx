import type { FC, ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Button, MultiSelectFilter } from "../../components/common";
import { parseFilterSelection, serializeFilterSelection } from "../../lib/filterSelection";
import { GlobalActions } from "../../components/layout/GlobalActions";
import type { DivisionLookup } from "../../services/lookupsService";
import { ApiFulfilmentStatus } from "../../types";
import styles from "./AgreementsPage.module.css";

export type AgreementViewMode = "table" | "timeline";

export interface AgreementControlsProps {
  agreementCount: number;
  divisionLookups: readonly DivisionLookup[];
  hasActiveFilters: boolean;
  isReadOnly: boolean;
  isSuperAdmin: boolean;
  orderTypeFilter: string;
  savedViewControls: ReactNode;
  tableActions?: ReactNode;
  searchTerm: string;
  selectedDivision: string;
  showHistorical: boolean;
  statusFilter: string;
  userDivisionCodes: readonly string[];
  viewMode: AgreementViewMode;
  onOrderTypeFilterChange: (filter: string) => void;
  onRefresh: () => void;
  onResetFilters: () => void;
  onSearchTermChange: (searchTerm: string) => void;
  onSelectedDivisionChange: (division: string) => void;
  onShowHistoricalChange: (showHistorical: boolean) => void;
  onStatusFilterChange: (status: string) => void;
  onViewModeChange: (viewMode: AgreementViewMode) => void;
}

export const AgreementControls: FC<AgreementControlsProps> = ({
  agreementCount,
  divisionLookups,
  hasActiveFilters,
  isReadOnly,
  isSuperAdmin,
  orderTypeFilter,
  savedViewControls,
  tableActions,
  searchTerm,
  selectedDivision,
  showHistorical,
  statusFilter,
  userDivisionCodes,
  viewMode,
  onOrderTypeFilterChange,
  onRefresh,
  onResetFilters,
  onSearchTermChange,
  onSelectedDivisionChange,
  onShowHistoricalChange,
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
  const orderTypeOptions = [
    { label: t("agreements.orderTypeQuote"), value: "quote" },
    { label: t("agreements.orderTypeTemporaryAgreement"), value: "temporaryAgreement" },
    { label: t("agreements.orderTypeAgreement"), value: "agreement" },
  ];
  const statusOptions = [
    { label: t("fulfilmentStatus.unfulfilled"), value: String(ApiFulfilmentStatus.Unfulfilled) },
    { label: t("fulfilmentStatus.partiallyFulfilled"), value: String(ApiFulfilmentStatus.PartiallyFulfilled) },
    { label: t("fulfilmentStatus.fullyFulfilled"), value: String(ApiFulfilmentStatus.FullyFulfilled) },
  ];

  return (
    <>
      <header className={styles.header}>
        <div className={styles.headerLeft}>
          <h1>{t("agreements.title")}</h1>
          <span className={styles.count}>{agreementCount} agreements</span>
        </div>
        <div className={styles.headerActions} data-print-hidden>
          <GlobalActions showPull={!isReadOnly} showUtilities={false} showAlerts={false} />
          <div className={styles.viewControls}>
            <label className={styles.historicalToggle}>
              <input
                type="checkbox"
                checked={showHistorical}
                onChange={(event) => onShowHistoricalChange(event.target.checked)}
              />
              {t("agreements.showHistorical")}
            </label>
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
            allowEmpty={isSuperAdmin || userDivisionCodes.length > 1}
            searchable
            label="Division"
            options={divisionOptions}
            values={parseFilterSelection(selectedDivision)}
            onChange={(values) => onSelectedDivisionChange(serializeFilterSelection(values))}
          />

          <MultiSelectFilter
            className={styles.filterSelect}
            allLabel={t("agreements.orderTypeAll")}
            label={t("agreements.orderType")}
            options={orderTypeOptions}
            values={parseFilterSelection(orderTypeFilter)}
            onChange={(values) => onOrderTypeFilterChange(serializeFilterSelection(values))}
          />

          <MultiSelectFilter
            className={styles.filterSelect}
            label={t("agreements.fulfilmentStatus")}
            options={statusOptions}
            values={parseFilterSelection(statusFilter)}
            onChange={(values) => onStatusFilterChange(serializeFilterSelection(values))}
          />

          <div className={styles.resetFiltersButton}>
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
