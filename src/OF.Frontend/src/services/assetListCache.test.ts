import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Asset } from "../types";
import { cacheAssetList, invalidateCachedAssetLists, readCachedAssetList } from "./assetListCache";

const query = { division: "110,120", excludeStatuses: "RemovedStock,Scrap,Sold" };
const assets: Asset[] = [{ id: "ASSET-1" } as Asset];

describe("assetListCache", () => {
  beforeEach(() => {
    invalidateCachedAssetLists();
    vi.useRealTimers();
  });

  it("returns a cached result only for the same user and query", () => {
    cacheAssetList("Planner@Example.com", query, assets);

    expect(readCachedAssetList("planner@example.com", query)).toBe(assets);
    expect(readCachedAssetList("another@example.com", query)).toBeNull();
    expect(readCachedAssetList("planner@example.com", { ...query, division: "130" })).toBeNull();
  });

  it("expires cached results after two minutes", () => {
    vi.useFakeTimers();
    cacheAssetList("planner@example.com", query, assets);
    vi.advanceTimersByTime(2 * 60 * 1000);

    expect(readCachedAssetList("planner@example.com", query)).toBeNull();
  });

  it("clears cached results after an invalidating mutation", () => {
    cacheAssetList("planner@example.com", query, assets);
    invalidateCachedAssetLists();

    expect(readCachedAssetList("planner@example.com", query)).toBeNull();
  });
});
