import type { AgreementListItem } from "../../services/agreementsService";
import type { AgreementsSortField, SavedColumnFilterValue, SavedViewSortDirection } from "../../types/savedViews";
import {
  matchesMultiValueTextFilter,
  toMultiValueTextFilter,
} from "../../components/common/TableColumnControls/multiValueTextFilterModel";
import {
  matchesDateColumnFilter,
  type DateColumnFilterValue,
} from "../../components/common/TableColumnControls/dateColumnFilterModel";
import { matchesExactFilterSelection, parseFilterSelection } from "../../lib/filterSelection";

export type AgreementOrderTypeFilter = "" | "quote" | "temporaryAgreement" | "agreement";

export interface AgreementListFilters {
  orderTypeFilter: string;
  showHistorical: boolean;
  columnFilters: Readonly<Partial<Record<AgreementsSortField, SavedColumnFilterValue>>>;
  dateColumnFilters?: Readonly<Partial<Record<AgreementsSortField, DateColumnFilterValue>>>;
}

export function filterAgreements(
  agreements: readonly AgreementListItem[],
  { orderTypeFilter, showHistorical, columnFilters, dateColumnFilters = {} }: AgreementListFilters,
  now: Date = new Date(),
): AgreementListItem[] {
  let visibleAgreements: readonly AgreementListItem[] = agreements;

  const orderTypes = new Set(parseFilterSelection(orderTypeFilter));
  if (orderTypes.size > 0) {
    visibleAgreements = visibleAgreements.filter((agreement) => {
      const number = agreement.agreementNumber ?? "";
      return (
        (orderTypes.has("quote") && /^q/i.test(number)) ||
        (orderTypes.has("temporaryAgreement") && /^t/i.test(number)) ||
        (orderTypes.has("agreement") && /^a/i.test(number))
      );
    });
  }

  if (!showHistorical) {
    const cutoff = new Date(now.getTime());
    cutoff.setHours(0, 0, 0, 0);
    cutoff.setDate(cutoff.getDate() - 30);

    visibleAgreements = visibleAgreements.filter((agreement) => {
      if (!agreement.offHireDate) {
        return true;
      }

      const offHire = new Date(agreement.offHireDate);
      if (Number.isNaN(offHire.getTime())) {
        return true;
      }

      return offHire >= cutoff;
    });
  }

  return visibleAgreements.filter(
    (agreement) =>
      Object.entries(columnFilters).every(([field, filter]) => {
        if (!filter) return true;
        if (Array.isArray(filter)) {
          if (field === "warehouse" || field === "fulfilmentStatus") {
            return matchesExactFilterSelection(agreement[field as AgreementsSortField], filter);
          }
          return matchesMultiValueTextFilter(agreement[field as AgreementsSortField], toMultiValueTextFilter(filter));
        }
        if (field === "warehouse") {
          return matchesExactColumnFilter(agreement.warehouse, filter as string);
        }

        return matchesColumnFilter(agreement[field as AgreementsSortField], filter as string);
      }) &&
      Object.entries(dateColumnFilters).every(([field, filter]) =>
        filter
          ? matchesDateColumnFilter(agreement[field as AgreementsSortField] as string | null | undefined, filter, now)
          : true,
      ),
  );
}

export function sortAgreements(
  agreements: readonly AgreementListItem[],
  sortField: AgreementsSortField,
  sortDirection: SavedViewSortDirection,
): AgreementListItem[] {
  return [...agreements].sort((first, second) => {
    const firstValue = first[sortField];
    const secondValue = second[sortField];

    if (firstValue == null && secondValue == null) return 0;
    if (firstValue == null) return 1;
    if (secondValue == null) return -1;

    const comparison =
      typeof firstValue === "string"
        ? firstValue.localeCompare(secondValue as string, undefined, { numeric: true, sensitivity: "base" })
        : (firstValue as number) - (secondValue as number);

    return sortDirection === "asc" ? comparison : -comparison;
  });
}

function matchesColumnFilter(value: string | number | null | undefined, filter: string): boolean {
  return value != null && String(value).toLocaleLowerCase().includes(filter.toLocaleLowerCase());
}

function matchesExactColumnFilter(value: string | number | null | undefined, filter: string): boolean {
  return value != null && String(value).toLocaleLowerCase() === filter.toLocaleLowerCase();
}
