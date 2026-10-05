import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type {
  SavedViewFeedback,
  SavedViewPendingAction,
} from "../components/common/SavedViewControls/SavedViewControls";
import { isSavedViewNameTaken, normalizeSavedViewName } from "../lib/savedViewNames";
import type { SavedViewDecoder, SavedViewPage } from "../lib/savedViewsStorage";
import {
  createSavedView,
  deleteSavedView,
  getSavedViewValidationMessage,
  listSavedViewRecipientCandidates,
  listSavedViews,
  updateSavedView,
  type PersistedSavedView,
  type SavedViewRecipient,
  type SavedViewScope,
  type SavedViewSource,
} from "../services/viewsService";

const SUCCESS_FEEDBACK_DURATION_MS = 2800;

export interface SavedViewOptions<TState> {
  applyState: (state: TState) => void;
  captureState: () => TState;
  canManageSharedViews: boolean;
  canPersistViews?: boolean;
}

interface SavedViewConfiguration<TState> {
  page: SavedViewPage;
  decode: SavedViewDecoder<TState>;
  selectionKey: string;
  legacySelectionKey?: string;
}

export function useSavedViews<TState>(
  { applyState, canManageSharedViews, canPersistViews = true, captureState }: SavedViewOptions<TState>,
  { page, decode, selectionKey, legacySelectionKey }: SavedViewConfiguration<TState>,
) {
  const { t } = useTranslation();
  const [isInitialized, setIsInitialized] = useState(false);
  const [views, setViews] = useState<PersistedSavedView<TState>[]>([]);
  const [selectedViewId, setSelectedViewId] = useState<string>(() => {
    return (
      readSavedViewSelection(selectionKey) || (legacySelectionKey ? readSavedViewSelection(legacySelectionKey) : "")
    );
  });
  const [name, setName] = useState("");
  const [scope, setScope] = useState<SavedViewScope>("personal");
  const [recipients, setRecipients] = useState<SavedViewRecipient[]>([]);
  const [recipientCandidates, setRecipientCandidates] = useState<SavedViewRecipient[]>([]);
  const [isRecipientLoading, setIsRecipientLoading] = useState(false);
  const [recipientError, setRecipientError] = useState<string | null>(null);
  const recipientSearchRef = useRef("");
  const recipientRequestIdRef = useRef(0);
  const [isSharingEditorOpen, setIsSharingEditorOpen] = useState(false);
  const [source, setSource] = useState<SavedViewSource>("api");
  const [feedback, setFeedback] = useState<SavedViewFeedback | null>(null);
  const [pendingAction, setPendingAction] = useState<SavedViewPendingAction>(null);
  const mutationPendingRef = useRef(false);
  const hasAutoAppliedDefaultViewRef = useRef(false);

  const selectedView = useMemo(() => {
    return views.find((view) => view.id === selectedViewId) ?? null;
  }, [selectedViewId, views]);

  const capturedState = captureState();
  const isSelectedViewReceived = Boolean(selectedView?.scope === "users" && !selectedView.isOwner);
  const isSharingDirty = Boolean(
    selectedView &&
    !isSelectedViewReceived &&
    (scope !== selectedView.scope || !haveSameRecipients(recipients, selectedView.recipients)),
  );
  const isDirty = Boolean(
    selectedView &&
    (name.trim() !== selectedView.name ||
      isSharingDirty ||
      JSON.stringify(capturedState) !== JSON.stringify(selectedView.state)),
  );

  const refreshViews = useCallback(
    async (preferredSelectionId?: string) => {
      const result = await listSavedViews(page, decode);
      setSource(result.source);
      setViews(result.views);

      const nextSelectedId = preferredSelectionId ?? selectedViewId;
      let selected = nextSelectedId ? (result.views.find((view) => view.id === nextSelectedId) ?? null) : null;

      if (!selected && !hasAutoAppliedDefaultViewRef.current) {
        const defaultView = result.views.find((view) => view.isDefault) ?? null;
        if (defaultView) {
          hasAutoAppliedDefaultViewRef.current = true;
          selected = defaultView;
          setFeedback({
            tone: "success",
            message: t("savedViews.defaultApplied", { name: defaultView.name }),
          });
        }
      }

      if (!selected) {
        setSelectedViewId("");
        setName("");
        setScope("personal");
        setRecipients([]);
        setIsSharingEditorOpen(false);
        return;
      }

      setSelectedViewId(selected.id);
      setName(selected.name);
      const isReceived = selected.scope === "users" && !selected.isOwner;
      setScope(isReceived ? "personal" : selected.scope);
      setRecipients(isReceived ? [] : selected.recipients);
      setIsSharingEditorOpen(false);
      applyState(selected.state);
    },
    [applyState, selectedViewId, page, decode, t],
  );

  useEffect(() => {
    let active = true;
    void refreshViews()
      .catch(() => {
        if (active) setFeedback({ tone: "error", message: t("common.error") });
      })
      .finally(() => {
        if (active) setIsInitialized(true);
      });
    return () => {
      active = false;
    };
  }, [refreshViews, t]);

  useEffect(() => {
    writeSavedViewSelection(selectionKey, selectedViewId);
    if (legacySelectionKey) writeSavedViewSelection(legacySelectionKey, "");
  }, [selectionKey, legacySelectionKey, selectedViewId]);

  useEffect(() => {
    if (feedback?.tone !== "success") {
      return;
    }

    const timeout = window.setTimeout(() => {
      setFeedback(null);
    }, SUCCESS_FEEDBACK_DURATION_MS);

    return () => {
      window.clearTimeout(timeout);
    };
  }, [feedback]);

  const loadRecipientCandidates = useCallback(
    async (search: string) => {
      if (!canPersistViews) return;
      const normalizedSearch = search.trim();
      recipientSearchRef.current = normalizedSearch;
      const requestId = ++recipientRequestIdRef.current;
      if (!normalizedSearch) {
        setRecipientCandidates([]);
        setRecipientError(null);
        setIsRecipientLoading(false);
        return;
      }
      setIsRecipientLoading(true);
      setRecipientError(null);
      try {
        const candidates = await listSavedViewRecipientCandidates(normalizedSearch);
        if (recipientRequestIdRef.current === requestId) setRecipientCandidates(candidates);
      } catch {
        if (recipientRequestIdRef.current === requestId) setRecipientError(t("savedViews.recipientLoadError"));
      } finally {
        if (recipientRequestIdRef.current === requestId) setIsRecipientLoading(false);
      }
    },
    [canPersistViews, t],
  );

  const selectView = useCallback(
    (viewId: string) => {
      if (mutationPendingRef.current || viewId === selectedViewId) {
        return;
      }

      if (isDirty && !window.confirm(t("savedViews.confirmDiscard"))) {
        return;
      }

      setFeedback(null);
      setSelectedViewId(viewId);
      setIsSharingEditorOpen(false);
      void loadRecipientCandidates("");

      if (!viewId) {
        setName("");
        setScope("personal");
        setRecipients([]);
        return;
      }

      const nextView = views.find((view) => view.id === viewId);
      if (!nextView) {
        return;
      }

      setName(nextView.name);
      const isReceived = nextView.scope === "users" && !nextView.isOwner;
      setScope(isReceived ? "personal" : nextView.scope);
      setRecipients(isReceived ? [] : nextView.recipients);
      applyState(nextView.state);
    },
    [applyState, isDirty, loadRecipientCandidates, selectedViewId, t, views],
  );

  const changeScope = useCallback(
    (nextScope: SavedViewScope) => {
      setScope(nextScope);
      setIsSharingEditorOpen(nextScope === "users");
      if (nextScope !== "users") {
        setRecipients([]);
        void loadRecipientCandidates("");
      }
    },
    [loadRecipientCandidates],
  );

  const editSharing = useCallback(() => {
    if (selectedView?.canEdit && selectedView.scope === "users") {
      setScope("users");
      setRecipients(selectedView.recipients);
      setIsSharingEditorOpen(true);
    }
  }, [selectedView]);

  const cancelSharingEdit = useCallback(() => {
    setIsSharingEditorOpen(false);
    void loadRecipientCandidates("");
    if (selectedView) {
      setScope(selectedView.scope);
      setRecipients(selectedView.recipients);
    } else {
      setScope("personal");
      setRecipients([]);
    }
  }, [loadRecipientCandidates, selectedView]);

  const saveNew = useCallback(async () => {
    if (mutationPendingRef.current) return;
    mutationPendingRef.current = true;
    setPendingAction("save-new");
    setFeedback(null);

    try {
      if (!canPersistViews) {
        setFeedback({ tone: "error", message: t("savedViews.readOnlySave") });
        return;
      }

      if (isSavedViewNameTaken(views, name)) {
        setFeedback({ tone: "error", message: t("savedViews.duplicateName") });
        return;
      }

      const isReceivedView = selectedView?.scope === "users" && !selectedView.isOwner;
      const createScope = isReceivedView ? "personal" : scope;
      const createRecipients = isReceivedView ? [] : recipients;

      if ((createScope === "division" || createScope === "global") && !canManageSharedViews) {
        setFeedback({ tone: "error", message: t("savedViews.adminScopeRequired") });
        return;
      }

      if (createScope === "users" && createRecipients.length === 0) {
        setFeedback({ tone: "error", message: t("savedViews.recipientRequired") });
        return;
      }

      const createdView = await createSavedView(
        source,
        page,
        name,
        createScope,
        capturedState,
        decode,
        createRecipients.map((recipient) => recipient.loginName),
      );

      if (!createdView.view) {
        setFeedback({ tone: "error", message: t("savedViews.nameRequired") });
        return;
      }

      setSource(createdView.source);
      await refreshViews(createdView.view.id);
      setFeedback({
        tone: "success",
        message: t("savedViews.savedNew", { name: createdView.view.name }),
      });
    } catch (error) {
      setFeedback({
        tone: "error",
        message: getSavedViewValidationMessage(error) ?? t("savedViews.saveError"),
      });
    } finally {
      mutationPendingRef.current = false;
      setPendingAction(null);
    }
  }, [
    page,
    decode,
    canManageSharedViews,
    canPersistViews,
    capturedState,
    name,
    recipients,
    refreshViews,
    scope,
    selectedView,
    source,
    t,
    views,
  ]);

  const updateSelected = useCallback(async () => {
    if (mutationPendingRef.current) return;
    mutationPendingRef.current = true;
    setPendingAction("save-changes");
    setFeedback(null);

    try {
      if (!canPersistViews) {
        setFeedback({ tone: "error", message: t("savedViews.readOnlyUpdate") });
        return;
      }

      if (!selectedViewId) {
        setFeedback({ tone: "error", message: t("savedViews.selectToUpdate") });
        return;
      }

      if (!selectedView?.canEdit) {
        setFeedback({ tone: "error", message: t("savedViews.updateDenied") });
        return;
      }

      if (
        normalizeSavedViewName(name) !== normalizeSavedViewName(selectedView.name) &&
        isSavedViewNameTaken(views, name, selectedViewId)
      ) {
        setFeedback({ tone: "error", message: t("savedViews.duplicateName") });
        return;
      }

      if ((scope === "division" || scope === "global") && !canManageSharedViews) {
        setFeedback({ tone: "error", message: t("savedViews.adminScopeRequired") });
        return;
      }

      if (scope === "users" && recipients.length === 0) {
        setFeedback({ tone: "error", message: t("savedViews.recipientRequired") });
        return;
      }

      const updatedView = await updateSavedView(
        source,
        page,
        selectedViewId,
        name,
        scope,
        capturedState,
        decode,
        recipients.map((recipient) => recipient.loginName),
      );

      if (!updatedView.view) {
        setFeedback({ tone: "error", message: t("savedViews.updateError") });
        return;
      }

      setSource(updatedView.source);
      await refreshViews(updatedView.view.id);
      setFeedback({
        tone: "success",
        message: t("savedViews.updated", { name: updatedView.view.name }),
      });
    } catch (error) {
      setFeedback({ tone: "error", message: getSavedViewValidationMessage(error) ?? t("savedViews.updateError") });
    } finally {
      mutationPendingRef.current = false;
      setPendingAction(null);
    }
  }, [
    page,
    decode,
    canManageSharedViews,
    canPersistViews,
    capturedState,
    name,
    refreshViews,
    recipients,
    scope,
    selectedView,
    selectedViewId,
    source,
    t,
    views,
  ]);

  const saveSharing = useCallback(async () => {
    if (mutationPendingRef.current) return;
    mutationPendingRef.current = true;
    setPendingAction("save-sharing");
    setFeedback(null);

    try {
      if (!canPersistViews || !selectedViewId || !selectedView?.canEdit) {
        setFeedback({ tone: "error", message: t("savedViews.updateDenied") });
        return;
      }

      if ((scope === "division" || scope === "global") && !canManageSharedViews) {
        setFeedback({ tone: "error", message: t("savedViews.adminScopeRequired") });
        return;
      }

      if (scope === "users" && recipients.length === 0) {
        setFeedback({ tone: "error", message: t("savedViews.recipientRequired") });
        return;
      }

      const updatedView = await updateSavedView(
        source,
        page,
        selectedViewId,
        selectedView.name,
        scope,
        selectedView.state,
        decode,
        recipients.map((recipient) => recipient.loginName),
      );

      const savedView = updatedView.view;
      if (!savedView) {
        setFeedback({ tone: "error", message: t("savedViews.sharingError") });
        return;
      }

      setSource(updatedView.source);
      setViews((currentViews) => currentViews.map((view) => (view.id === savedView.id ? savedView : view)));
      setScope(savedView.scope);
      setRecipients(savedView.recipients);
      setIsSharingEditorOpen(false);
      setFeedback({ tone: "success", message: t("savedViews.sharingUpdated", { name: savedView.name }) });
    } catch (error) {
      setFeedback({ tone: "error", message: getSavedViewValidationMessage(error) ?? t("savedViews.sharingError") });
    } finally {
      mutationPendingRef.current = false;
      setPendingAction(null);
    }
  }, [page, decode, canManageSharedViews, canPersistViews, recipients, scope, selectedView, selectedViewId, source, t]);

  const deleteSelected = useCallback(async () => {
    if (mutationPendingRef.current) return;
    setFeedback(null);

    if (!canPersistViews) {
      setFeedback({ tone: "error", message: t("savedViews.readOnlyDelete") });
      return;
    }

    if (!selectedViewId) {
      setFeedback({ tone: "error", message: t("savedViews.selectToDelete") });
      return;
    }

    if (!selectedView?.canDelete) {
      setFeedback({ tone: "error", message: t("savedViews.deleteDenied") });
      return;
    }

    const confirmationKey =
      selectedView.scope === "personal" ? "savedViews.confirmDelete" : "savedViews.confirmDeleteShared";
    if (!window.confirm(t(confirmationKey, { name: selectedView.name }))) {
      return;
    }

    mutationPendingRef.current = true;
    setPendingAction("delete");

    try {
      const deletedViewName = selectedView.name;
      const deleted = await deleteSavedView(source, page, selectedViewId);
      if (!deleted.deleted) {
        setFeedback({ tone: "error", message: t("savedViews.deleteError") });
        return;
      }

      setSource(deleted.source);
      setSelectedViewId("");
      setName("");
      setScope("personal");
      setRecipients([]);
      await refreshViews();
      setFeedback({
        tone: "success",
        message: t("savedViews.deleted", { name: deletedViewName }),
      });
    } catch {
      setFeedback({ tone: "error", message: t("savedViews.deleteError") });
    } finally {
      mutationPendingRef.current = false;
      setPendingAction(null);
    }
  }, [page, canPersistViews, refreshViews, selectedView, selectedViewId, source, t]);

  const searchRecipients = useCallback(
    (search: string) => {
      void loadRecipientCandidates(search);
    },
    [loadRecipientCandidates],
  );

  const retryRecipients = useCallback(() => {
    void loadRecipientCandidates(recipientSearchRef.current);
  }, [loadRecipientCandidates]);

  return {
    isInitialized,
    canDeleteSelectedView: Boolean(canPersistViews && selectedViewId && selectedView?.canDelete),
    canEditSelectedView: Boolean(canPersistViews && selectedViewId && selectedView?.canEdit),
    feedback,
    name,
    recipientCandidates,
    recipientError,
    recipients,
    isRecipientLoading,
    isDirty,
    isSharingEditorOpen,
    isSharingDirty,
    isSelectedViewReceived,
    pendingAction,
    scope,
    selectedViewId,
    views,
    deleteSelected,
    cancelSharingEdit,
    editSharing,
    searchRecipients,
    saveNew,
    saveSharing,
    selectView,
    setName,
    setRecipients,
    setScope: changeScope,
    retryRecipients,
    updateSelected,
  };
}

function haveSameRecipients(left: readonly SavedViewRecipient[], right: readonly SavedViewRecipient[]): boolean {
  const normalize = (recipients: readonly SavedViewRecipient[]) =>
    recipients.map((recipient) => recipient.loginName.trim().toLowerCase()).sort();
  return JSON.stringify(normalize(left)) === JSON.stringify(normalize(right));
}

function readSavedViewSelection(storageKey: string): string {
  if (typeof window === "undefined") {
    return "";
  }

  try {
    return window.localStorage.getItem(storageKey) ?? "";
  } catch {
    return "";
  }
}

function writeSavedViewSelection(storageKey: string, viewId: string): void {
  if (typeof window === "undefined") {
    return;
  }

  try {
    if (viewId) {
      window.localStorage.setItem(storageKey, viewId);
    } else {
      window.localStorage.removeItem(storageKey);
    }
  } catch {
    // Keep silent so saved views continue to work even when storage is unavailable.
  }
}
