import type { FC } from "react";
import { useTranslation } from "react-i18next";
import { SavedViewControls, type SavedViewControlsProps, type SavedViewFeedback } from "../../components/common";
import type { AgreementsSavedViewState } from "../../types/savedViews";
import styles from "./AgreementsPage.module.css";

export type AgreementSavedViewFeedback = SavedViewFeedback;
export type AgreementSavedViewControlsProps = Omit<
  SavedViewControlsProps<AgreementsSavedViewState>,
  "savedViewsLabel" | "classNames"
>;

export const AgreementSavedViewControls: FC<AgreementSavedViewControlsProps> = (props) => {
  const { t } = useTranslation();
  return (
    <SavedViewControls
      {...props}
      savedViewsLabel={t("savedViews.savedAgreementViews")}
      classNames={{
        bar: styles.savedViewsBar,
        select: styles.savedViewsSelect,
        nameInput: styles.savedViewsNameInput,
      }}
    />
  );
};
