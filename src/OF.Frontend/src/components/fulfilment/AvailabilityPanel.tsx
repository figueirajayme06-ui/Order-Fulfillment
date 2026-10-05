import { Fragment, useCallback, useEffect, useMemo, useRef, useState, type FC } from "react";
import { useTranslation } from "react-i18next";
import { LuArrowLeftRight } from "react-icons/lu";
import { Spinner } from "../common";
import { AvailabilityDetails } from "./AvailabilityDetails";
import { AvailabilityFilters, type FacilityOption, type WarehouseOption } from "./AvailabilityFilters";
import {
  buildAvailabilityGrid,
  filterAvailabilityGridByFacilities,
  filterAvailabilityGridByStock,
  filterAvailabilityGridByWarehouses,
  getAvailabilityFacilityKey,
  getAvailabilityWarehouseKey,
  getDefaultHiddenWarehouseKeys,
} from "./availabilityGrid";
import styles from "./AvailabilityPanel.module.css";
import { buildAvailabilityScheduleRange, formatAvailabilityDate } from "./availabilitySchedule";
import type { AvailabilityPanelProps, ExpandedCell } from "./availabilityTypes";
import { useAvailabilityData } from "./useAvailabilityData";

const ITEM_NUMBER_COLUMN_WIDTH = 120;
const DESCRIPTION_COLUMN_WIDTH = 290;

function normalizeDivisionCode(code?: string): string {
  return code?.trim().toUpperCase() ?? "";
}

function getAvailabilityCellClass(available: number, count: number, isClickable: boolean): string {
  const base = (() => {
    if (count === 0) return styles.cellRed;
    const percentage = (available / count) * 100;
    if (percentage > 50) return styles.cellGreen;
    if (percentage > 20) return styles.cellOrange;
    return styles.cellRed;
  })();
  return isClickable ? `${base} ${styles.cellClickable}` : base;
}

export const AvailabilityPanel: FC<AvailabilityPanelProps> = ({
  genericCode,
  itemNumber,
  warehouse,
  division,
  startDate,
  endDate,
  attributes,
  lineId,
  requiredQuantity = 0,
  readOnly = false,
  fulfilledQuantity = 0,
  onReserveAsset,
  onReserveStock,
}) => {
  const { t, i18n } = useTranslation();
  const warehouseScheduleRange = buildAvailabilityScheduleRange(startDate, endDate);
  const agreementDivision = normalizeDivisionCode(division);
  const agreementWarehouse = getAvailabilityWarehouseKey(warehouse);
  const [singleWarehouseView, setSingleWarehouseView] = useState(false);
  const [singleWarehouseKey, setSingleWarehouseKey] = useState(agreementWarehouse);
  const [includeEmptyWarehouses, setIncludeEmptyWarehouses] = useState(false);
  const [selectedDivisions, setSelectedDivisions] = useState<string[]>(() =>
    agreementDivision ? [agreementDivision] : [],
  );
  const [facilityVisibilityOverrides, setFacilityVisibilityOverrides] = useState<Record<string, boolean>>({});
  const [hiddenWarehouseKeys, setHiddenWarehouseKeys] = useState<string[]>([]);
  const [restoredWarehouseKeys, setRestoredWarehouseKeys] = useState<string[]>([]);
  const [expandedCell, setExpandedCell] = useState<ExpandedCell | null>(null);
  const divisionSelectRef = useRef<HTMLButtonElement>(null);
  const focusDivisionSelectAfterRemoval = useRef(false);
  const facilitySelectRef = useRef<HTMLButtonElement>(null);
  const focusFacilitySelectAfterRemoval = useRef(false);
  const warehouseSelectRef = useRef<HTMLButtonElement>(null);
  const focusWarehouseSelectAfterRemoval = useRef(false);
  const viewOptionsRef = useRef<HTMLDetailsElement>(null);

  const selectedDivisionQuery = selectedDivisions.join(",");
  const {
    data,
    loading,
    error,
    divisionOptions,
    divisionOptionsLoading,
    divisionOptionsError,
    warehouseCatalog,
    warehouseCatalogError,
    loadData,
  } = useAvailabilityData({ genericCode, itemNumber, attributes, startDate, endDate, lineId, selectedDivisionQuery });

  const clearExpandedCell = useCallback(() => setExpandedCell(null), []);

  useEffect(() => {
    setSingleWarehouseKey(agreementWarehouse);
    setSelectedDivisions(agreementDivision ? [agreementDivision] : []);
    setFacilityVisibilityOverrides({});
    setHiddenWarehouseKeys([]);
    setRestoredWarehouseKeys([]);
    setIncludeEmptyWarehouses(false);
  }, [agreementDivision, agreementWarehouse]);

  // Switching lines keeps the grid's added divisions/facilities/warehouses; only the expanded cell closes.
  useEffect(() => {
    clearExpandedCell();
  }, [clearExpandedCell, lineId]);

  useEffect(() => {
    clearExpandedCell();
  }, [genericCode, selectedDivisionQuery, clearExpandedCell]);

  useEffect(() => {
    if (!focusDivisionSelectAfterRemoval.current) return;
    focusDivisionSelectAfterRemoval.current = false;
    divisionSelectRef.current?.focus();
  }, [selectedDivisions]);

  useEffect(() => {
    if (!focusFacilitySelectAfterRemoval.current) return;
    focusFacilitySelectAfterRemoval.current = false;
    facilitySelectRef.current?.focus();
  }, [facilityVisibilityOverrides]);

  useEffect(() => {
    if (!focusWarehouseSelectAfterRemoval.current) return;
    focusWarehouseSelectAfterRemoval.current = false;
    warehouseSelectRef.current?.focus();
  }, [hiddenWarehouseKeys]);

  const handleCellClick = (itemNumber: string, warehouseCode: string, divisionCode: string) => {
    setExpandedCell((current) =>
      current?.itemNumber === itemNumber &&
      current.warehouseCode === warehouseCode &&
      current.divisionCode === divisionCode
        ? null
        : { itemNumber, warehouseCode, divisionCode },
    );
  };

  const onStockReserved = useCallback(async () => {
    clearExpandedCell();
    await loadData();
  }, [clearExpandedCell, loadData]);

  const locationCatalogGrid = useMemo(
    () => buildAvailabilityGrid(data, genericCode, agreementDivision, warehouse, warehouseCatalog),
    [data, genericCode, agreementDivision, warehouse, warehouseCatalog],
  );

  const facilityNames = useMemo(() => {
    const labels = new Map<string, string>();
    warehouseCatalog.forEach((location) => {
      const code = location.facility.trim();
      const name = location.facilityName?.trim();
      if (code && name && code.toLocaleUpperCase() !== name.toLocaleUpperCase()) {
        labels.set(getAvailabilityFacilityKey(location.divisionCode, code), name);
      }
    });
    return labels;
  }, [warehouseCatalog]);

  const facilityOptions = useMemo<FacilityOption[]>(
    () =>
      locationCatalogGrid.divisions.flatMap((divisionGroup) =>
        divisionGroup.facilities.map((facility) => ({
          key: getAvailabilityFacilityKey(divisionGroup.code, facility.name),
          divisionCode: divisionGroup.code,
          name:
            [facility.name, facilityNames.get(getAvailabilityFacilityKey(divisionGroup.code, facility.name))]
              .filter(Boolean)
              .join(" — ") || t("availability.unknownFacility"),
        })),
      ),
    [locationCatalogGrid.divisions, facilityNames, t],
  );

  const warehouseOptions = useMemo<WarehouseOption[]>(
    () =>
      locationCatalogGrid.warehouses.map((warehouseColumn) => ({
        key: getAvailabilityWarehouseKey(warehouseColumn.code),
        divisionCode: warehouseColumn.divisionCode,
        facility: warehouseColumn.facility,
        code: warehouseColumn.code,
        name: warehouseColumn.name,
      })),
    [locationCatalogGrid.warehouses],
  );

  const agreementFacilityKey = useMemo(() => {
    if (!agreementWarehouse) return "";
    const agreementColumn = locationCatalogGrid.warehouses.find(
      (column) => getAvailabilityWarehouseKey(column.code) === agreementWarehouse,
    );
    return agreementColumn ? getAvailabilityFacilityKey(agreementColumn.divisionCode, agreementColumn.facility) : "";
  }, [agreementWarehouse, locationCatalogGrid.warehouses]);

  const agreementFacilityResolved = useMemo(() => {
    if (!agreementWarehouse) return false;
    return (
      data.some((item) => getAvailabilityWarehouseKey(item.warehouseCode) === agreementWarehouse) ||
      warehouseCatalog.some((item) => getAvailabilityWarehouseKey(item.warehouseCode) === agreementWarehouse)
    );
  }, [agreementWarehouse, data, warehouseCatalog]);

  useEffect(() => {
    const availableFacilityKeys = new Set(facilityOptions.map((option) => option.key));
    setFacilityVisibilityOverrides((current) => {
      const next = Object.fromEntries(
        Object.entries(current).filter(([key]) => availableFacilityKeys.has(key) && key !== agreementFacilityKey),
      );
      const currentKeys = Object.keys(current);
      const nextKeys = Object.keys(next);
      return currentKeys.length === nextKeys.length && currentKeys.every((key) => current[key] === next[key])
        ? current
        : next;
    });
  }, [agreementFacilityKey, facilityOptions]);

  useEffect(() => {
    const availableWarehouseKeys = new Set(warehouseOptions.map((option) => option.key));
    setHiddenWarehouseKeys((current) => {
      const next = current.filter((key) => availableWarehouseKeys.has(key) && key !== agreementWarehouse);
      return next.length === current.length && next.every((key, index) => key === current[index]) ? current : next;
    });
    setRestoredWarehouseKeys((current) => {
      const next = current.filter((key) => availableWarehouseKeys.has(key) && key !== agreementWarehouse);
      return next.length === current.length && next.every((key, index) => key === current[index]) ? current : next;
    });
  }, [agreementWarehouse, warehouseOptions]);

  const hiddenFacilityKeySet = useMemo(
    () =>
      new Set(
        facilityOptions
          .filter((option) => {
            if (option.key === agreementFacilityKey) return false;
            const override = facilityVisibilityOverrides[option.key];
            // Sibling facilities in the agreement's own division stay opt-in; facilities in
            // divisions the user explicitly added are shown by default (still stock-filtered).
            const isAgreementDivisionSibling =
              agreementFacilityKey && agreementFacilityResolved && option.divisionCode === agreementDivision;
            return isAgreementDivisionSibling ? override !== true : override === false;
          })
          .map((option) => option.key),
      ),
    [agreementDivision, agreementFacilityKey, agreementFacilityResolved, facilityOptions, facilityVisibilityOverrides],
  );
  const defaultHiddenWarehouseKeySet = useMemo(
    () => getDefaultHiddenWarehouseKeys(locationCatalogGrid, agreementWarehouse),
    [agreementWarehouse, locationCatalogGrid],
  );
  const hiddenWarehouseKeySet = useMemo(() => {
    const restored = new Set(restoredWarehouseKeys);
    const hidden = new Set(hiddenWarehouseKeys);
    if (!includeEmptyWarehouses) {
      defaultHiddenWarehouseKeySet.forEach((key) => {
        if (!restored.has(key)) hidden.add(key);
      });
    }
    if (agreementWarehouse) hidden.delete(agreementWarehouse);
    return hidden;
  }, [
    agreementWarehouse,
    defaultHiddenWarehouseKeySet,
    hiddenWarehouseKeys,
    includeEmptyWarehouses,
    restoredWarehouseKeys,
  ]);
  const facilityFilteredGrid = useMemo(
    () => filterAvailabilityGridByFacilities(locationCatalogGrid, hiddenFacilityKeySet),
    [locationCatalogGrid, hiddenFacilityKeySet],
  );
  const warehouseFilteredGrid = useMemo(
    () => filterAvailabilityGridByWarehouses(facilityFilteredGrid, hiddenWarehouseKeySet),
    [facilityFilteredGrid, hiddenWarehouseKeySet],
  );
  const selectedWarehouseKey = warehouseOptions.some((option) => option.key === singleWarehouseKey)
    ? singleWarehouseKey
    : (warehouseOptions.find((option) => option.key === agreementWarehouse)?.key ?? warehouseOptions[0]?.key ?? "");
  const grid = useMemo(
    () =>
      filterAvailabilityGridByStock(
        singleWarehouseView
          ? filterAvailabilityGridByWarehouses(
              locationCatalogGrid,
              new Set(
                locationCatalogGrid.warehouses
                  .map((column) => getAvailabilityWarehouseKey(column.code))
                  .filter((key) => key !== selectedWarehouseKey),
              ),
            )
          : warehouseFilteredGrid,
      ),
    [singleWarehouseView, locationCatalogGrid, selectedWarehouseKey, warehouseFilteredGrid],
  );

  useEffect(() => {
    if (!expandedCell) return;

    const warehouseCode = getAvailabilityWarehouseKey(expandedCell.warehouseCode);
    const divisionCode = normalizeDivisionCode(expandedCell.divisionCode);
    const hasVisibleWarehouse = grid.warehouses.some(
      (column) =>
        getAvailabilityWarehouseKey(column.code) === warehouseCode &&
        normalizeDivisionCode(column.divisionCode) === divisionCode,
    );
    const item = grid.items.find((candidate) => candidate.itemNumber === expandedCell.itemNumber);
    if (!hasVisibleWarehouse || !item?.warehouseData.has(warehouseCode)) {
      clearExpandedCell();
    }
  }, [clearExpandedCell, expandedCell, grid]);

  const hiddenFacilityOptions = useMemo(
    () => facilityOptions.filter((option) => hiddenFacilityKeySet.has(option.key)),
    [facilityOptions, hiddenFacilityKeySet],
  );

  const hiddenWarehouseOptions = useMemo(
    () => warehouseOptions.filter((option) => hiddenWarehouseKeySet.has(option.key)),
    [hiddenWarehouseKeySet, warehouseOptions],
  );

  const hasPositiveAssetWarehouse = useMemo(
    () =>
      locationCatalogGrid.warehouses.some((column) =>
        locationCatalogGrid.items.some((item) => {
          const cell = item.warehouseData.get(getAvailabilityWarehouseKey(column.code));
          return cell?.reservationMode === "asset" && cell.available > 0;
        }),
      ),
    [locationCatalogGrid],
  );

  const divisionNames = useMemo(() => {
    const names = new Map(divisionOptions.map((option) => [option.code, option.name]));
    warehouseCatalog.forEach((location) => {
      const code = normalizeDivisionCode(location.divisionCode);
      if (code && location.divisionName && !names.has(code)) {
        names.set(code, location.divisionName);
      }
    });
    data.forEach((item) => {
      const code = normalizeDivisionCode(item.divisionCode);
      if (code && item.divisionName && !names.has(code)) {
        names.set(code, item.divisionName);
      }
    });
    return names;
  }, [data, divisionOptions, warehouseCatalog]);

  const additionalDivisionOptions = useMemo(
    () =>
      divisionOptions
        .filter((option) => !selectedDivisions.includes(option.code))
        .sort((a, b) => a.code.localeCompare(b.code, undefined, { numeric: true, sensitivity: "base" })),
    [divisionOptions, selectedDivisions],
  );

  const warehouseNames = useMemo(
    () => new Map(grid.warehouses.map((column) => [column.code, column.name])),
    [grid.warehouses],
  );

  const hasWarehouseColumns = grid.warehouses.length > 0;
  const colCount = 2 + grid.warehouses.length;

  const addDivision = (code: string) => {
    const normalizedCode = normalizeDivisionCode(code);
    if (!normalizedCode || selectedDivisions.includes(normalizedCode)) return;
    setSelectedDivisions((current) => [...current, normalizedCode]);
  };

  const removeDivision = (code: string) => {
    const normalizedCode = normalizeDivisionCode(code);
    if (normalizedCode === agreementDivision) return;
    focusDivisionSelectAfterRemoval.current = true;
    setSelectedDivisions((current) => current.filter((candidate) => candidate !== normalizedCode));
  };

  const hideFacility = (key: string) => {
    if (!key || key === agreementFacilityKey) return;
    focusFacilitySelectAfterRemoval.current = true;
    setFacilityVisibilityOverrides((current) => ({ ...current, [key]: false }));
    clearExpandedCell();
  };

  const showFacility = (key: string) => {
    if (!key) return;
    setFacilityVisibilityOverrides((current) => ({ ...current, [key]: true }));
    clearExpandedCell();
  };

  const hideWarehouse = (key: string) => {
    const normalizedKey = getAvailabilityWarehouseKey(key);
    if (!normalizedKey || normalizedKey === agreementWarehouse) return;
    focusWarehouseSelectAfterRemoval.current = true;
    setHiddenWarehouseKeys((current) => (current.includes(normalizedKey) ? current : [...current, normalizedKey]));
    setRestoredWarehouseKeys((current) => current.filter((candidate) => candidate !== normalizedKey));
    clearExpandedCell();
  };

  const showWarehouse = (key: string) => {
    const normalizedKey = getAvailabilityWarehouseKey(key);
    if (!normalizedKey) return;
    setHiddenWarehouseKeys((current) => current.filter((candidate) => candidate !== normalizedKey));
    setRestoredWarehouseKeys((current) => (current.includes(normalizedKey) ? current : [...current, normalizedKey]));
    const option = warehouseOptions.find((candidate) => candidate.key === normalizedKey);
    if (option) {
      const facilityKey = getAvailabilityFacilityKey(option.divisionCode, option.facility);
      setFacilityVisibilityOverrides((current) => ({ ...current, [facilityKey]: true }));
    }
    clearExpandedCell();
  };

  const setEmptyWarehousesVisible = (visible: boolean) => {
    clearExpandedCell();
    setIncludeEmptyWarehouses(visible);
  };

  const closeViewOptions = () => {
    viewOptionsRef.current?.removeAttribute("open");
  };

  const activeViewOptionCount = singleWarehouseView ? 1 : Number(includeEmptyWarehouses);

  return (
    <div className={styles.panel}>
      <AvailabilityFilters
        viewOptionsRef={viewOptionsRef}
        activeViewOptionCount={activeViewOptionCount}
        singleWarehouseView={singleWarehouseView}
        setSingleWarehouseView={setSingleWarehouseView}
        clearExpandedCell={clearExpandedCell}
        closeViewOptions={closeViewOptions}
        includeEmptyWarehouses={includeEmptyWarehouses}
        setEmptyWarehousesVisible={setEmptyWarehousesVisible}
        divisionSelectRef={divisionSelectRef}
        addDivision={addDivision}
        divisionOptionsLoading={divisionOptionsLoading}
        divisionOptionsError={divisionOptionsError}
        additionalDivisionOptions={additionalDivisionOptions}
        facilitySelectRef={facilitySelectRef}
        showFacility={showFacility}
        facilityOptions={facilityOptions}
        hiddenFacilityOptions={hiddenFacilityOptions}
        warehouseSelectRef={warehouseSelectRef}
        showWarehouse={showWarehouse}
        warehouseOptions={warehouseOptions}
        hiddenWarehouseOptions={hiddenWarehouseOptions}
        selectedWarehouseKey={selectedWarehouseKey}
        setSingleWarehouseKey={setSingleWarehouseKey}
        selectedDivisions={selectedDivisions}
        agreementDivision={agreementDivision}
        divisionNames={divisionNames}
        removeDivision={removeDivision}
      />

      {divisionOptionsError && (
        <p className={styles.lookupError} role="status">
          {t("availability.divisionsError")}
        </p>
      )}

      {warehouseCatalogError && (
        <p className={styles.lookupError} role="status">
          {t("availability.warehousesError")}
        </p>
      )}

      {!singleWarehouseView &&
        !loading &&
        data.length > 0 &&
        !includeEmptyWarehouses &&
        !hasPositiveAssetWarehouse &&
        defaultHiddenWarehouseKeySet.size > 0 && (
          <p className={styles.stockFocusHint}>{t("availability.noPositiveStock")}</p>
        )}

      {loading ? (
        <div className={styles.loadingState}>
          <Spinner />
        </div>
      ) : error ? (
        <p className={styles.emptyText}>{error}</p>
      ) : data.length === 0 ? (
        <p className={styles.emptyText}>{t("availability.noData")}</p>
      ) : singleWarehouseView ? (
        <div className={styles.tableWrap} data-print-table>
          <table className={styles.warehouseStockTable} aria-label={t("availability.singleWarehouseExpanded")}>
            <caption>
              {grid.warehouses[0]?.code}
              {grid.warehouses[0]?.name ? ` — ${grid.warehouses[0].name}` : ""}
            </caption>
            <colgroup>
              <col className={styles.stockAssetColumn} />
              <col className={styles.stockItemColumn} />
              <col className={styles.stockDescriptionColumn} />
              <col className={styles.stockLocationColumn} />
              <col />
              <col className={styles.stockActionColumn} />
            </colgroup>
            <thead>
              <tr>
                <th scope="col">{t("availability.assetId")}</th>
                <th scope="col">{t("fulfilment.itemNumber")}</th>
                <th scope="col">{t("availability.description")}</th>
                <th scope="col">{t("availability.assetLocation")}</th>
                <th scope="col">
                  <div className={styles.stockPeriod}>
                    {warehouseScheduleRange ? (
                      <>
                        <span>{formatAvailabilityDate(warehouseScheduleRange.start, i18n.language)}</span>
                        <span>{formatAvailabilityDate(warehouseScheduleRange.end, i18n.language)}</span>
                      </>
                    ) : (
                      t("availability.commitments")
                    )}
                  </div>
                </th>
                <th scope="col">
                  <span className={styles.assetActionHeader}>{t("common.actions")}</span>
                </th>
              </tr>
            </thead>
            {grid.items.length === 0 && (
              <tbody>
                <tr>
                  <td colSpan={6}>{t("availability.noStockInDisplayedWarehouses")}</td>
                </tr>
              </tbody>
            )}
            {grid.warehouses[0] &&
              grid.items.map((item) => {
                const column = grid.warehouses[0];
                const cell = item.warehouseData.get(column.code);
                return cell ? (
                  <AvailabilityDetails
                    key={`${lineId}-${item.itemNumber}-${column.code}-${column.divisionCode}`}
                    expandedCell={{
                      itemNumber: item.itemNumber,
                      warehouseCode: column.code,
                      divisionCode: column.divisionCode,
                    }}
                    cell={cell}
                    displayMode="singleWarehouse"
                    description={item.descriptionIntl}
                    relatedSubstitute={item.substitutionReason === "RELATED"}
                    warehouseName={column.name}
                    startDate={startDate}
                    endDate={endDate}
                    lineId={lineId}
                    requiredQuantity={requiredQuantity}
                    fulfilledQuantity={fulfilledQuantity}
                    readOnly={readOnly}
                    onReserveAsset={onReserveAsset}
                    onReserveStock={onReserveStock}
                    onStockReserved={onStockReserved}
                  />
                ) : null;
              })}
          </table>
        </div>
      ) : (
        <div className={styles.tableWrap} data-print-table>
          <table className={styles.table} style={{ minWidth: `${380 + grid.warehouses.length * 75}px` }}>
            <thead>
              <tr>
                <th
                  className={styles.stickyColHeader}
                  rowSpan={hasWarehouseColumns ? 3 : 1}
                  scope="col"
                  style={{ width: `${ITEM_NUMBER_COLUMN_WIDTH}px` }}
                >
                  {t("fulfilment.itemNumber")}
                </th>
                <th
                  className={styles.stickyColHeader}
                  rowSpan={hasWarehouseColumns ? 3 : 1}
                  scope="col"
                  style={{ left: `${ITEM_NUMBER_COLUMN_WIDTH}px`, width: `${DESCRIPTION_COLUMN_WIDTH}px` }}
                >
                  {t("availability.description")}
                </th>
                {grid.divisions.map((divisionGroup) => {
                  const divisionSpan = divisionGroup.facilities.reduce(
                    (total, facility) => total + facility.warehouses.length,
                    0,
                  );
                  return (
                    <th
                      key={divisionGroup.code}
                      className={`${styles.divisionHeader} ${divisionGroup.code === agreementDivision ? styles.agreementGroupHeader : ""}`}
                      colSpan={divisionSpan}
                      scope="colgroup"
                    >
                      <span>{divisionGroup.code}</span>
                      {divisionGroup.name && <span className={styles.groupName}>{divisionGroup.name}</span>}
                      {divisionGroup.code === agreementDivision && (
                        <span className={styles.headerBadge}>{t("availability.agreementDivision")}</span>
                      )}
                    </th>
                  );
                })}
              </tr>
              {hasWarehouseColumns && (
                <tr>
                  {grid.divisions.flatMap((divisionGroup) =>
                    divisionGroup.facilities.map((facility) => {
                      const facilityKey = getAvailabilityFacilityKey(divisionGroup.code, facility.name);
                      const isAgreementFacility = facilityKey === agreementFacilityKey;
                      const facilityDescription = facilityNames.get(facilityKey);
                      const facilityName = [facility.name, facilityDescription].filter(Boolean).join(" — ");
                      const facilityHeading = (
                        <span>
                          <span>{facility.name || t("availability.unknownFacility")}</span>
                          {facilityDescription && (
                            <>
                              {" "}
                              <span className={styles.groupName}>{facilityDescription}</span>
                            </>
                          )}
                        </span>
                      );
                      return (
                        <th
                          key={facilityKey}
                          className={styles.facilityHeader}
                          colSpan={facility.warehouses.length}
                          scope="colgroup"
                        >
                          {isAgreementFacility ? (
                            facilityHeading
                          ) : (
                            <button
                              type="button"
                              className={styles.removeFacility}
                              aria-label={
                                facility.name
                                  ? t("availability.hideFacility", {
                                      facility: facilityName,
                                      division: divisionGroup.code,
                                    })
                                  : t("availability.hideUnknownFacility")
                              }
                              onClick={() => hideFacility(facilityKey)}
                            >
                              {facilityHeading}
                              <span aria-hidden="true">×</span>
                            </button>
                          )}
                        </th>
                      );
                    }),
                  )}
                </tr>
              )}
              {hasWarehouseColumns && (
                <tr>
                  {grid.warehouses.map((column) => {
                    const warehouseKey = getAvailabilityWarehouseKey(column.code);
                    const isAgreementWarehouse = warehouseKey === agreementWarehouse;
                    const warehouseDescription = column.name ? `${column.code} — ${column.name}` : column.code;
                    const warehouseTitle = isAgreementWarehouse
                      ? t("availability.agreementWarehouse", { warehouse: warehouseDescription })
                      : column.name || column.code;
                    const facilityName = column.facility.trim();
                    const divisionCode = column.divisionCode.trim();
                    const hideWarehouseLabel = t(
                      facilityName && divisionCode
                        ? "availability.hideWarehouseInLocation"
                        : facilityName
                          ? "availability.hideWarehouseInFacility"
                          : divisionCode
                            ? "availability.hideWarehouseInDivision"
                            : "availability.hideWarehouse",
                      {
                        warehouse: column.code,
                        facility: facilityName,
                        division: divisionCode,
                      },
                    );
                    return (
                      <th
                        key={column.code}
                        className={`${styles.warehouseCol} ${isAgreementWarehouse ? styles.highlightCol : ""}`}
                        title={warehouseTitle}
                        aria-label={isAgreementWarehouse ? warehouseTitle : undefined}
                        scope="col"
                      >
                        {isAgreementWarehouse ? (
                          <>
                            <span>{column.code}</span>
                            <span
                              hidden={!isAgreementWarehouse}
                              className={styles.agreementWarehouseMarker}
                              aria-hidden="true"
                            >
                              ✓
                            </span>
                          </>
                        ) : (
                          <button
                            type="button"
                            className={styles.removeWarehouse}
                            aria-label={hideWarehouseLabel}
                            onClick={() => hideWarehouse(warehouseKey)}
                          >
                            <span>{column.code}</span>
                            <span aria-hidden="true">×</span>
                          </button>
                        )}
                      </th>
                    );
                  })}
                </tr>
              )}
            </thead>
            <tbody>
              {grid.items.length === 0 && (
                <tr>
                  <td className={styles.noStockCell} colSpan={colCount}>
                    {t("availability.noStockInDisplayedWarehouses")}
                  </td>
                </tr>
              )}
              {grid.items.map((item) => (
                <Fragment key={item.itemNumber}>
                  <tr>
                    <td
                      className={`${styles.stickyCol} ${styles.mono}`}
                      style={{ width: `${ITEM_NUMBER_COLUMN_WIDTH}px` }}
                    >
                      <span className={styles.itemNumber}>
                        {item.itemNumber}
                        {item.substitutionReason === "RELATED" && (
                          <span
                            className={styles.relatedSubstitution}
                            title={t("availability.relatedSubstitute")}
                            aria-label={t("availability.relatedSubstitute")}
                          >
                            <LuArrowLeftRight aria-hidden="true" />
                          </span>
                        )}
                      </span>
                    </td>
                    <td
                      className={styles.stickyCol}
                      style={{ left: `${ITEM_NUMBER_COLUMN_WIDTH}px`, width: `${DESCRIPTION_COLUMN_WIDTH}px` }}
                    >
                      <span className={styles.description}>{item.descriptionIntl}</span>
                    </td>
                    {grid.warehouses.map((column) => {
                      const cell = item.warehouseData.get(column.code);
                      const isAgreementWarehouse = getAvailabilityWarehouseKey(column.code) === agreementWarehouse;
                      if (!cell) {
                        return (
                          <td
                            key={column.code}
                            className={`${styles.cellEmpty} ${isAgreementWarehouse ? styles.agreementWarehouseCell : ""}`}
                          >
                            —
                          </td>
                        );
                      }

                      const percentage = cell.count > 0 ? Math.round((cell.available / cell.count) * 100) : 0;
                      const isActive =
                        expandedCell?.itemNumber === item.itemNumber &&
                        expandedCell.warehouseCode === column.code &&
                        expandedCell.divisionCode === column.divisionCode;
                      const isQuantityStock = cell.reservationMode === "quantity";
                      const canOpenCell = isQuantityStock ? !!onReserveStock : !!onReserveAsset || readOnly;
                      const cellLabel = t(isQuantityStock ? "availability.allocateStock" : "availability.viewAssets", {
                        itemNumber: item.itemNumber,
                        warehouse: column.code,
                        available: cell.available,
                        count: cell.count,
                      });
                      const stockLabel = t("availability.stockAvailability", {
                        available: cell.available,
                        count: cell.count,
                      });
                      const stockCount = (
                        <span className={styles.stockCount} aria-hidden={true}>
                          <span>{cell.available}</span>
                          <span className={styles.stockCountSecondary}> / {cell.count}</span>
                        </span>
                      );

                      return (
                        <td
                          key={column.code}
                          className={`${getAvailabilityCellClass(cell.available, cell.count, canOpenCell)} ${isAgreementWarehouse ? styles.agreementWarehouseCell : ""} ${isActive ? styles.cellActive : ""}`}
                          title={canOpenCell ? cellLabel : `${stockLabel} (${percentage}%)`}
                        >
                          {canOpenCell ? (
                            <button
                              type="button"
                              className={styles.cellButton}
                              aria-label={cellLabel}
                              aria-expanded={isActive}
                              onClick={() => handleCellClick(item.itemNumber, column.code, column.divisionCode)}
                            >
                              {stockCount}
                            </button>
                          ) : (
                            <span aria-label={stockLabel}>{stockCount}</span>
                          )}
                        </td>
                      );
                    })}
                  </tr>
                  {(() => {
                    const detailCell = expandedCell?.itemNumber === item.itemNumber ? expandedCell : null;
                    const cell = detailCell && item.warehouseData.get(detailCell.warehouseCode);
                    return detailCell && cell ? (
                      <tr className={styles.assetRow}>
                        <td colSpan={colCount} className={styles.assetCell}>
                          <AvailabilityDetails
                            key={`${lineId}-${detailCell.itemNumber}-${detailCell.warehouseCode}-${detailCell.divisionCode}`}
                            expandedCell={detailCell}
                            cell={cell}
                            displayMode="summary"
                            warehouseName={warehouseNames.get(detailCell.warehouseCode)}
                            startDate={startDate}
                            endDate={endDate}
                            lineId={lineId}
                            requiredQuantity={requiredQuantity}
                            fulfilledQuantity={fulfilledQuantity}
                            readOnly={readOnly}
                            onReserveAsset={onReserveAsset}
                            onReserveStock={onReserveStock}
                            onStockReserved={onStockReserved}
                          />
                        </td>
                      </tr>
                    ) : null;
                  })()}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
