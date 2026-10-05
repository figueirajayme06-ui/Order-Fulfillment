import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  clearSessionPageState,
  getSessionPageStateKey,
  readSessionPageState,
  writeSessionPageState,
} from "./sessionPageState";

describe("session page state", () => {
  beforeEach(() => sessionStorage.clear());

  const decode = (value: unknown) =>
    typeof value === "object" && value !== null && "searchTerm" in value ? (value as { searchTerm: string }) : null;

  it("stores state separately by normalized user and page", () => {
    writeSessionPageState(" Planner@Example.com ", "agreements", { searchTerm: "A-1" });
    writeSessionPageState("planner@example.com", "assets", { searchTerm: "ASSET-1" });
    writeSessionPageState("planner@example.com", "ringfence", { searchTerm: "Priority" });
    writeSessionPageState("planner@example.com", "admin", { searchTerm: "User" });

    expect(readSessionPageState("PLANNER@example.com", "agreements", decode)).toEqual({ searchTerm: "A-1" });
    expect(readSessionPageState("planner@example.com", "assets", decode)).toEqual({ searchTerm: "ASSET-1" });
    expect(readSessionPageState("planner@example.com", "ringfence", decode)).toEqual({ searchTerm: "Priority" });
    expect(readSessionPageState("planner@example.com", "admin", decode)).toEqual({ searchTerm: "User" });
    expect(readSessionPageState("someone@example.com", "agreements", decode)).toBeNull();
  });

  it("ignores malformed or decoder-rejected state", () => {
    sessionStorage.setItem(getSessionPageStateKey("planner", "assets"), "not json");
    expect(readSessionPageState("planner", "assets", decode)).toBeNull();

    writeSessionPageState("planner", "assets", { unknown: true });
    expect(readSessionPageState("planner", "assets", decode)).toBeNull();
  });

  it("clears only the requested user/page entry", () => {
    writeSessionPageState("planner", "agreements", { searchTerm: "A" });
    writeSessionPageState("planner", "assets", { searchTerm: "B" });
    clearSessionPageState("planner", "agreements");
    expect(readSessionPageState("planner", "agreements", decode)).toBeNull();
    expect(readSessionPageState("planner", "assets", decode)).toEqual({ searchTerm: "B" });
  });

  it("does not throw when browser storage is unavailable", () => {
    const getItem = vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    expect(readSessionPageState("planner", "agreements", decode)).toBeNull();
    getItem.mockRestore();

    const setItem = vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    expect(() => writeSessionPageState("planner", "agreements", { searchTerm: "A" })).not.toThrow();
    setItem.mockRestore();
  });
});
