import { useCallback, useEffect, useState, type Dispatch, type SetStateAction } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { getRingfenceItemBatchResult } from "../../lib/ringfenceBatchResult";
import { invalidateCachedAssetLists } from "../../services/assetListCache";
import {
  addRingfenceItems,
  fetchRingfences,
  preflightRingfenceItems,
  type RingfenceListItem,
  type RingfenceOverlap,
} from "../../services/ringfenceService";

export interface RingfenceOverlapConfirmation {
  ringfenceId: number;
  assetIds: string[];
  overlaps: RingfenceOverlap[];
}

const MAX_RINGFENCE_BATCH_ASSETS = 250;

interface AssetRingfenceOptions {
  contextualRingfenceId: number | null;
  contextualReturnPath: string | null;
  selectedAssets: Set<string>;
  setSelectedAssets: Dispatch<SetStateAction<Set<string>>>;
}

export function useAssetRingfence({
  contextualRingfenceId,
  contextualReturnPath,
  selectedAssets,
  setSelectedAssets,
}: AssetRingfenceOptions) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [ringfences, setRingfences] = useState<RingfenceListItem[]>([]);
  const [selectedRingfence, setSelectedRingfence] = useState<number | "">("");
  const [isAddingToRingfence, setIsAddingToRingfence] = useState(false);
  const [ringfenceFeedback, setRingfenceFeedback] = useState<{
    variant: "success" | "error" | "info";
    message: string;
  } | null>(null);
  const [overlapConfirmation, setOverlapConfirmation] = useState<RingfenceOverlapConfirmation | null>(null);

  useEffect(() => {
    let isCurrent = true;

    fetchRingfences()
      .then((data) => {
        if (!isCurrent) {
          return;
        }

        setRingfences(data);

        const contextualTargetExists =
          contextualRingfenceId !== null && data.some((ringfence) => ringfence.id === contextualRingfenceId);
        if (contextualTargetExists && contextualRingfenceId !== null) {
          setSelectedRingfence(contextualRingfenceId);
        } else {
          setSelectedRingfence((current) =>
            current !== "" && data.some((ringfence) => ringfence.id === current) ? current : "",
          );
          if (contextualRingfenceId !== null) {
            setRingfenceFeedback({ variant: "info", message: t("assets.ringfence.targetUnavailable") });
          }
        }
      })
      .catch(() => {
        if (!isCurrent) return;
        setRingfences([]);
        setSelectedRingfence("");
        setRingfenceFeedback({ variant: "error", message: t("assets.ringfence.targetsLoadError") });
      });

    return () => {
      isCurrent = false;
    };
  }, [contextualRingfenceId, t]);

  const applyRingfenceBatch = useCallback(
    async (ringfenceId: number, assetIds: string[], acknowledgeOverlaps = false) => {
      const result = await addRingfenceItems(ringfenceId, assetIds, acknowledgeOverlaps);

      if (result.addedAssetIds.length > 0) {
        invalidateCachedAssetLists();
        setSelectedAssets(new Set());
        setRingfenceFeedback({
          variant: "success",
          message:
            result.alreadyAssignedAssetIds.length > 0
              ? t("assets.ringfence.addedAssetsWithExisting", {
                  count: result.addedAssetIds.length,
                  existing: result.alreadyAssignedAssetIds.length,
                })
              : t("assets.ringfence.addedAssets", { count: result.addedAssetIds.length }),
        });

        if (contextualReturnPath) {
          navigate(contextualReturnPath);
        }
        return;
      }

      if (result.alreadyAssignedAssetIds.length > 0) {
        setRingfenceFeedback({
          variant: "info",
          message: t("assets.ringfence.alreadyAssignedAssets", { count: result.alreadyAssignedAssetIds.length }),
        });
        return;
      }

      setRingfenceFeedback({ variant: "info", message: t("assets.ringfence.noAssetsAdded") });
    },
    [contextualReturnPath, navigate, setSelectedAssets, t],
  );

  const handleAddToRingfence = useCallback(async () => {
    if (selectedRingfence === "" || selectedAssets.size === 0) return;
    const assetIds = Array.from(selectedAssets);
    if (assetIds.length > MAX_RINGFENCE_BATCH_ASSETS) {
      setRingfenceFeedback({
        variant: "error",
        message: t("assets.ringfence.maxAssets", { count: MAX_RINGFENCE_BATCH_ASSETS }),
      });
      return;
    }

    setIsAddingToRingfence(true);
    setRingfenceFeedback(null);
    try {
      const result = await preflightRingfenceItems(selectedRingfence, assetIds);
      if (result.unavailableAssetIds.length > 0) {
        setRingfenceFeedback({
          variant: "error",
          message: t("assets.ringfence.unavailableAssets", { assets: result.unavailableAssetIds.join(", ") }),
        });
        return;
      }
      if (result.readyAssetIds.length === 0) {
        setRingfenceFeedback({
          variant: "info",
          message: t("assets.ringfence.alreadyAssignedAssets", { count: result.alreadyAssignedAssetIds.length }),
        });
        return;
      }
      if (result.requiresOverlapAcknowledgement) {
        setOverlapConfirmation({
          ringfenceId: selectedRingfence,
          assetIds: result.readyAssetIds,
          overlaps: result.overlaps,
        });
        return;
      }

      await applyRingfenceBatch(selectedRingfence, result.readyAssetIds);
    } catch (error) {
      setRingfenceFeedback({
        variant: "error",
        message: getRingfenceBatchErrorMessage(error, t("assets.ringfence.addError")),
      });
    } finally {
      setIsAddingToRingfence(false);
    }
  }, [applyRingfenceBatch, selectedRingfence, selectedAssets, t]);

  const confirmOverlappingAssets = useCallback(async () => {
    const confirmation = overlapConfirmation;
    setOverlapConfirmation(null);
    if (!confirmation) return;

    setIsAddingToRingfence(true);
    try {
      await applyRingfenceBatch(confirmation.ringfenceId, confirmation.assetIds, true);
    } catch (error) {
      const batchResult = getRingfenceItemBatchResult(error);
      if (batchResult?.requiresOverlapAcknowledgement && batchResult.overlaps.length > 0) {
        setOverlapConfirmation({
          ringfenceId: confirmation.ringfenceId,
          assetIds: confirmation.assetIds,
          overlaps: batchResult.overlaps,
        });
      } else if (batchResult?.unavailableAssetIds.length) {
        setRingfenceFeedback({
          variant: "error",
          message: t("assets.ringfence.unavailableAssets", { assets: batchResult.unavailableAssetIds.join(", ") }),
        });
      } else {
        setRingfenceFeedback({
          variant: "error",
          message: getRingfenceBatchErrorMessage(error, t("assets.ringfence.addError")),
        });
      }
    } finally {
      setIsAddingToRingfence(false);
    }
  }, [applyRingfenceBatch, overlapConfirmation, t]);

  return {
    ringfences,
    selectedRingfence,
    setSelectedRingfence,
    isAddingToRingfence,
    ringfenceFeedback,
    setRingfenceFeedback,
    overlapConfirmation,
    setOverlapConfirmation,
    handleAddToRingfence,
    confirmOverlappingAssets,
  };
}

function getRingfenceBatchErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== "object" || error === null) return fallback;
  const candidate = error as {
    response?: { data?: { detail?: unknown; message?: unknown; title?: unknown } };
    message?: unknown;
  };
  if (typeof candidate.response?.data?.detail === "string") return candidate.response.data.detail;
  if (typeof candidate.response?.data?.message === "string") return candidate.response.data.message;
  if (typeof candidate.response?.data?.title === "string") return candidate.response.data.title;
  return typeof candidate.message === "string" ? candidate.message : fallback;
}
