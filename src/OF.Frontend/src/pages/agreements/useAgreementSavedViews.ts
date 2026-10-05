import { useSavedViews, type SavedViewOptions } from "../../hooks/useSavedViews";
import { parseAgreementsSavedViewState, type AgreementsSavedViewState } from "../../types/savedViews";

const configuration = {
  page: "agreements" as const,
  decode: parseAgreementsSavedViewState,
  selectionKey: "agreements.savedViewSelection.v3",
  legacySelectionKey: "orders.savedViewSelection.v2",
};

export type UseAgreementSavedViewsOptions = SavedViewOptions<AgreementsSavedViewState>;
export type AgreementSavedViewsController = ReturnType<typeof useAgreementSavedViews>;

export function useAgreementSavedViews(options: UseAgreementSavedViewsOptions) {
  return useSavedViews(options, configuration);
}
