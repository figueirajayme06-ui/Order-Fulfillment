import { useEffect, useId, useRef } from "react";
import { useTranslation } from "react-i18next";
import { isSavedViewNameTaken, normalizeSavedViewName } from "../../../lib/savedViewNames";
import type { PersistedSavedView, SavedViewRecipient, SavedViewScope } from "../../../services/viewsService";
import { Button } from "../Button/Button";
import { SavedViewRecipientPicker } from "../SavedViewRecipientPicker/SavedViewRecipientPicker";
import styles from "./SavedViewControls.module.css";

export interface SavedViewFeedback {
  tone: "success" | "error";
  message: string;
}

export interface SavedViewControlsClassNames {
  bar?: string;
  select?: string;
  nameInput?: string;
}

export type SavedViewPendingAction = "save-new" | "save-changes" | "save-sharing" | "delete" | null;

export interface SavedViewControlsProps<TState> {
  savedViewsLabel: string;
  canDeleteSelectedView: boolean;
  canEditSelectedView: boolean;
  canManageSharedViews: boolean;
  canPersistViews: boolean;
  classNames?: SavedViewControlsClassNames;
  feedback: SavedViewFeedback | null;
  isDirty?: boolean;
  isSharingDirty?: boolean;
  pendingAction?: SavedViewPendingAction;
  name: string;
  recipientCandidates: readonly SavedViewRecipient[];
  recipientError: string | null;
  recipients: readonly SavedViewRecipient[];
  isRecipientLoading: boolean;
  isSelectedViewReceived: boolean;
  isSharingEditorOpen: boolean;
  scope: SavedViewScope;
  selectedViewId: string;
  views: readonly PersistedSavedView<TState>[];
  onDelete: () => void;
  onCancelSharingEdit: () => void;
  onEditSharing: () => void;
  onNameChange: (name: string) => void;
  onRecipientsChange: (recipients: SavedViewRecipient[]) => void;
  onRetryRecipients: () => void;
  onSaveSharing?: () => void;
  onSearchRecipients: (search: string) => void;
  onSaveNew: () => void;
  onScopeChange: (scope: SavedViewScope) => void;
  onSelectionChange: (viewId: string) => void;
  onUpdate: () => void;
}

export const SavedViewControls = <TState,>({
  savedViewsLabel,
  canDeleteSelectedView,
  canEditSelectedView,
  canManageSharedViews,
  canPersistViews,
  classNames,
  feedback,
  isDirty,
  isSharingDirty,
  pendingAction = null,
  name,
  recipientCandidates,
  recipientError,
  recipients,
  isRecipientLoading,
  isSelectedViewReceived,
  isSharingEditorOpen,
  scope,
  selectedViewId,
  views,
  onDelete,
  onCancelSharingEdit,
  onEditSharing,
  onNameChange,
  onRecipientsChange,
  onRetryRecipients,
  onSaveSharing,
  onSearchRecipients,
  onSaveNew,
  onScopeChange,
  onSelectionChange,
  onUpdate,
}: SavedViewControlsProps<TState>) => {
  const { t } = useTranslation();
  const editSharingRef = useRef<HTMLButtonElement>(null);
  const scopeRef = useRef<HTMLSelectElement>(null);
  const duplicateNameMessageId = useId();
  const returnFocusTarget = useRef<"edit" | "scope">("scope");
  const wasSharingEditorOpen = useRef(isSharingEditorOpen);
  const personalViews = views.filter((view) => view.scope === "personal" || (view.scope === "users" && view.isOwner));
  const receivedViews = views.filter((view) => view.scope === "users" && !view.isOwner);
  const divisionViews = views.filter((view) => view.scope === "division");
  const globalViews = views.filter((view) => view.scope === "global");
  const selectedView = views.find((view) => view.id === selectedViewId);
  const hasSelectedView = Boolean(selectedView);
  const isRenamingSelectedView = Boolean(
    selectedView && normalizeSavedViewName(name) !== normalizeSavedViewName(selectedView.name),
  );
  const isNameTakenForNewView = isSavedViewNameTaken(views, name);
  const isNameTakenForUpdate = Boolean(isRenamingSelectedView && isSavedViewNameTaken(views, name, selectedViewId));
  // A selected view naturally has a name that is already in the list. Keep
  // "Save as new" unavailable until it is renamed, but do not present that
  // expected state as a validation problem just from selecting the view.
  const showDuplicateNameMessage = isNameTakenForUpdate || (!hasSelectedView && isNameTakenForNewView);
  const isEditingExistingSharing = hasSelectedView && scope === "users" && canEditSelectedView && isSharingEditorOpen;
  const isBusy = pendingAction !== null;
  const canSaveChanges =
    canEditSelectedView && isDirty !== false && !isNameTakenForUpdate && !isRecipientLoading && !isBusy;
  const canSaveSharing = canEditSelectedView && isSharingDirty !== false && !isRecipientLoading && !isBusy;

  useEffect(() => {
    if (wasSharingEditorOpen.current && !isSharingEditorOpen) {
      (returnFocusTarget.current === "edit" ? editSharingRef.current : scopeRef.current)?.focus();
    }
    wasSharingEditorOpen.current = isSharingEditorOpen;
  }, [isSharingEditorOpen]);

  return (
    <>
      <div className={joinClassNames(styles.bar, classNames?.bar)}>
        <select
          className={joinClassNames(styles.select, classNames?.select)}
          aria-label={savedViewsLabel}
          value={selectedViewId}
          disabled={isBusy}
          onChange={(event) => onSelectionChange(event.target.value)}
        >
          <option value="">{savedViewsLabel}</option>
          <SavedViewOptions label={t("savedViews.personal")} views={personalViews} />
          <SavedViewOptions label={t("savedViews.sharedWithMe")} views={receivedViews} includeOwner />
          <SavedViewOptions label={t("savedViews.division")} views={divisionViews} />
          <SavedViewOptions label={t("savedViews.global")} views={globalViews} />
        </select>
        <input
          type="text"
          className={joinClassNames(styles.nameInput, classNames?.nameInput)}
          aria-label={t("savedViews.viewName")}
          placeholder={t("savedViews.viewNamePlaceholder")}
          value={name}
          aria-describedby={showDuplicateNameMessage ? duplicateNameMessageId : undefined}
          aria-invalid={isNameTakenForUpdate || (!hasSelectedView && isNameTakenForNewView) || undefined}
          disabled={!canPersistViews || isBusy || isEditingExistingSharing}
          onChange={(event) => onNameChange(event.target.value)}
        />
        <select
          ref={scopeRef}
          className={styles.scopeSelect}
          aria-label={t("savedViews.scope")}
          value={scope}
          disabled={!canPersistViews || isSelectedViewReceived || isBusy || isEditingExistingSharing}
          onChange={(event) => {
            if (event.target.value === "users") returnFocusTarget.current = "scope";
            onScopeChange(event.target.value as SavedViewScope);
          }}
        >
          <option value="personal">{t("savedViews.personal")}</option>
          <option value="users">{t("savedViews.specificUsers")}</option>
          <option value="division" disabled={!canManageSharedViews}>
            {t("savedViews.division")}
          </option>
          <option value="global" disabled={!canManageSharedViews}>
            {t("savedViews.global")}
          </option>
        </select>
        {hasSelectedView && scope === "users" && canEditSelectedView && !isSharingEditorOpen && (
          <div className={styles.sharingSummary}>
            <span>{t("savedViews.sharedWithCount", { count: recipients.length })}</span>
            <button
              ref={editSharingRef}
              type="button"
              className={styles.editSharingButton}
              onClick={() => {
                returnFocusTarget.current = "edit";
                onEditSharing();
              }}
              disabled={isBusy}
            >
              {t("savedViews.editSharing")}
            </button>
          </div>
        )}
        {isEditingExistingSharing ? (
          <Button
            label={pendingAction === "save-sharing" ? t("savedViews.savingSharing") : t("savedViews.saveSharing")}
            variant="primary"
            size="small"
            onClick={onSaveSharing ?? onUpdate}
            disabled={!canSaveSharing}
          />
        ) : (
          <>
            {hasSelectedView && !isSelectedViewReceived && (
              <Button
                label={pendingAction === "save-changes" ? t("common.saving") : t("savedViews.saveChanges")}
                variant="primary"
                size="small"
                onClick={onUpdate}
                disabled={!canSaveChanges}
              />
            )}
            <Button
              label={
                pendingAction === "save-new"
                  ? t("common.saving")
                  : t(isSelectedViewReceived ? "savedViews.saveCopy" : "savedViews.saveAsNew")
              }
              variant={hasSelectedView ? "secondary" : "primary"}
              size="small"
              onClick={onSaveNew}
              disabled={!canPersistViews || isNameTakenForNewView || isRecipientLoading || isBusy}
            />
          </>
        )}
        {hasSelectedView && isDirty && !isEditingExistingSharing && (
          <span className={styles.unsaved}>{t("savedViews.unsavedChanges")}</span>
        )}
        <Button
          label={pendingAction === "delete" ? t("common.deleting") : t("savedViews.delete")}
          variant="danger"
          size="small"
          onClick={onDelete}
          disabled={!canDeleteSelectedView || isBusy}
        />
      </div>
      {showDuplicateNameMessage && (
        <p id={duplicateNameMessageId} className={styles.error} role="status" aria-live="polite">
          {t("savedViews.duplicateName")}
        </p>
      )}
      {scope === "users" && canPersistViews && isSharingEditorOpen && !isSelectedViewReceived && (
        <SavedViewRecipientPicker
          candidates={recipientCandidates}
          disabled={!canPersistViews || isBusy}
          error={recipientError}
          showValidation={feedback?.tone === "error" && recipients.length === 0}
          isLoading={isRecipientLoading}
          selected={recipients}
          onCancel={onCancelSharingEdit}
          onChange={onRecipientsChange}
          onSearch={onSearchRecipients}
          onRetry={onRetryRecipients}
        />
      )}
      {feedback && (
        <p
          className={joinClassNames(feedback.tone === "error" ? styles.error : styles.success, styles.feedback)}
          role={feedback.tone === "error" ? "alert" : "status"}
          aria-live={feedback.tone === "success" ? "polite" : undefined}
        >
          {feedback.message}
        </p>
      )}
    </>
  );
};

interface SavedViewOptionsProps<TState> {
  includeOwner?: boolean;
  label: string;
  views: readonly PersistedSavedView<TState>[];
}

const SavedViewOptions = <TState,>({ includeOwner = false, label, views }: SavedViewOptionsProps<TState>) => {
  const { t } = useTranslation();
  if (views.length === 0) return null;
  return (
    <optgroup label={label}>
      {views.map((view) => (
        <option key={view.id} value={view.id}>
          {buildSavedViewOptionLabel(view, t, includeOwner)}
        </option>
      ))}
    </optgroup>
  );
};

function buildSavedViewOptionLabel<TState>(
  view: PersistedSavedView<TState>,
  t: (key: string, values?: Record<string, unknown>) => string,
  includeOwner: boolean,
): string {
  const name = view.isDefault ? t("savedViews.defaultOption", { name: view.name }) : view.name;
  if (view.scope === "users" && view.isOwner) return t("savedViews.ownedSharedOption", { name });
  return includeOwner ? t("savedViews.sharedOption", { name, owner: view.owner }) : name;
}

function joinClassNames(...values: Array<string | undefined>): string {
  return values.filter(Boolean).join(" ");
}
