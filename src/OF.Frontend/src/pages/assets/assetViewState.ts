import type { DateColumnFilterValue } from "../../components/common/TableColumnControls/dateColumnFilterModel";
import { toMultiValueTextFilter } from "../../components/common/TableColumnControls/multiValueTextFilterModel";
import { serializeFilterSelection } from "../../lib/filterSelection";
import type {
  AssetsSavedViewState,
  SavedColumnFilterValue,
  AssetsSortField as SortField,
} from "../../types/savedViews";
import type { AssetTimelineColumnFilters } from "./assetsTimelineModel";

const ASSET_COLUMN_FILTER_FIELDS: ReadonlySet<SortField> = new Set([
  "id",
  "itemNumber",
  "description",
  "warehouse",
  "customerName",
  "daysOffHire",
]);

const ASSET_DATE_COLUMN_FILTER_FIELDS: ReadonlySet<SortField> = new Set([
  "deliveryDate",
  "agreementLineValidFromDate",
  "agreementLineValidToDate",
  "terminationDate",
  "collectionDate",
]);

export function toAssetColumnFilters(
  filters: Record<string, SavedColumnFilterValue>,
): Partial<Record<SortField, SavedColumnFilterValue>> {
  return Object.fromEntries(
    Object.entries(filters).filter(([field]) => ASSET_COLUMN_FILTER_FIELDS.has(field as SortField)),
  ) as Partial<Record<SortField, SavedColumnFilterValue>>;
}

export function toAssetDateColumnFilters(
  filters: Record<string, DateColumnFilterValue>,
): Partial<Record<SortField, DateColumnFilterValue>> {
  return Object.fromEntries(
    Object.entries(filters).filter(([field]) => ASSET_DATE_COLUMN_FILTER_FIELDS.has(field as SortField)),
  ) as Partial<Record<SortField, DateColumnFilterValue>>;
}

export function removeSyncedAssetTimelineFilters(
  filters: Readonly<AssetTimelineColumnFilters>,
): AssetTimelineColumnFilters {
  const supportedFilters = { ...filters };
  delete supportedFilters.status;
  delete supportedFilters.division;
  return supportedFilters;
}

export function getAssetStatusFilter(state: AssetsSavedViewState | null): string {
  if (!state) return "";
  if (state.statusFilter) return state.statusFilter;
  return serializeFilterSelection(
    toMultiValueTextFilter(state.columnFilters.status ?? state.timelineColumnFilters?.status),
  );
}

export function getAssetDivisionFilter(state: AssetsSavedViewState): string {
  const legacyColumnDivision = toMultiValueTextFilter(
    state.columnFilters.division ?? state.timelineColumnFilters?.division,
  );
  return legacyColumnDivision.length > 0 ? serializeFilterSelection(legacyColumnDivision) : state.selectedDivision;
}
