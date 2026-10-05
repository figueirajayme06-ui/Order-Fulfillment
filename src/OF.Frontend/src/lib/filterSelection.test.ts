import { describe, expect, it } from "vitest";
import {
  haveSameFilterSelection,
  matchesExactFilterSelection,
  normalizeFilterSelection,
  parseFilterSelection,
  serializeFilterSelection,
} from "./filterSelection";

describe("filterSelection", () => {
  it("parses, trims, de-duplicates, and serializes selections", () => {
    expect(parseFilterSelection(" 02,01,02, ")).toEqual(["02", "01"]);
    expect(serializeFilterSelection([" Available ", "Repair", "available"])).toBe("Available,Repair");
  });

  it("normalizes known options in catalogue order", () => {
    expect(normalizeFilterSelection("agreement,unknown,quote", ["quote", "temporaryAgreement", "agreement"])).toBe(
      "quote,agreement",
    );
  });

  it("compares selections without depending on order and matches exact option values", () => {
    expect(haveSameFilterSelection("01,02", "02,01")).toBe(true);
    expect(matchesExactFilterSelection("Available", ["Repair", "available"])).toBe(true);
    expect(matchesExactFilterSelection("In Transit", ["Transit"])).toBe(false);
  });
});
