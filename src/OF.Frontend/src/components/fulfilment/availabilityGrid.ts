import type { AvailabilityItem } from "../../types/availability";
import type { WarehouseLookup } from "../../services/lookupsService";

export interface AvailabilityCellData {
  available: number;
  count: number;
  reservationMode: "asset" | "quantity" | "generic";
}

export interface AvailabilityItemRow {
  itemNumber: string;
  descriptionIntl: string;
  substitutionReason?: "RELATED" | null;
  warehouseData: Map<string, AvailabilityCellData>;
}

export interface AvailabilityWarehouseColumn {
  code: string;
  name: string;
  facility: string;
  divisionCode: string;
  divisionName: string;
}

export interface AvailabilityFacilityGroup {
  name: string;
  warehouses: AvailabilityWarehouseColumn[];
}

export interface AvailabilityDivisionGroup {
  code: string;
  name: string;
  facilities: AvailabilityFacilityGroup[];
}

export interface AvailabilityGridModel {
  items: AvailabilityItemRow[];
  divisions: AvailabilityDivisionGroup[];
  warehouses: AvailabilityWarehouseColumn[];
}

function normalizeDivisionCode(code?: string): string {
  return code?.trim().toUpperCase() ?? "";
}

const UNKNOWN_FACILITY_ID = "__UNKNOWN_FACILITY__";

function normalizeFacilityName(name?: string): string {
  return name?.trim().toUpperCase() || UNKNOWN_FACILITY_ID;
}

export function getAvailabilityFacilityKey(divisionCode?: string, facilityName?: string): string {
  return `${normalizeDivisionCode(divisionCode)}|${normalizeFacilityName(facilityName)}`;
}

export function getAvailabilityWarehouseKey(warehouseCode?: string): string {
  return warehouseCode?.trim().toUpperCase() ?? "";
}

export function getDefaultHiddenWarehouseKeys(grid: AvailabilityGridModel, agreementWarehouse?: string): Set<string> {
  const protectedWarehouse = getAvailabilityWarehouseKey(agreementWarehouse);

  return new Set(
    grid.warehouses
      .filter((warehouse) => {
        const warehouseKey = getAvailabilityWarehouseKey(warehouse.code);
        if (!warehouseKey || warehouseKey === protectedWarehouse) return false;

        // Warehouses with a real (even zero-available) result stay visible; only truly empty ones are hidden.
        return !grid.items.some((item) => {
          const cell = item.warehouseData.get(warehouseKey);
          return cell?.reservationMode === "asset" && cell.count > 0;
        });
      })
      .map((warehouse) => getAvailabilityWarehouseKey(warehouse.code)),
  );
}

function compareWithAgreementFirst(a: string, b: string, agreementValue?: string): number {
  if (agreementValue) {
    const normalizedAgreement = agreementValue.trim().toUpperCase();
    const normalizedA = a.trim().toUpperCase();
    const normalizedB = b.trim().toUpperCase();
    if (normalizedA === normalizedAgreement && normalizedB !== normalizedAgreement) return -1;
    if (normalizedA !== normalizedAgreement && normalizedB === normalizedAgreement) return 1;
  }

  return a.localeCompare(b, undefined, { numeric: true, sensitivity: "base" });
}

export function buildAvailabilityGrid(
  data: AvailabilityItem[],
  genericCode: string,
  agreementDivision?: string,
  agreementWarehouse?: string,
  warehouseCatalog: readonly WarehouseLookup[] = [],
): AvailabilityGridModel {
  const itemMap = new Map<string, AvailabilityItemRow>();
  const warehouseMap = new Map<string, AvailabilityWarehouseColumn>();
  const normalizedAgreementWarehouse = getAvailabilityWarehouseKey(agreementWarehouse);

  const mergeWarehouseLocation = (location: WarehouseLookup) => {
    const warehouseCode = getAvailabilityWarehouseKey(location.warehouseCode);
    if (!warehouseCode) return;

    const candidate: AvailabilityWarehouseColumn = {
      code: warehouseCode,
      name: location.warehouse.trim(),
      facility: location.facility.trim(),
      divisionCode: normalizeDivisionCode(location.divisionCode),
      divisionName: location.divisionName.trim(),
    };
    const existing = warehouseMap.get(warehouseCode);
    warehouseMap.set(
      warehouseCode,
      existing
        ? {
            code: warehouseCode,
            name: existing.name || candidate.name,
            facility: existing.facility || candidate.facility,
            divisionCode: existing.divisionCode || candidate.divisionCode,
            divisionName: existing.divisionName || candidate.divisionName,
          }
        : candidate,
    );
  };

  if (normalizedAgreementWarehouse) {
    mergeWarehouseLocation({
      warehouseCode: normalizedAgreementWarehouse,
      warehouse: "",
      facility: "",
      divisionCode: agreementDivision ?? "",
      divisionName: "",
    });
  }

  warehouseCatalog.forEach(mergeWarehouseLocation);

  data.forEach((item) => {
    const warehouseCode = getAvailabilityWarehouseKey(item.warehouseCode);
    if (!itemMap.has(item.itemNumber)) {
      itemMap.set(item.itemNumber, {
        itemNumber: item.itemNumber,
        descriptionIntl: item.descriptionIntl,
        substitutionReason: item.substitutionReason,
        warehouseData: new Map(),
      });
    }

    itemMap.get(item.itemNumber)!.warehouseData.set(warehouseCode, {
      available: item.available,
      count: item.count,
      reservationMode: item.reservationMode,
    });

    mergeWarehouseLocation(item);
  });

  const items = Array.from(itemMap.values()).sort((a, b) => {
    const aRelated = a.substitutionReason === "RELATED";
    const bRelated = b.substitutionReason === "RELATED";
    if (aRelated && !bRelated) return 1;
    if (!aRelated && bRelated) return -1;

    const aMain = a.itemNumber.startsWith(genericCode);
    const bMain = b.itemNumber.startsWith(genericCode);
    if (aMain && !bMain) return -1;
    if (!aMain && bMain) return 1;
    return a.itemNumber.localeCompare(b.itemNumber, undefined, { numeric: true, sensitivity: "base" });
  });

  const divisions: AvailabilityDivisionGroup[] = [];

  warehouseMap.forEach((warehouse) => {
    let division = divisions.find(
      (candidate) => normalizeDivisionCode(candidate.code) === normalizeDivisionCode(warehouse.divisionCode),
    );
    if (!division) {
      division = {
        code: warehouse.divisionCode,
        name: warehouse.divisionName,
        facilities: [],
      };
      divisions.push(division);
    }

    const facilityName = warehouse.facility.trim();
    let facility = division.facilities.find(
      (candidate) => normalizeFacilityName(candidate.name) === normalizeFacilityName(facilityName),
    );
    if (!facility) {
      facility = { name: facilityName, warehouses: [] };
      division.facilities.push(facility);
    }

    facility.warehouses.push(warehouse);
  });

  divisions.sort((a, b) => compareWithAgreementFirst(a.code, b.code, agreementDivision));
  divisions.forEach((division) => {
    const agreementFacility = division.facilities.find((facility) =>
      facility.warehouses.some(
        (warehouse) => getAvailabilityWarehouseKey(warehouse.code) === getAvailabilityWarehouseKey(agreementWarehouse),
      ),
    )?.name;

    division.facilities.sort((a, b) => compareWithAgreementFirst(a.name, b.name, agreementFacility));
    division.facilities.forEach((facility) => {
      facility.warehouses.sort((a, b) => compareWithAgreementFirst(a.code, b.code, agreementWarehouse));
    });
  });

  return {
    items,
    divisions,
    warehouses: divisions.flatMap((division) => division.facilities.flatMap((facility) => facility.warehouses)),
  };
}

export function filterAvailabilityGridByWarehouses(
  grid: AvailabilityGridModel,
  hiddenWarehouseKeys: ReadonlySet<string>,
): AvailabilityGridModel {
  if (hiddenWarehouseKeys.size === 0) return grid;

  const normalizedHiddenKeys = new Set(Array.from(hiddenWarehouseKeys, getAvailabilityWarehouseKey));
  const divisions = grid.divisions
    .map((division) => ({
      ...division,
      facilities: division.facilities
        .map((facility) => ({
          ...facility,
          warehouses: facility.warehouses.filter(
            (warehouse) => !normalizedHiddenKeys.has(getAvailabilityWarehouseKey(warehouse.code)),
          ),
        }))
        .filter((facility) => facility.warehouses.length > 0),
    }))
    .filter((division) => division.facilities.length > 0);

  return {
    ...grid,
    divisions,
    warehouses: divisions.flatMap((division) => division.facilities.flatMap((facility) => facility.warehouses)),
  };
}

export function filterAvailabilityGridByFacilities(
  grid: AvailabilityGridModel,
  hiddenFacilityKeys: ReadonlySet<string>,
): AvailabilityGridModel {
  if (hiddenFacilityKeys.size === 0) return grid;

  const normalizedHiddenKeys = new Set(Array.from(hiddenFacilityKeys, (key) => key.trim().toUpperCase()));
  const divisions = grid.divisions
    .map((division) => ({
      ...division,
      facilities: division.facilities.filter(
        (facility) => !normalizedHiddenKeys.has(getAvailabilityFacilityKey(division.code, facility.name)),
      ),
    }))
    .filter((division) => division.facilities.length > 0);

  return {
    ...grid,
    divisions,
    warehouses: divisions.flatMap((division) => division.facilities.flatMap((facility) => facility.warehouses)),
  };
}

export function filterAvailabilityGridByStock(grid: AvailabilityGridModel): AvailabilityGridModel {
  if (grid.warehouses.length === 0) return grid;

  const visibleWarehouseKeys = new Set(grid.warehouses.map((warehouse) => getAvailabilityWarehouseKey(warehouse.code)));

  return {
    ...grid,
    items: grid.items.filter((item) =>
      Array.from(item.warehouseData).some(
        ([warehouseKey, cell]) => visibleWarehouseKeys.has(getAvailabilityWarehouseKey(warehouseKey)) && cell.count > 0,
      ),
    ),
  };
}
