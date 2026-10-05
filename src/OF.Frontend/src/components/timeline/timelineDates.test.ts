import { describe, expect, it } from "vitest";
import { formatTimelineDate, timelineDate, timelineGeometry } from "./timelineDates";

describe("Reservation timeline dates", () => {
  it("preserves calendar dates across UTC offsets and daylight-saving boundaries", () => {
    expect(timelineDate("2026-03-29T00:30:00+14:00")).toBe("2026-03-29");
    expect(timelineDate("2026-10-25T23:30:00-12:00")).toBe("2026-10-25");
    expect(formatTimelineDate("2026-03-29", "en-GB")).toBe("29 Mar 2026");
    expect(formatTimelineDate("2026-03-29T00:00:00", "en-GB")).toBe("29 Mar 2026");
  });

  it("rejects invalid dates and leaves missing source values unknown", () => {
    expect(timelineDate("2026-02-30")).toBeNull();
    expect(timelineDate("not-a-date")).toBeNull();
    expect(timelineDate(null)).toBeNull();
    expect(timelineDate("2028-02-29")).toBe("2028-02-29");
  });

  it("keeps both inclusive endpoints, including one-day periods", () => {
    expect(timelineGeometry({ start: "2026-08-10", end: "2026-08-10" }, "2026-09-01")).toEqual({
      start: "2026-08-10",
      end: "2026-08-10",
    });
    expect(timelineGeometry({ start: "2026-08-10", end: "2026-08-11" }, "2026-09-01")).toEqual({
      start: "2026-08-10",
      end: "2026-08-11",
    });
  });

  it("keeps source dates unchanged while applying renderer limits and open-ended fallbacks", () => {
    const period = { start: "2026-08-10", end: "2099-12-31" };
    expect(timelineGeometry(period, "2026-09-01")).toEqual({ start: "2026-08-10", end: "2036-08-06" });
    expect(period.end).toBe("2099-12-31");
    expect(timelineGeometry({ start: "2026-08-10", end: null }, "2026-09-01")).toEqual({
      start: "2026-08-10",
      end: "2026-08-10",
    });
    expect(timelineGeometry({ start: null, end: null }, "2026-09-01")).toEqual({
      start: "2026-09-01",
      end: "2026-09-01",
    });
  });
});
