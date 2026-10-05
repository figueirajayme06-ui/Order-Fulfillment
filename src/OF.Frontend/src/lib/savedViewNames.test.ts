import { describe, expect, it } from "vitest";
import { isSavedViewNameTaken, normalizeSavedViewName } from "./savedViewNames";

describe("saved view names", () => {
  const views = [
    { id: "one", name: "Morning plan" },
    { id: "two", name: "Shared stock" },
  ];

  it("compares trimmed names without case differences", () => {
    expect(normalizeSavedViewName("  MORNING Plan ")).toBe("morning plan");
    expect(isSavedViewNameTaken(views, " morning PLAN ")).toBe(true);
  });

  it("can exclude the view being updated", () => {
    expect(isSavedViewNameTaken(views, "Morning plan", "one")).toBe(false);
    expect(isSavedViewNameTaken(views, "Shared stock", "one")).toBe(true);
  });
});
