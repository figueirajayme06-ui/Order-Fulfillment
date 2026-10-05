import {
  matchesMultiValueTextFilter,
  toMultiValueTextFilter,
} from "../../components/common/TableColumnControls/multiValueTextFilterModel";
import type { GanttTask } from "../../components/timeline/FrappeGantt";
import { timelineDate, timelineGeometry } from "../../components/timeline/timelineDates";
import { matchesExactFilterSelection } from "../../lib/filterSelection";
import type { Asset, AssetEvent } from "../../types";
import type { SavedColumnFilterValue } from "../../types/savedViews";
import type { AssetTimelineColumnKey } from "./assetColumnCatalog";

export type AssetTimelineFilterField = AssetTimelineColumnKey;
export type AssetTimelineColumnFilters = Partial<Record<AssetTimelineFilterField, SavedColumnFilterValue>>;

export function filterAssetTimelineAssets(
  assets: readonly Asset[],
  columnFilters: Readonly<AssetTimelineColumnFilters>,
): Asset[] {
  return assets.filter((asset) =>
    Object.entries(columnFilters).every(([field, filter]) => {
      if (!filter) return true;
      if (Array.isArray(filter)) {
        if (field === "status" || field === "warehouse" || field === "division") {
          return matchesExactFilterSelection(asset[field as AssetTimelineFilterField], filter);
        }
        return matchesMultiValueTextFilter(asset[field as AssetTimelineFilterField], toMultiValueTextFilter(filter));
      }
      return matchesFilter(asset[field as AssetTimelineFilterField], filter as string);
    }),
  );
}

export type AssetTimelineEventClass =
  | "event-ringfence"
  | "event-onhire"
  | "event-reserved"
  | "event-service"
  | "event-repair"
  | "event-collection"
  | "event-transport"
  | "event-onhold"
  | "event-none"
  | "event-unknown";

export interface AssetTimelineRow {
  rowId: string;
  asset: Asset;
  event: AssetEvent | null;
  eventClass: AssetTimelineEventClass;
  eventLabel: string;
  laneIndex?: number;
  timelineRowIndex?: number;
}

export interface AssetTimelineDateRange {
  start: Date;
  end: Date;
}

const TIMELINE_MONTHS_PER_PERIOD = 3;
const TIMELINE_HISTORY_MONTHS = 12;

export function buildAssetTimelinePeriod(currentDate: Date): AssetTimelineDateRange {
  const start = new Date(
    currentDate.getFullYear(),
    currentDate.getMonth() - TIMELINE_HISTORY_MONTHS,
    currentDate.getDate(),
  );
  const end = new Date(currentDate.getFullYear(), currentDate.getMonth() + TIMELINE_MONTHS_PER_PERIOD, 0);
  return { start, end };
}

export function deriveAssetTimelineEventDivisions(selectedDivision: string, assets: readonly Asset[]): string {
  if (selectedDivision) {
    return selectedDivision;
  }

  return Array.from(
    new Set(assets.map((asset) => asset.division?.trim()).filter((division): division is string => Boolean(division))),
  ).join(";");
}

function normalizeAssetId(assetId: string | null | undefined): string {
  return (assetId ?? "").trim().toUpperCase();
}

export function normalizeAssetEventsByAssetId(
  source: Readonly<Record<string, readonly AssetEvent[] | null | undefined>>,
): Record<string, AssetEvent[]> {
  const result: Record<string, AssetEvent[]> = {};

  Object.entries(source).forEach(([key, events]) => {
    const normalizedKey = normalizeAssetId(key);
    if (!normalizedKey) {
      return;
    }

    const normalizedEvents = (events ?? [])
      .map((event) => ({
        ...event,
        assetId: normalizeAssetId(event.assetId || key),
      }))
      .sort((a, b) => {
        const aDate = a.startDate ? new Date(a.startDate).getTime() : 0;
        const bDate = b.startDate ? new Date(b.startDate).getTime() : 0;
        return aDate - bDate;
      });

    result[normalizedKey] = normalizedEvents;
  });

  return result;
}

export function buildAssetTimelineRows(
  assets: readonly Asset[],
  eventsByAssetId: Readonly<Record<string, readonly AssetEvent[] | undefined>>,
): AssetTimelineRow[] {
  const rows: AssetTimelineRow[] = [];
  let timelineRowOffset = 0;

  assets.forEach((asset) => {
    const events = [...(eventsByAssetId[normalizeAssetId(asset.id)] ?? [])].sort((a, b) =>
      (timelineDate(a.startDate) ?? "").localeCompare(timelineDate(b.startDate) ?? ""),
    );

    if (events.length === 0) {
      rows.push({
        rowId: `${asset.id}::none`,
        asset,
        event: null,
        eventClass: "event-none",
        eventLabel: "",
        laneIndex: 0,
        timelineRowIndex: timelineRowOffset,
      });
      timelineRowOffset += 1;
      return;
    }

    const occurrences = new Map<string, number>();
    const laneEndDates: Array<string | null> = [];
    events.forEach((event) => {
      const identity = assetEventIdentity(event);
      const occurrence = occurrences.get(identity) ?? 0;
      occurrences.set(identity, occurrence + 1);
      const eventClass = mapAssetEventCssClass(event.cssClass);
      const eventStart = timelineDate(event.startDate);
      const eventEnd = timelineDate(event.endDate);
      const effectiveEnd = eventStart && eventEnd && eventEnd >= eventStart ? eventEnd : eventStart;
      const reusableLane = laneEndDates.findIndex(
        (laneEnd) => eventStart != null && laneEnd != null && laneEnd < eventStart,
      );
      const laneIndex = reusableLane >= 0 ? reusableLane : laneEndDates.length;
      laneEndDates[laneIndex] = effectiveEnd;
      rows.push({
        rowId: `${asset.id}::${eventClass}::${encodeURIComponent(identity)}::${occurrence}`,
        asset,
        event,
        eventClass,
        eventLabel: formatAssetEventLabel(event.eventType, eventClass),
        laneIndex,
        timelineRowIndex: timelineRowOffset + laneIndex,
      });
    });
    timelineRowOffset += laneEndDates.length;
  });

  return rows;
}

export function filterAssetTimelineAssetsByDateRange(
  assets: readonly Asset[],
  eventsByAssetId: Readonly<Record<string, readonly AssetEvent[] | undefined>>,
  startDate: string,
  endDate: string,
): Asset[] {
  if (!startDate && !endDate) return [...assets];
  return assets.filter((asset) =>
    (eventsByAssetId[normalizeAssetId(asset.id)] ?? []).some((event) => {
      const eventStart = timelineDate(event.startDate);
      const eventEnd = timelineDate(event.endDate) ?? eventStart;
      return (
        eventStart != null &&
        eventEnd != null &&
        (!startDate || eventEnd >= startDate) &&
        (!endDate || eventStart <= endDate)
      );
    }),
  );
}

function mapAssetEventCssClass(cssClass: string | null): AssetTimelineEventClass {
  switch ((cssClass ?? "").toLowerCase()) {
    case "ringfence_event":
      return "event-ringfence";
    case "onhire_event":
      return "event-onhire";
    case "reserved_event":
      return "event-reserved";
    case "service_event":
      return "event-service";
    case "repair_event":
      return "event-repair";
    case "collection_event":
      return "event-collection";
    case "transport_event":
      return "event-transport";
    case "onhold_event":
      return "event-onhold";
    default:
      return "event-unknown";
  }
}

function formatAssetEventLabel(eventType: string | null, eventClass: AssetTimelineEventClass): string {
  switch ((eventType ?? "").toUpperCase()) {
    case "RINGFENCE":
      return "Ringfence";
    case "ONHIRE":
      return "On Hire";
    case "RESERVED":
      return "Reserved";
    case "SERVICE":
      return "Service";
    case "REPAIR":
      return "Repair";
    case "COLLECTION":
      return "Collection";
    case "TRANSPORT":
      return "Transport";
    case "ONHOLD":
      return "On Hold";
    default:
      if (eventClass === "event-none") {
        return "No Event";
      }

      return "Event";
  }
}

export function buildAssetTimelineTasks(rows: readonly AssetTimelineRow[], rangeStart: Date): GanttTask[] {
  const earliestDate = toAssetTimelineDateValue(rangeStart);

  return rows.map((row) => {
    const period = { start: timelineDate(row.event?.startDate), end: timelineDate(row.event?.endDate) };
    const geometry = timelineGeometry(period, earliestDate);
    const start = geometry.start > earliestDate ? geometry.start : earliestDate;
    const { end } = timelineGeometry({ start, end: period.end }, earliestDate);

    const title = row.event?.title?.trim() || row.asset.itemNumber || row.asset.id;
    const name = row.event ? `${row.eventLabel}: ${title}` : "";

    return {
      id: row.rowId,
      name,
      start,
      end,
      ...(row.event ? { period } : {}),
      progress: row.event ? 100 : 0,
      custom_class: row.eventClass,
      row: row.timelineRowIndex,
    };
  });
}

export function toAssetTimelineDateValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function formatAssetTimelineRange(startDate: Date, endDate: Date): string {
  const startMonth = startDate.toLocaleDateString(undefined, { month: "short" });
  const endMonth = endDate.toLocaleDateString(undefined, { month: "short" });

  if (startDate.getFullYear() === endDate.getFullYear()) {
    return `${startMonth} to ${endMonth} ${endDate.getFullYear()}`;
  }

  const startYear = startDate.getFullYear();
  const endYear = endDate.getFullYear();
  return `${startMonth} ${startYear} to ${endMonth} ${endYear}`;
}

function assetEventIdentity(event: AssetEvent): string {
  return JSON.stringify([event.startDate, event.endDate, event.eventType, event.title, event.cssClass]);
}

function matchesFilter(value: string | number | null | undefined, filter: string): boolean {
  return value != null && String(value).toLocaleLowerCase().includes(filter.toLocaleLowerCase());
}
