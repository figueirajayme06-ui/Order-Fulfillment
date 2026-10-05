import { describe, expect, it } from "vitest";
import { isIsoCalendarDate, matchesDateColumnFilter } from "./dateColumnFilterModel";

const now = new Date(2026, 0, 1, 12);

describe("matchesDateColumnFilter", () => {
  it("uses exclusive calendar-date comparisons and excludes missing or invalid values", () => {
    expect(matchesDateColumnFilter("2026-01-02T23:59:00Z", { operator: "after", value: "2026-01-01" }, now)).toBe(true);
    expect(matchesDateColumnFilter("2026-01-01", { operator: "after", value: "2026-01-01" }, now)).toBe(false);
    expect(matchesDateColumnFilter(null, { operator: "notOn", value: "2026-01-01" }, now)).toBe(false);
    expect(matchesDateColumnFilter("not-a-date", { operator: "notOn", value: "2026-01-01" }, now)).toBe(false);
  });

  it("calculates relative month and year boundaries in browser-local time", () => {
    expect(matchesDateColumnFilter("2026-01-01", { operator: "thisMonth" }, now)).toBe(true);
    expect(matchesDateColumnFilter("2025-12-31", { operator: "lastMonth" }, now)).toBe(true);
    expect(matchesDateColumnFilter("2026-02-01", { operator: "nextMonth" }, now)).toBe(true);
    expect(matchesDateColumnFilter("2027-01-01", { operator: "nextYear" }, now)).toBe(true);
    expect(matchesDateColumnFilter("2026-12-31", { operator: "nextYear" }, now)).toBe(false);
  });

  it("validates true ISO calendar dates including leap years", () => {
    expect(isIsoCalendarDate("2024-02-29")).toBe(true);
    expect(isIsoCalendarDate("2026-02-29")).toBe(false);
  });
});
