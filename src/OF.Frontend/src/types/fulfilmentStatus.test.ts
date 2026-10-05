import { describe, expect, it } from "vitest";
import { ApiFulfilmentStatus, isFulfilmentStatusFilterValue, mapFulfilmentStatus } from "./fulfilmentStatus";

describe("mapFulfilmentStatus", () => {
  it.each([
    [ApiFulfilmentStatus.Unfulfilled, "unfulfilled"],
    [ApiFulfilmentStatus.PartiallyFulfilled, "partial"],
    [ApiFulfilmentStatus.FullyFulfilled, "fulfilled"],
  ] as const)("maps raw API code %s to %s", (status, expected) => {
    expect(mapFulfilmentStatus(status)).toBe(expected);
  });

  it("does not give unsupported API codes a user-facing fulfilment status", () => {
    expect(mapFulfilmentStatus(ApiFulfilmentStatus.Overfulfilled)).toBe("unknown");
    expect(mapFulfilmentStatus(4)).toBe("unknown");
  });

  it("accepts only canonical raw filter values", () => {
    expect(isFulfilmentStatusFilterValue("0")).toBe(true);
    expect(isFulfilmentStatusFilterValue("3")).toBe(true);
    expect(isFulfilmentStatusFilterValue("2")).toBe(false);
  });
});
