export interface TimelinePeriod {
  start: string | null;
  end: string | null;
}

/** API planning dates are calendar dates, including when serialized with an offset. */
export function timelineDate(value: string | null | undefined): string | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})(?:T|$)/.exec(value ?? "");
  if (!match) return null;
  const [year, month, day] = match.slice(1).map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  return parsed.getUTCFullYear() === year && parsed.getUTCMonth() === month - 1 && parsed.getUTCDate() === day
    ? match[0].slice(0, 10)
    : null;
}

export function formatTimelineDate(value: string, language: string): string {
  const date = timelineDate(value);
  if (!date) return "";

  return new Intl.DateTimeFormat(language, { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" }).format(
    new Date(`${date}T00:00:00Z`),
  );
}

export function timelineGeometry(period: TimelinePeriod, fallback: string): { start: string; end: string } {
  const start = period.start ?? period.end ?? fallback;
  // Frappe renders a date-only end inclusively, so same-day periods must remain same-day.
  const sourceEnd = period.end && period.end >= start ? period.end : start;
  const limit = new Date(`${start}T00:00:00Z`);
  limit.setUTCDate(limit.getUTCDate() + 3649);
  const maximumEnd = limit.toISOString().slice(0, 10);
  return { start, end: sourceEnd > maximumEnd ? maximumEnd : sourceEnd };
}
