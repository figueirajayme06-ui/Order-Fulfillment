import type { RefObject } from "react";
import { useTranslation } from "react-i18next";
import type { DivisionLookup } from "../../services/lookupsService";
import { SearchableSelect } from "../common";
import styles from "./AvailabilityPanel.module.css";

export interface FacilityOption {
  key: string;
  divisionCode: string;
  name: string;
}

export interface WarehouseOption {
  key: string;
  divisionCode: string;
  facility: string;
  code: string;
  name: string;
}

export function getFacilityOptionLabel(option: FacilityOption): string {
  return option.name;
}

export function getWarehouseOptionLabel(option: WarehouseOption): string {
  return [option.code, option.name]
    .map((part) => part.trim())
    .filter(Boolean)
    .join(" \u2014 ");
}

interface AvailabilityFiltersProps {
  viewOptionsRef: RefObject<HTMLDetailsElement | null>;
  activeViewOptionCount: number;
  singleWarehouseView: boolean;
  setSingleWarehouseView: (value: boolean) => void;
  clearExpandedCell: () => void;
  closeViewOptions: () => void;
  includeEmptyWarehouses: boolean;
  setEmptyWarehousesVisible: (value: boolean) => void;
  divisionSelectRef: RefObject<HTMLButtonElement | null>;
  addDivision: (code: string) => void;
  divisionOptionsLoading: boolean;
  divisionOptionsError: boolean;
  additionalDivisionOptions: readonly DivisionLookup[];
  facilitySelectRef: RefObject<HTMLButtonElement | null>;
  showFacility: (key: string) => void;
  facilityOptions: readonly FacilityOption[];
  hiddenFacilityOptions: readonly FacilityOption[];
  warehouseSelectRef: RefObject<HTMLButtonElement | null>;
  showWarehouse: (key: string) => void;
  warehouseOptions: readonly WarehouseOption[];
  hiddenWarehouseOptions: readonly WarehouseOption[];
  selectedWarehouseKey: string;
  setSingleWarehouseKey: (key: string) => void;
  selectedDivisions: readonly string[];
  agreementDivision: string;
  divisionNames: ReadonlyMap<string, string>;
  removeDivision: (code: string) => void;
}

export function AvailabilityFilters({
  viewOptionsRef,
  activeViewOptionCount,
  singleWarehouseView,
  setSingleWarehouseView,
  clearExpandedCell,
  closeViewOptions,
  includeEmptyWarehouses,
  setEmptyWarehousesVisible,
  divisionSelectRef,
  addDivision,
  divisionOptionsLoading,
  divisionOptionsError,
  additionalDivisionOptions,
  facilitySelectRef,
  showFacility,
  facilityOptions,
  hiddenFacilityOptions,
  warehouseSelectRef,
  showWarehouse,
  warehouseOptions,
  hiddenWarehouseOptions,
  selectedWarehouseKey,
  setSingleWarehouseKey,
  selectedDivisions,
  agreementDivision,
  divisionNames,
  removeDivision,
}: AvailabilityFiltersProps) {
  const { t } = useTranslation();
  return (
    <div className={styles.toolbar}>
      <details ref={viewOptionsRef} className={styles.viewOptions}>
        <summary>
          <span>{t("availability.viewOptions")}</span>
          {activeViewOptionCount > 0 && <span className={styles.viewOptionCount}>{activeViewOptionCount}</span>}
        </summary>
        <div className={styles.viewOptionsMenu} role="group" aria-label={t("availability.viewOptions")}>
          <label className={styles.viewControl}>
            <input
              type="checkbox"
              checked={singleWarehouseView}
              onChange={(event) => {
                setSingleWarehouseView(event.target.checked);
                clearExpandedCell();
                closeViewOptions();
              }}
            />
            <span>{t("availability.singleWarehouseExpanded")}</span>
          </label>
          <label className={styles.viewControl}>
            <input
              id="availability-include-empty"
              type="checkbox"
              disabled={singleWarehouseView}
              checked={includeEmptyWarehouses}
              onChange={(event) => {
                setEmptyWarehousesVisible(event.target.checked);
                closeViewOptions();
              }}
            />
            <span>{t("availability.includeEmptyWarehouses")}</span>
          </label>
        </div>
      </details>

      <div className={styles.stockAreaControl}>
        <label htmlFor="availability-add-division">{t("availability.divisions")}</label>
        <SearchableSelect
          id="availability-add-division"
          ref={divisionSelectRef}
          value=""
          ariaLabel={`Choose ${t("availability.divisions").toLocaleLowerCase()}`}
          searchLabel={t("multiSelect.search", { label: t("availability.divisions") })}
          onChange={addDivision}
          disabled={divisionOptionsLoading || divisionOptionsError || additionalDivisionOptions.length === 0}
          placeholder={
            divisionOptionsLoading
              ? t("availability.loadingDivisions")
              : divisionOptionsError
                ? t("availability.addDivision")
                : additionalDivisionOptions.length === 0
                  ? t("availability.noAdditionalDivisions")
                  : t("availability.addDivision")
          }
          options={additionalDivisionOptions.map((option) => ({
            value: option.code,
            label: `${option.code} — ${option.name}`,
          }))}
        />
      </div>

      {!singleWarehouseView && (
        <>
          <div className={styles.facilityControl}>
            <label htmlFor="availability-add-facility">{t("availability.facilities")}</label>
            <SearchableSelect
              id="availability-add-facility"
              ref={facilitySelectRef}
              value=""
              ariaLabel={`Choose ${t("availability.facilities").toLocaleLowerCase()}`}
              searchLabel={t("multiSelect.search", { label: t("availability.facilities") })}
              onChange={showFacility}
              disabled={facilityOptions.length === 0}
              placeholder={
                facilityOptions.length === 0
                  ? t("availability.noFacilities")
                  : hiddenFacilityOptions.length === 0
                    ? t("availability.allFacilitiesShown")
                    : t("availability.addFacility")
              }
              options={hiddenFacilityOptions.map((option) => ({
                value: option.key,
                label: getFacilityOptionLabel(option),
              }))}
            />
          </div>

          <div className={styles.warehouseControl}>
            <label htmlFor="availability-add-warehouse">{t("availability.warehouses")}</label>
            <SearchableSelect
              id="availability-add-warehouse"
              ref={warehouseSelectRef}
              value=""
              ariaLabel={`Choose ${t("availability.warehouses").toLocaleLowerCase()}`}
              searchLabel={t("multiSelect.search", { label: t("availability.warehouses") })}
              onChange={showWarehouse}
              disabled={warehouseOptions.length === 0}
              placeholder={
                warehouseOptions.length === 0
                  ? t("availability.noWarehouses")
                  : hiddenWarehouseOptions.length === 0
                    ? t("availability.allWarehousesShown")
                    : t("availability.addWarehouse")
              }
              options={hiddenWarehouseOptions.map((option) => ({
                value: option.key,
                label: getWarehouseOptionLabel(option),
              }))}
            />
          </div>
        </>
      )}
      {singleWarehouseView && (
        <div className={styles.warehouseControl}>
          <label htmlFor="availability-single-warehouse">{t("availability.singleWarehouse")}</label>
          <SearchableSelect
            id="availability-single-warehouse"
            value={selectedWarehouseKey}
            ariaLabel={t("availability.singleWarehouse")}
            searchLabel={t("multiSelect.search", { label: t("availability.warehouses") })}
            onChange={setSingleWarehouseKey}
            placeholder={t("availability.noWarehouses")}
            disabled={warehouseOptions.length === 0}
            options={warehouseOptions.map((option) => ({
              value: option.key,
              label: getWarehouseOptionLabel(option),
            }))}
          />
        </div>
      )}
      <div className={styles.stockAreaList} role="list" aria-label={t("availability.selectedDivisions")}>
        {selectedDivisions.map((code) => (
          <div
            key={code}
            className={`${styles.stockAreaChip} ${code === agreementDivision ? styles.agreementStockArea : ""}`}
            role="listitem"
          >
            <span>
              {code}
              {divisionNames.get(code) ? ` — ${divisionNames.get(code)}` : ""}
            </span>
            {code === agreementDivision ? (
              <span className={styles.agreementBadge}>{t("availability.agreementDivision")}</span>
            ) : (
              <button
                type="button"
                className={styles.removeStockArea}
                aria-label={t("availability.removeDivision", { code })}
                onClick={() => removeDivision(code)}
              >
                ×
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
