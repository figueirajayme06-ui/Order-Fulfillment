import { describe, expect, it } from "vitest";
import {
  applyMultiValueTextFilterDrafts,
  matchesMultiValueTextFilter,
  parseMultiValueTextFilter,
  toMultiValueTextFilter,
} from "./multiValueTextFilterModel";

describe("multi-value text filters", () => {
  it("parses comma, semicolon, and newline separated values and removes case-insensitive duplicates", () => {
    expect(parseMultiValueTextFilter(" ASSET-101, asset-204;asset-101\nASSET-319 ").values).toEqual([
      "ASSET-101",
      "asset-204",
      "ASSET-319",
    ]);
  });

  it("keeps the first 100 unique values and reports overflow", () => {
    const result = parseMultiValueTextFilter(Array.from({ length: 101 }, (_, index) => `A-${index}`).join(","));
    expect(result.values).toHaveLength(100);
    expect(result.exceededLimit).toBe(true);
  });

  it("migrates a legacy string to a one-value filter", () => {
    expect(toMultiValueTextFilter("  A-100  ")).toEqual(["A-100"]);
  });

  it("combines an active draft with committed values without changing the committed values", () => {
    const filters = { id: ["ZAD"] };
    expect(applyMultiValueTextFilterDrafts(filters, { id: "YCK" })).toEqual({ id: ["ZAD", "YCK"] });
    expect(filters).toEqual({ id: ["ZAD"] });
  });

  it("matches partial values case-insensitively with OR semantics", () => {
    expect(matchesMultiValueTextFilter("ZAD008160-0", ["zad", "yck"])).toBe(true);
    expect(matchesMultiValueTextFilter("YCKB001", ["zad", "yck"])).toBe(true);
    expect(matchesMultiValueTextFilter("ASSET-1010", ["asset-101", "asset-204"])).toBe(true);
    expect(matchesMultiValueTextFilter("North customer", ["south", "customer"])).toBe(true);
    expect(matchesMultiValueTextFilter("Unrelated", ["zad", "yck"])).toBe(false);
  });
});
