export const DATE_FILTER_OPERATORS = [
  "on",
  "notOn",
  "after",
  "before",
  "today",
  "yesterday",
  "thisMonth",
  "lastMonth",
  "nextMonth",
  "thisYear",
  "lastYear",
  "nextYear",
] as const;

export type DateFilterOperator = (typeof DATE_FILTER_OPERATORS)[number];

export interface DateColumnFilterValue {
  operator: DateFilterOperator;
  value?: string;
}

export function isDateFilterOperator(value: unknown): value is DateFilterOperator {
  return typeof value === "string" && (DATE_FILTER_OPERATORS as readonly string[]).includes(value);
}

export function requiresDateValue(operator: DateFilterOperator): boolean {
  return operator === "on" || operator === "notOn" || operator === "after" || operator === "before";
}

export function isIsoCalendarDate(value: unknown): value is string {
  if (typeof value !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = toCalendarDate(value);
  return date !== null && toIsoDate(date) === value;
}

export function matchesDateColumnFilter(
  rowValue: string | null | undefined,
  filter: DateColumnFilterValue,
  now: Date = new Date(),
): boolean {
  const date = toCalendarDate(rowValue);
  if (!date) return false;

  if (requiresDateValue(filter.operator)) {
    if (!isIsoCalendarDate(filter.value)) return false;
    const comparison = toCalendarDate(filter.value)!;
    if (filter.operator === "on") return date.getTime() === comparison.getTime();
    if (filter.operator === "notOn") return date.getTime() !== comparison.getTime();
    return filter.operator === "after" ? date > comparison : date < comparison;
  }

  const today = startOfDay(now);
  if (filter.operator === "today") return date.getTime() === today.getTime();
  if (filter.operator === "yesterday") return date.getTime() === addDays(today, -1).getTime();

  const year = today.getFullYear();
  const month = today.getMonth();
  if (filter.operator === "thisMonth") return inRange(date, new Date(year, month, 1), new Date(year, month + 1, 1));
  if (filter.operator === "lastMonth") return inRange(date, new Date(year, month - 1, 1), new Date(year, month, 1));
  if (filter.operator === "nextMonth") return inRange(date, new Date(year, month + 1, 1), new Date(year, month + 2, 1));
  if (filter.operator === "thisYear") return inRange(date, new Date(year, 0, 1), new Date(year + 1, 0, 1));
  if (filter.operator === "lastYear") return inRange(date, new Date(year - 1, 0, 1), new Date(year, 0, 1));
  return inRange(date, new Date(year + 1, 0, 1), new Date(year + 2, 0, 1));
}

export function toIsoDate(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function toCalendarDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) return null;
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return Number.isNaN(date.getTime()) || toIsoDate(date) !== `${match[1]}-${match[2]}-${match[3]}` ? null : date;
}

function startOfDay(value: Date): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate());
}

function addDays(value: Date, days: number): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate() + days);
}

function inRange(value: Date, start: Date, end: Date): boolean {
  return value >= start && value < end;
}
