import type { RingfenceItemBatchResult } from "../services/ringfenceService";

export function getRingfenceItemBatchResult(error: unknown): RingfenceItemBatchResult | null {
  if (typeof error !== "object" || error === null) return null;
  const data = (error as { response?: { data?: unknown } }).response?.data;
  if (typeof data !== "object" || data === null) return null;

  const candidate = data as Partial<RingfenceItemBatchResult>;
  return Array.isArray(candidate.readyAssetIds) &&
    Array.isArray(candidate.alreadyAssignedAssetIds) &&
    Array.isArray(candidate.unavailableAssetIds) &&
    Array.isArray(candidate.addedAssetIds) &&
    Array.isArray(candidate.overlaps)
    ? {
        readyAssetIds: candidate.readyAssetIds,
        alreadyAssignedAssetIds: candidate.alreadyAssignedAssetIds,
        unavailableAssetIds: candidate.unavailableAssetIds,
        addedAssetIds: candidate.addedAssetIds,
        overlaps: candidate.overlaps,
        requiresOverlapAcknowledgement: Boolean(candidate.requiresOverlapAcknowledgement),
      }
    : null;
}
