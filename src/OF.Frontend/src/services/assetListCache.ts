import type { Asset } from "../types";
import type { AssetFilterParams } from "./assetsService";

const CACHE_TTL_MS = 2 * 60 * 1000;
const MAX_CACHED_QUERIES = 2;

interface CachedAssetList {
  assets: Asset[];
  expiresAt: number;
}

// Asset-list responses are large, so this intentionally remains a small in-memory cache.
// It is scoped to the signed-in user and disappears on refresh or sign-out.
const cachedAssetLists = new Map<string, CachedAssetList>();

export function readCachedAssetList(userIdentifier: string | undefined, params: AssetFilterParams): Asset[] | null {
  const key = buildCacheKey(userIdentifier, params);
  if (!key) return null;

  const cached = cachedAssetLists.get(key);
  if (!cached) return null;

  if (cached.expiresAt <= Date.now()) {
    cachedAssetLists.delete(key);
    return null;
  }

  // Move the entry to the end so eviction retains the most recently used query.
  cachedAssetLists.delete(key);
  cachedAssetLists.set(key, cached);
  return cached.assets;
}

export function cacheAssetList(userIdentifier: string | undefined, params: AssetFilterParams, assets: Asset[]): void {
  const key = buildCacheKey(userIdentifier, params);
  if (!key) return;

  cachedAssetLists.delete(key);
  cachedAssetLists.set(key, { assets, expiresAt: Date.now() + CACHE_TTL_MS });

  while (cachedAssetLists.size > MAX_CACHED_QUERIES) {
    const oldestKey = cachedAssetLists.keys().next().value;
    if (!oldestKey) return;
    cachedAssetLists.delete(oldestKey);
  }
}

export function invalidateCachedAssetLists(): void {
  cachedAssetLists.clear();
}

function buildCacheKey(userIdentifier: string | undefined, params: AssetFilterParams): string | null {
  const normalizedUserIdentifier = userIdentifier?.trim().toLocaleLowerCase();
  if (!normalizedUserIdentifier) return null;

  const query = new URLSearchParams();
  for (const [name, value] of Object.entries(params).sort(([first], [second]) => first.localeCompare(second))) {
    if (value !== undefined) query.set(name, String(value));
  }

  return `${normalizedUserIdentifier}?${query.toString()}`;
}
