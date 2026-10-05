import type { FC } from "react";
import { useTranslation } from "react-i18next";
import { SavedViewControls, type SavedViewControlsProps, type SavedViewFeedback } from "../../components/common";
import type { AssetsSavedViewState } from "../../types/savedViews";
import styles from "./AssetsPage.module.css";

export type AssetSavedViewFeedback = SavedViewFeedback;
export type AssetSavedViewControlsProps = Omit<
  SavedViewControlsProps<AssetsSavedViewState>,
  "savedViewsLabel" | "classNames"
>;

export const AssetSavedViewControls: FC<AssetSavedViewControlsProps> = (props) => {
  const { t } = useTranslation();
  return (
    <SavedViewControls
      {...props}
      savedViewsLabel={t("savedViews.savedAssetViews")}
      classNames={{
        bar: styles.savedViewsBar,
      }}
    />
  );
};
