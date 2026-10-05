import type { Asset } from "../../types";
import type { AssetsSortField, SavedColumnFilterValue, SavedViewSortDirection } from "../../types/savedViews";
import {
  matchesMultiValueTextFilter,
  toMultiValueTextFilter,
} from "../../components/common/TableColumnControls/multiValueTextFilterModel";
import {
  matchesDateColumnFilter,
  type DateColumnFilterValue,
} from "../../components/common/TableColumnControls/dateColumnFilterModel";
import { matchesExactFilterSelection } from "../../lib/filterSelection";

export type AssetColumnFilters = Partial<Record<AssetsSortField, SavedColumnFilterValue>>;
export type AssetDateColumnFilters = Partial<Record<AssetsSortField, DateColumnFilterValue>>;

export function filterAssets(
  assets: readonly Asset[],
  columnFilters: Readonly<AssetColumnFilters>,
  dateColumnFilters: Readonly<AssetDateColumnFilters> = {},
  now: Date = new Date(),
): Asset[] {
  return assets.filter(
    (asset) =>
      Object.entries(columnFilters).every(([field, filter]) => {
        if (!filter) return true;

        if (Array.isArray(filter)) {
          if (field === "status" || field === "warehouse" || field === "division") {
            return matchesExactFilterSelection(asset[field as AssetsSortField], filter);
          }
          return matchesMultiValueTextFilter(asset[field as AssetsSortField], toMultiValueTextFilter(filter));
        }
        return matchesColumnFilter(asset[field as AssetsSortField], filter as string);
      }) &&
      Object.entries(dateColumnFilters).every(([field, filter]) =>
        filter
          ? matchesDateColumnFilter(asset[field as AssetsSortField] as string | null | undefined, filter, now)
          : true,
      ),
  );
}

export function sortAssets(
  assets: readonly Asset[],
  sortField: AssetsSortField,
  sortDirection: SavedViewSortDirection,
): Asset[] {
  return [...assets].sort((first, second) => {
    const firstValue = first[sortField];
    const secondValue = second[sortField];

    if (firstValue == null && secondValue == null) return 0;
    if (firstValue == null) return 1;
    if (secondValue == null) return -1;

    const comparison =
      typeof firstValue === "string"
        ? firstValue.localeCompare(secondValue as string, undefined, { numeric: true, sensitivity: "base" })
        : firstValue - (secondValue as number);

    return sortDirection === "asc" ? comparison : -comparison;
  });
}

function matchesColumnFilter(value: string | number | null | undefined, filter: string): boolean {
  return value != null && String(value).toLocaleLowerCase().includes(filter.toLocaleLowerCase());
}
