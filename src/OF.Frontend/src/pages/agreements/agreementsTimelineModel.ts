import type { GanttTask } from "../../components/timeline/FrappeGantt";
import type { AgreementListItem } from "../../services/agreementsService";
import { mapFulfilmentStatus } from "../../types";
import type { AgreementTimelineColumnKey } from "./agreementColumnCatalog";
import type { SavedColumnFilterValue } from "../../types/savedViews";
import {
  matchesMultiValueTextFilter,
  toMultiValueTextFilter,
} from "../../components/common/TableColumnControls/multiValueTextFilterModel";
import { matchesExactFilterSelection } from "../../lib/filterSelection";

export type AgreementTimelineFilterField = AgreementTimelineColumnKey | "customerOrOpportunity";

export type AgreementTimelineColumnFilters = Partial<Record<AgreementTimelineFilterField, SavedColumnFilterValue>>;

export function filterAgreementTimelineAgreements(
  agreements: readonly AgreementListItem[],
  columnFilters: Readonly<AgreementTimelineColumnFilters>,
): AgreementListItem[] {
  return agreements.filter((agreement) =>
    Object.entries(columnFilters).every(([field, filter]) => {
      if (!filter) return true;

      if (field === "customerOrOpportunity") {
        const values = toMultiValueTextFilter(filter);
        return (
          matchesMultiValueTextFilter(agreement.customerName, values) ||
          matchesMultiValueTextFilter(agreement.opportunityName, values)
        );
      }

      if (Array.isArray(filter)) {
        if (field === "warehouse" || field === "fulfilmentStatus") {
          return matchesExactFilterSelection(agreement[field as AgreementTimelineColumnKey], filter);
        }
        return matchesMultiValueTextFilter(agreement[field as AgreementTimelineColumnKey], filter);
      }
      return matchesFilter(agreement[field as AgreementTimelineColumnKey], filter as string);
    }),
  );
}

export function getAgreementTimelineHistoryStart(today: string): string {
  const [year, month, day] = today.split("-").map(Number);
  return formatDateValue(new Date(year, month - 13, day));
}

export function filterAgreementTimelineHistory(
  agreements: readonly AgreementListItem[],
  today: string,
): AgreementListItem[] {
  const historyStart = getAgreementTimelineHistoryStart(today);
  return agreements.filter((agreement) => getAgreementTimelineDateRange(agreement, today).end >= historyStart);
}

export function filterAgreementTimelineDateRange(
  agreements: readonly AgreementListItem[],
  startDate: string,
  endDate: string,
  today: string,
): AgreementListItem[] {
  return agreements.filter((agreement) => {
    const period = getAgreementTimelineDateRange(agreement, today);
    return (!startDate || period.end >= startDate) && (!endDate || period.start <= endDate);
  });
}

export function buildAgreementTimelineTasks(agreements: readonly AgreementListItem[], today: string): GanttTask[] {
  const tasks: GanttTask[] = [];
  const historyStart = getAgreementTimelineHistoryStart(today);

  agreements.forEach((agreement) => {
    const statusKind = mapFulfilmentStatus(agreement.fulfilmentStatus);
    if (statusKind === "unknown") {
      return;
    }

    const range = getAgreementTimelineDateRange(agreement, today);
    if (range.end < historyStart) {
      return;
    }

    const start = range.start < historyStart ? historyStart : range.start;
    const end = range.end > start ? range.end : addDays(start, 7);
    let customClass = "bar-fulfilled";
    if (statusKind === "unfulfilled") customClass = "bar-unfulfilled";
    if (statusKind === "partial") customClass = "bar-partial";

    tasks.push({
      id: String(agreement.id),
      name: `${agreement.agreementNumber ?? "—"} — ${agreement.customerName ?? "Unknown"}`,
      start,
      end,
      progress: statusKind === "fulfilled" ? 100 : statusKind === "partial" ? 50 : 0,
      custom_class: customClass,
    });
  });

  return tasks;
}

function getAgreementTimelineDateRange(agreement: AgreementListItem, today: string): { start: string; end: string } {
  const rawStart = agreement.onHireDate?.split("T")[0] ?? today;
  const rawEnd = agreement.offHireDate?.split("T")[0] ?? addDays(rawStart, 30);

  return {
    start: rawStart <= rawEnd ? rawStart : rawEnd,
    end: rawEnd > rawStart ? rawEnd : addDays(rawStart, 7),
  };
}

function addDays(dateStr: string, days: number): string {
  const date = new Date(dateStr);
  date.setDate(date.getDate() + days);
  return date.toISOString().split("T")[0];
}

function formatDateValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function matchesFilter(value: string | number | null | undefined, filter: string): boolean {
  return value != null && String(value).toLocaleLowerCase().includes(filter.toLocaleLowerCase());
}
