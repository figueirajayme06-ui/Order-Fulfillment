import type { Asset, AssetEvent } from "../../types";

const DAY_IN_MILLISECONDS = 24 * 60 * 60 * 1000;

export type AvailabilityCommitmentKind =
  "ringfence" | "onHire" | "reserved" | "service" | "repair" | "collection" | "transport" | "onHold" | "other";

export interface AvailabilityScheduleRange {
  start: Date;
  end: Date;
}

export interface AvailabilityCommitment {
  event: AssetEvent;
  start: Date;
  end: Date;
  clippedStart: Date;
  clippedEnd: Date;
  leftPercent: number;
  widthPercent: number;
  kind: AvailabilityCommitmentKind;
}

function parseDate(value?: string | null): Date | null {
  const dateValue = value?.trim().slice(0, 10);
  if (!dateValue || !/^\d{4}-\d{2}-\d{2}$/.test(dateValue)) return null;

  const date = new Date(`${dateValue}T00:00:00Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}

function normalizeAssetId(value?: string | null): string {
  return value?.trim().toUpperCase() ?? "";
}

function getCommitmentKind(cssClass?: string | null): AvailabilityCommitmentKind {
  switch (cssClass?.trim().toLowerCase()) {
    case "ringfence_event":
      return "ringfence";
    case "onhire_event":
      return "onHire";
    case "reserved_event":
      return "reserved";
    case "service_event":
      return "service";
    case "repair_event":
      return "repair";
    case "collection_event":
      return "collection";
    case "transport_event":
      return "transport";
    case "onhold_event":
      return "onHold";
    default:
      return "other";
  }
}

export function buildAvailabilityScheduleRange(
  startDate?: string | null,
  endDate?: string | null,
): AvailabilityScheduleRange | null {
  const start = parseDate(startDate);
  const end = parseDate(endDate);
  if (!start || !end || end < start) return null;
  return { start, end };
}

export function normalizeAvailabilityEventsByAssetId(
  source: Readonly<Record<string, readonly AssetEvent[] | null | undefined>>,
): Record<string, AssetEvent[]> {
  const normalized: Record<string, AssetEvent[]> = {};

  Object.entries(source).forEach(([key, events]) => {
    const assetId = normalizeAssetId(key);
    if (!assetId) return;

    normalized[assetId] = [...(events ?? [])].sort((left, right) => {
      return (parseDate(left.startDate)?.getTime() ?? 0) - (parseDate(right.startDate)?.getTime() ?? 0);
    });
  });

  return normalized;
}

export function getAvailabilityCommitments(
  assetId: string,
  eventsByAssetId: Readonly<Record<string, readonly AssetEvent[] | undefined>>,
  range: AvailabilityScheduleRange | null,
): AvailabilityCommitment[] {
  const events = eventsByAssetId[normalizeAssetId(assetId)] ?? [];

  return events.flatMap((event) => {
    const start = parseDate(event.startDate);
    const parsedEnd = parseDate(event.endDate);
    if (!start || !parsedEnd) return [];
    const end = parsedEnd < start ? start : parsedEnd;

    if (range && (end < range.start || start > range.end)) return [];

    const clippedStart = range && start < range.start ? range.start : start;
    const clippedEnd = range && end > range.end ? range.end : end;
    const rangeDuration = range ? range.end.getTime() - range.start.getTime() + DAY_IN_MILLISECONDS : 0;
    const leftPercent = range ? ((clippedStart.getTime() - range.start.getTime()) / rangeDuration) * 100 : 0;
    const widthPercent = range
      ? ((clippedEnd.getTime() - clippedStart.getTime() + DAY_IN_MILLISECONDS) / rangeDuration) * 100
      : 100;

    return [
      {
        event,
        start,
        end,
        clippedStart,
        clippedEnd,
        leftPercent,
        widthPercent,
        kind: getCommitmentKind(event.cssClass),
      },
    ];
  });
}

export function getAssetLocationLabel(
  asset: Asset,
  fallbackWarehouseName?: string,
  fallbackWarehouseCode?: string,
): string {
  const explicitLocation = asset.warehouseLocation?.trim();
  if (explicitLocation) return explicitLocation;

  const facility = asset.facility?.trim();
  const warehouse = asset.warehouse?.trim();
  const hierarchy = [facility, warehouse].filter((value, index, values) => value && values.indexOf(value) === index);
  if (hierarchy.length > 0) return hierarchy.join(" — ");

  return fallbackWarehouseName?.trim() || fallbackWarehouseCode?.trim() || "—";
}

export function formatAvailabilityDate(date: Date, locale: string): string {
  return new Intl.DateTimeFormat(locale, {
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  }).format(date);
}

export function getCommitmentLabel(event: AssetEvent, fallback: string): string {
  const title = event.title?.trim();
  if (title) return title;

  const eventType = event.eventType?.trim().toLowerCase();
  if (!eventType) return fallback;

  return eventType.replace(/(^|[_\s-])\p{L}/gu, (match) => match.toUpperCase()).replaceAll("_", " ");
}
