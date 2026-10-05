import { describe, expect, it } from "vitest";
import {
  getTimelineDateAtPosition,
  getTimelinePositionForDate,
  getVisibleTimelineLabelStart,
} from "./timelineViewport";

const DAY = 24 * 60 * 60 * 1000;

describe("timeline viewport positioning", () => {
  it("preserves the same visible date when the timescale column width changes", () => {
    const dailyDates = [new Date(2026, 7, 1), new Date(2026, 7, 2), new Date(2026, 7, 3), new Date(2026, 7, 4)];
    const weeklyDates = [new Date(2026, 7, 1), new Date(2026, 7, 8), new Date(2026, 7, 15)];
    const visibleDate = getTimelineDateAtPosition(dailyDates, 40, 100);

    expect(visibleDate).toBe(new Date(2026, 7, 3).getTime() + DAY / 2);
    expect(getTimelinePositionForDate(weeklyDates, 280, visibleDate!)).toBe(100);
  });

  it("interpolates variable date intervals and clamps dates outside the timeline", () => {
    const dates = [new Date(2026, 0, 1), new Date(2026, 1, 1), new Date(2026, 2, 1)];
    const halfwayThroughJanuary = new Date(2026, 0, 1).getTime() + (31 * DAY) / 2;

    expect(getTimelinePositionForDate(dates, 120, halfwayThroughJanuary)).toBe(60);
    expect(getTimelinePositionForDate(dates, 120, new Date(2025, 0, 1).getTime())).toBe(0);
    expect(getTimelinePositionForDate(dates, 120, new Date(2027, 0, 1).getTime())).toBe(240);
  });

  it("returns null when timeline measurements are unavailable", () => {
    expect(getTimelineDateAtPosition([], 40, 100)).toBeNull();
    expect(getTimelineDateAtPosition([new Date()], 0, 100)).toBeNull();
    expect(getTimelinePositionForDate(undefined, 40, Date.now())).toBeNull();
  });

  it("centres labels in long visible bar sections and pins labels beside narrow edge slivers", () => {
    expect(getVisibleTimelineLabelStart(100, 10_000, 200, 440, 900)).toBe(570);
    expect(getVisibleTimelineLabelStart(880, 50, 200, 440, 900)).toBe(700);
    expect(getVisibleTimelineLabelStart(100, 100, 80, 440, 900)).toBeNull();
  });
});
