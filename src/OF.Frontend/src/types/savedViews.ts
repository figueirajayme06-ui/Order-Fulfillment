import { isFulfilmentStatusFilterValue } from "./fulfilmentStatus";
import {
  isDateFilterOperator,
  isIsoCalendarDate,
  requiresDateValue,
  type DateColumnFilterValue,
} from "../components/common/TableColumnControls/dateColumnFilterModel";
import {
  toMultiValueTextFilter,
  type MultiValueTextFilter,
} from "../components/common/TableColumnControls/multiValueTextFilterModel";
import {
  limitVisibleTableColumns,
  repairTableColumnLayout,
  type TableColumnLayoutItem,
} from "../lib/tableColumnLayout";
import {
  AGREEMENT_COLUMN_CATALOG,
  AGREEMENT_TIMELINE_COLUMN_CATALOG,
  type AgreementColumnKey,
  type AgreementTimelineColumnKey,
} from "../pages/agreements/agreementColumnCatalog";
import {
  ASSET_COLUMN_CATALOG,
  ASSET_TIMELINE_COLUMN_CATALOG,
  type AssetColumnKey,
  type AssetTimelineColumnKey,
} from "../pages/assets/assetColumnCatalog";
import { normalizeFilterSelection, serializeFilterSelection } from "../lib/filterSelection";

export type SavedViewMode = "table" | "timeline";
export type SavedViewSortDirection = "asc" | "desc";
export type SavedColumnFilterValue = string | MultiValueTextFilter;
export const MAX_VISIBLE_TIMELINE_COLUMNS = 10;

export type AgreementsSortField =
  | "fulfilmentStatus"
  | "agreementNumber"
  | "customerName"
  | "opportunityName"
  | "division"
  | "warehouse"
  | "deliveryDate"
  | "validFromDate"
  | "validToDate"
  | "terminationDate"
  | "collectionDate"
  | "lastUpdatedByName"
  | "lineCount"
  | "fromDate"
  | "toDate"
  | "customerNumber"
  | "customerAddress"
  | "lastUpdatedDate"
  | "opportunityStage"
  | "probability"
  | "onHireDate"
  | "offHireDate";
export type AssetsSortField =
  | "id"
  | "itemNumber"
  | "description"
  | "warehouse"
  | "division"
  | "customerName"
  | "daysOffHire"
  | "deliveryDate"
  | "agreementLineValidFromDate"
  | "agreementLineValidToDate"
  | "terminationDate"
  | "collectionDate"
  | "status"
  | "warehouseName"
  | "agreementNumber"
  | "customerNumber"
  | "facility"
  | "warehouseLocation"
  | "estimatedReadyDate"
  | "productGroup"
  | "productCategory"
  | "runHours"
  | "size"
  | "telemetryStatus"
  | "remark";

export interface AgreementsSavedViewState {
  stateVersion: 1 | 2 | 3 | 4 | 5;
  viewMode: SavedViewMode;
  searchTerm: string;
  showHistorical: boolean;
  selectedDivision: string;
  orderTypeFilter: string;
  statusFilter: string;
  advancedFilters: Record<string, string>;
  columnFilters: Record<string, SavedColumnFilterValue>;
  timelineColumnFilters?: Record<string, SavedColumnFilterValue>;
  dateColumnFilters?: Record<string, DateColumnFilterValue>;
  columns?: TableColumnLayoutItem<AgreementColumnKey>[];
  timelineColumns?: TableColumnLayoutItem<AgreementTimelineColumnKey>[];
  sortField: AgreementsSortField;
  sortDirection: SavedViewSortDirection;
  currentPage?: number;
  timelinePage?: number;
  pageSize?: number;
}

export interface AssetsSavedViewState {
  stateVersion: 1 | 2 | 3 | 4 | 5;
  viewMode: SavedViewMode;
  searchTerm: string;
  statusFilter: string;
  warehouseFilter: string;
  hideRemovedStock: boolean;
  selectedDivision: string;
  advancedFilters: Record<string, string>;
  columnFilters: Record<string, SavedColumnFilterValue>;
  timelineColumnFilters?: Record<string, SavedColumnFilterValue>;
  dateColumnFilters?: Record<string, DateColumnFilterValue>;
  columns?: TableColumnLayoutItem<AssetColumnKey>[];
  timelineColumns?: TableColumnLayoutItem<AssetTimelineColumnKey>[];
  sortField: AssetsSortField;
  sortDirection: SavedViewSortDirection;
  currentPage?: number;
  timelinePage?: number;
  pageSize?: number;
}

const AGREEMENT_SORT_FIELDS: ReadonlySet<AgreementsSortField> = new Set([
  "agreementNumber",
  "customerName",
  "opportunityName",
  "division",
  "warehouse",
  "deliveryDate",
  "validFromDate",
  "validToDate",
  "terminationDate",
  "collectionDate",
  "lastUpdatedByName",
  "lineCount",
  "fromDate",
  "toDate",
  "customerNumber",
  "customerAddress",
  "lastUpdatedDate",
  "opportunityStage",
  "probability",
  "onHireDate",
  "offHireDate",
  "fulfilmentStatus",
]);

const ASSET_SORT_FIELDS: ReadonlySet<AssetsSortField> = new Set([
  "id",
  "itemNumber",
  "description",
  "warehouse",
  "division",
  "customerName",
  "daysOffHire",
  "deliveryDate",
  "agreementLineValidFromDate",
  "agreementLineValidToDate",
  "terminationDate",
  "collectionDate",
  "status",
  "warehouseName",
  "agreementNumber",
  "customerNumber",
  "facility",
  "warehouseLocation",
  "estimatedReadyDate",
  "productGroup",
  "productCategory",
  "runHours",
  "size",
  "telemetryStatus",
  "remark",
]);

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function toStringRecord(value: unknown): Record<string, string> {
  if (!isRecord(value)) {
    return {};
  }

  const next: Record<string, string> = {};

  Object.entries(value).forEach(([key, entryValue]) => {
    if (typeof entryValue === "string") {
      next[key] = entryValue;
    }
  });

  return next;
}

const AGREEMENT_MULTI_VALUE_FIELDS = new Set([
  "fulfilmentStatus",
  "agreementNumber",
  "customerName",
  "customerOrOpportunity",
  "opportunityName",
  "lastUpdatedByName",
  "warehouse",
]);
const ASSET_MULTI_VALUE_FIELDS = new Set(["id", "itemNumber", "description", "customerName", "status"]);
const AGREEMENT_ORDER_TYPES = ["quote", "temporaryAgreement", "agreement"];
const FULFILMENT_STATUSES = ["0", "1", "3"];

function toColumnFilterRecord(
  value: unknown,
  multiValueFields: ReadonlySet<string>,
): Record<string, SavedColumnFilterValue> {
  if (!isRecord(value)) return {};
  const filters: Record<string, SavedColumnFilterValue> = {};
  Object.entries(value).forEach(([field, candidate]) => {
    if (multiValueFields.has(field)) {
      const values = toMultiValueTextFilter(candidate);
      if (values.length > 0) filters[field] = values;
    } else if (typeof candidate === "string") {
      filters[field] = candidate;
    }
  });
  return filters;
}

function normalizeAgreementStatusColumnFilter(filters: Record<string, SavedColumnFilterValue>): void {
  if (!Array.isArray(filters.fulfilmentStatus)) return;
  filters.fulfilmentStatus = filters.fulfilmentStatus.filter(isFulfilmentStatusFilterValue);
  if (filters.fulfilmentStatus.length === 0) delete filters.fulfilmentStatus;
}

function toPositiveInteger(value: unknown, fallback: number): number {
  return typeof value === "number" && Number.isSafeInteger(value) && value > 0 ? value : fallback;
}

function isSupportedStateVersion(value: unknown): boolean {
  return value == null || (typeof value === "number" && Number.isInteger(value) && value >= 1 && value <= 5);
}

function toDateColumnFilters(value: unknown): Record<string, DateColumnFilterValue> {
  if (!isRecord(value)) return {};
  const filters: Record<string, DateColumnFilterValue> = {};
  Object.entries(value).forEach(([field, candidate]) => {
    if (!isRecord(candidate) || !isDateFilterOperator(candidate.operator)) return;
    if (requiresDateValue(candidate.operator)) {
      if (!isIsoCalendarDate(candidate.value)) return;
      filters[field] = { operator: candidate.operator, value: candidate.value };
      return;
    }
    filters[field] = { operator: candidate.operator };
  });
  return filters;
}

export function parseAgreementsSavedViewState(value: unknown): AgreementsSavedViewState | null {
  if (!isRecord(value) || !isSupportedStateVersion(value.stateVersion)) {
    return null;
  }

  const viewMode = value.viewMode;
  const sortField = value.sortField;
  const sortDirection = value.sortDirection;

  if (viewMode !== "table" && viewMode !== "timeline") {
    return null;
  }

  if (typeof sortField !== "string" || !AGREEMENT_SORT_FIELDS.has(sortField as AgreementsSortField)) {
    return null;
  }

  if (sortDirection !== "asc" && sortDirection !== "desc") {
    return null;
  }

  const statusFilter = normalizeFilterSelection(value.statusFilter, FULFILMENT_STATUSES);
  const columnFilters = toColumnFilterRecord(value.columnFilters, AGREEMENT_MULTI_VALUE_FIELDS);
  const timelineColumnFilters = toColumnFilterRecord(value.timelineColumnFilters, AGREEMENT_MULTI_VALUE_FIELDS);
  normalizeAgreementStatusColumnFilter(columnFilters);
  normalizeAgreementStatusColumnFilter(timelineColumnFilters);

  return {
    stateVersion: 5,
    viewMode,
    searchTerm: typeof value.searchTerm === "string" ? value.searchTerm : "",
    showHistorical: typeof value.showHistorical === "boolean" ? value.showHistorical : false,
    selectedDivision: serializeFilterSelection(
      typeof value.selectedDivision === "string" ? value.selectedDivision.split(",") : [],
    ),
    orderTypeFilter: normalizeFilterSelection(value.orderTypeFilter, AGREEMENT_ORDER_TYPES),
    statusFilter,
    advancedFilters: toStringRecord(value.advancedFilters),
    columnFilters,
    timelineColumnFilters,
    dateColumnFilters: toDateColumnFilters(value.dateColumnFilters),
    columns: repairTableColumnLayout(AGREEMENT_COLUMN_CATALOG, value.columns),
    timelineColumns: limitVisibleTableColumns(
      AGREEMENT_TIMELINE_COLUMN_CATALOG,
      repairTableColumnLayout(AGREEMENT_TIMELINE_COLUMN_CATALOG, value.timelineColumns),
      MAX_VISIBLE_TIMELINE_COLUMNS,
    ),
    sortField: sortField as AgreementsSortField,
    sortDirection,
    currentPage: toPositiveInteger(value.currentPage, 1),
    timelinePage: toPositiveInteger(value.timelinePage, 1),
    pageSize: [50, 250, 500, 1000].includes(Number(value.pageSize)) ? Number(value.pageSize) : 50,
  };
}

export function parseAssetsSavedViewState(value: unknown): AssetsSavedViewState | null {
  if (!isRecord(value) || !isSupportedStateVersion(value.stateVersion)) {
    return null;
  }

  const viewMode = value.viewMode;
  const sortField = value.sortField;
  const sortDirection = value.sortDirection;

  if (viewMode !== "table" && viewMode !== "timeline") {
    return null;
  }

  if (typeof sortField !== "string" || !ASSET_SORT_FIELDS.has(sortField as AssetsSortField)) {
    return null;
  }

  if (sortDirection !== "asc" && sortDirection !== "desc") {
    return null;
  }

  return {
    stateVersion: 5,
    viewMode,
    searchTerm: typeof value.searchTerm === "string" ? value.searchTerm : "",
    statusFilter: serializeFilterSelection(typeof value.statusFilter === "string" ? value.statusFilter.split(",") : []),
    warehouseFilter: typeof value.warehouseFilter === "string" ? value.warehouseFilter : "",
    hideRemovedStock: typeof value.hideRemovedStock === "boolean" ? value.hideRemovedStock : false,
    selectedDivision: serializeFilterSelection(
      typeof value.selectedDivision === "string" ? value.selectedDivision.split(",") : [],
    ),
    advancedFilters: toStringRecord(value.advancedFilters),
    columnFilters: toColumnFilterRecord(value.columnFilters, ASSET_MULTI_VALUE_FIELDS),
    timelineColumnFilters: toColumnFilterRecord(value.timelineColumnFilters, ASSET_MULTI_VALUE_FIELDS),
    dateColumnFilters: toDateColumnFilters(value.dateColumnFilters),
    columns: repairTableColumnLayout(ASSET_COLUMN_CATALOG, value.columns),
    timelineColumns: limitVisibleTableColumns(
      ASSET_TIMELINE_COLUMN_CATALOG,
      repairTableColumnLayout(ASSET_TIMELINE_COLUMN_CATALOG, value.timelineColumns),
      MAX_VISIBLE_TIMELINE_COLUMNS,
    ),
    sortField: sortField as AssetsSortField,
    sortDirection,
    currentPage: toPositiveInteger(value.currentPage, 1),
    timelinePage: toPositiveInteger(value.timelinePage, 1),
    pageSize: [50, 250, 500, 1000].includes(Number(value.pageSize)) ? Number(value.pageSize) : 50,
  };
}
