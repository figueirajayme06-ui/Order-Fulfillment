import type { AssetFilterParams } from "../../services/assetsService";

export interface AssetsQueryState {
  searchTerm: string;
  statusFilter: string;
  warehouseFilter: string;
  selectedDivision: string;
  hideRemovedStock: boolean;
  advancedFilters: Readonly<Record<string, string>>;
}

export function buildAssetFilterParams({
  searchTerm,
  statusFilter,
  warehouseFilter,
  selectedDivision,
  hideRemovedStock,
  advancedFilters,
}: AssetsQueryState): AssetFilterParams {
  const params: AssetFilterParams = {
    search: searchTerm || undefined,
    statuses: statusFilter || undefined,
    warehouse: warehouseFilter || undefined,
    division: selectedDivision || undefined,
    excludeStatuses: hideRemovedStock ? "RemovedStock,Scrap,Sold" : undefined,
  };

  if (advancedFilters.itemNumber) params.itemNumber = advancedFilters.itemNumber;
  if (advancedFilters.description) params.description = advancedFilters.description;
  if (advancedFilters.facility) params.facility = advancedFilters.facility;
  if (advancedFilters.agreementNumber) params.agreementNumber = advancedFilters.agreementNumber;
  if (advancedFilters.deliveryDateFrom) params.deliveryDateFrom = advancedFilters.deliveryDateFrom;
  if (advancedFilters.deliveryDateTo) params.deliveryDateTo = advancedFilters.deliveryDateTo;
  if (advancedFilters.validFromDate) params.validFromDate = advancedFilters.validFromDate;
  if (advancedFilters.validToDate) params.validToDate = advancedFilters.validToDate;
  if (advancedFilters.warehouseLocation) params.warehouseLocation = advancedFilters.warehouseLocation;
  if (advancedFilters.individualItemNumber) params.individualItemNumber = advancedFilters.individualItemNumber;
  if (advancedFilters.terminationDateFrom) params.terminationDateFrom = advancedFilters.terminationDateFrom;
  if (advancedFilters.terminationDateTo) params.terminationDateTo = advancedFilters.terminationDateTo;
  if (advancedFilters.collectionDateFrom) params.collectionDateFrom = advancedFilters.collectionDateFrom;
  if (advancedFilters.collectionDateTo) params.collectionDateTo = advancedFilters.collectionDateTo;
  if (advancedFilters.estimatedReadyDateFrom) params.estimatedReadyDateFrom = advancedFilters.estimatedReadyDateFrom;
  if (advancedFilters.estimatedReadyDateTo) params.estimatedReadyDateTo = advancedFilters.estimatedReadyDateTo;

  return params;
}
