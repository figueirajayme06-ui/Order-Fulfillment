import { useSavedViews, type SavedViewOptions } from "../../hooks/useSavedViews";
import { parseAssetsSavedViewState, type AssetsSavedViewState } from "../../types/savedViews";

const configuration = {
  page: "assets" as const,
  decode: parseAssetsSavedViewState,
  selectionKey: "assets.savedViewSelection.v2",
};

export type UseAssetSavedViewsOptions = SavedViewOptions<AssetsSavedViewState>;
export type AssetSavedViewsController = ReturnType<typeof useAssetSavedViews>;

export function useAssetSavedViews(options: UseAssetSavedViewsOptions) {
  return useSavedViews(options, configuration);
}
