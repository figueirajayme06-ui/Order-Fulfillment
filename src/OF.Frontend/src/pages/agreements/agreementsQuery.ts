import type { AgreementFilterParams } from "../../services/agreementsService";

export interface AgreementsQueryState {
  showHistorical: boolean;
  selectedDivision: string;
  searchTerm: string;
  orderTypeFilter: string;
  statusFilter: string;
  advancedFilters: Readonly<Record<string, string>>;
}

export function buildAgreementFilterParams({
  showHistorical,
  selectedDivision,
  searchTerm,
  orderTypeFilter,
  statusFilter,
  advancedFilters,
}: AgreementsQueryState): AgreementFilterParams {
  const params: AgreementFilterParams = {
    showHistorical,
    division: selectedDivision || undefined,
    search: searchTerm || undefined,
    orderTypes: orderTypeFilter || undefined,
    statuses: statusFilter || undefined,
  };

  if (advancedFilters.customerName) params.customerName = advancedFilters.customerName;
  if (advancedFilters.agreementNumber) params.agreementNumber = advancedFilters.agreementNumber;
  if (advancedFilters.warehouse) params.warehouse = advancedFilters.warehouse;
  if (advancedFilters.onHireDateFrom) params.onHireDateFrom = advancedFilters.onHireDateFrom;
  if (advancedFilters.onHireDateTo) params.onHireDateTo = advancedFilters.onHireDateTo;
  if (advancedFilters.offHireDateFrom) params.offHireDateFrom = advancedFilters.offHireDateFrom;
  if (advancedFilters.offHireDateTo) params.offHireDateTo = advancedFilters.offHireDateTo;
  if (advancedFilters.deliveryDateFrom) params.deliveryDateFrom = advancedFilters.deliveryDateFrom;
  if (advancedFilters.deliveryDateTo) params.deliveryDateTo = advancedFilters.deliveryDateTo;
  if (advancedFilters.validFromDate) params.validFromDate = advancedFilters.validFromDate;
  if (advancedFilters.validToDate) params.validToDate = advancedFilters.validToDate;
  if (advancedFilters.terminationDateFrom) params.terminationDateFrom = advancedFilters.terminationDateFrom;
  if (advancedFilters.terminationDateTo) params.terminationDateTo = advancedFilters.terminationDateTo;
  if (advancedFilters.collectionDateFrom) params.collectionDateFrom = advancedFilters.collectionDateFrom;
  if (advancedFilters.collectionDateTo) params.collectionDateTo = advancedFilters.collectionDateTo;
  if (advancedFilters.customerAddress) params.customerAddress = advancedFilters.customerAddress;
  if (advancedFilters.lastUpdatedByName) params.lastUpdatedByName = advancedFilters.lastUpdatedByName;

  return params;
}
