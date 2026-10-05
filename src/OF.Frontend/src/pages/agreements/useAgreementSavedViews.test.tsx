import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import {
  createSavedView,
  deleteSavedView,
  getSavedViewValidationMessage,
  listSavedViews,
  listSavedViewRecipientCandidates,
  updateSavedView,
  type PersistedSavedView,
  type SavedViewSource,
} from "../../services/viewsService";
import { parseAgreementsSavedViewState, type AgreementsSavedViewState } from "../../types/savedViews";
import { useAgreementSavedViews } from "./useAgreementSavedViews";

vi.mock("../../services/viewsService", () => ({
  createSavedView: vi.fn(),
  deleteSavedView: vi.fn(),
  getSavedViewValidationMessage: vi.fn(),
  listSavedViews: vi.fn(),
  listSavedViewRecipientCandidates: vi.fn(),
  updateSavedView: vi.fn(),
}));

const mockedCreateSavedView = vi.mocked(createSavedView);
const mockedDeleteSavedView = vi.mocked(deleteSavedView);
const mockedGetValidationMessage = vi.mocked(getSavedViewValidationMessage);
const mockedListSavedViews = vi.mocked(listSavedViews);
const mockedListRecipientCandidates = vi.mocked(listSavedViewRecipientCandidates);
const mockedUpdateSavedView = vi.mocked(updateSavedView);

const AGREEMENT_SELECTION_KEY = "agreements.savedViewSelection.v3";
const LEGACY_ORDER_SELECTION_KEY = "orders.savedViewSelection.v2";

function createState(overrides: Partial<AgreementsSavedViewState> = {}): AgreementsSavedViewState {
  return {
    stateVersion: 1,
    viewMode: "table",
    searchTerm: "",
    showHistorical: false,
    selectedDivision: "01",
    orderTypeFilter: "",
    statusFilter: "",
    advancedFilters: {},
    columnFilters: {},
    sortField: "onHireDate",
    sortDirection: "desc",
    ...overrides,
  };
}

function createView(
  id: string,
  overrides: Partial<PersistedSavedView<AgreementsSavedViewState>> = {},
): PersistedSavedView<AgreementsSavedViewState> {
  return {
    id,
    name: `View ${id}`,
    page: "agreements",
    scope: "personal",
    owner: "developer@example.com",
    isOwner: true,
    canEdit: true,
    canDelete: true,
    isDefault: false,
    source: "api",
    recipients: [],
    state: createState({ searchTerm: id }),
    ...overrides,
  };
}

function renderSavedViews(
  options: {
    applyState?: (state: AgreementsSavedViewState) => void;
    canManageSharedViews?: boolean;
    canPersistViews?: boolean;
    captureState?: () => AgreementsSavedViewState;
  } = {},
) {
  const applyState = options.applyState ?? vi.fn();
  const captureState = options.captureState ?? vi.fn(() => createState({ searchTerm: "captured" }));
  const rendered = renderHook(() =>
    useAgreementSavedViews({
      applyState,
      canManageSharedViews: options.canManageSharedViews ?? false,
      canPersistViews: options.canPersistViews,
      captureState,
    }),
  );

  return { ...rendered, applyState, captureState };
}

async function waitForInitialViews() {
  await waitFor(() => expect(mockedListSavedViews).toHaveBeenCalled());
  await act(async () => {
    await mockedListSavedViews.mock.results[0].value;
    await Promise.resolve();
  });
}

describe("useAgreementSavedViews", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.spyOn(window, "confirm").mockReturnValue(true);
    window.localStorage.clear();
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [] });
    mockedListRecipientCandidates.mockResolvedValue([]);
    mockedGetValidationMessage.mockReturnValue(null);
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it.each([
    ["prefers the current agreement key", "current-view", "legacy-view", "current-view"],
    ["migrates the legacy orders key", null, "legacy-view", "legacy-view"],
  ])("%s and applies the restored selection", async (_scenario, currentId, legacyId, expectedId) => {
    if (currentId) window.localStorage.setItem(AGREEMENT_SELECTION_KEY, currentId);
    window.localStorage.setItem(LEGACY_ORDER_SELECTION_KEY, legacyId);
    const restored = createView(expectedId);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [restored] });
    const applyState = vi.fn();

    const { result } = renderSavedViews({ applyState });

    await waitFor(() => expect(result.current.name).toBe(restored.name));
    expect(result.current.selectedViewId).toBe(expectedId);
    expect(result.current.name).toBe(restored.name);
    expect(result.current.scope).toBe(restored.scope);
    expect(applyState).toHaveBeenCalledWith(restored.state);
    expect(mockedListSavedViews).toHaveBeenCalledWith("agreements", parseAgreementsSavedViewState);
    expect(window.localStorage.getItem(AGREEMENT_SELECTION_KEY)).toBe(expectedId);
    expect(window.localStorage.getItem(LEGACY_ORDER_SELECTION_KEY)).toBeNull();
  });

  it("auto-applies a default only once and clears its success feedback after 2800ms", async () => {
    vi.useFakeTimers();
    const defaultView = createView("default", { name: "Default operations", isDefault: true });
    let resolveInitialList!: (value: { source: SavedViewSource; views: (typeof defaultView)[] }) => void;
    mockedListSavedViews
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveInitialList = resolve;
          }),
      )
      .mockResolvedValue({ source: "api", views: [defaultView] });
    const applyState = vi.fn();
    const { result } = renderSavedViews({ applyState });

    await act(async () => {
      resolveInitialList({ source: "api", views: [defaultView] });
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(result.current.selectedViewId).toBe(defaultView.id);
    expect(result.current.feedback).toEqual({
      tone: "success",
      message: "Default view applied: Default operations",
    });
    expect(applyState).toHaveBeenCalledWith(defaultView.state);

    act(() => vi.advanceTimersByTime(2799));
    expect(result.current.feedback).not.toBeNull();
    act(() => vi.advanceTimersByTime(1));
    expect(result.current.feedback).toBeNull();

    act(() => result.current.selectView(""));
    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(result.current.selectedViewId).toBe("");
    expect(result.current.name).toBe("");
    expect(result.current.scope).toBe("personal");
  });

  it("applies known selections and clears the controlled fields when selection is cleared", async () => {
    const divisionView = createView("division", { scope: "division", name: "Division view" });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [divisionView] });
    const applyState = vi.fn();
    const { result } = renderSavedViews({ applyState });
    await waitFor(() => expect(result.current.views).toEqual([divisionView]));

    act(() => result.current.selectView(divisionView.id));
    expect(result.current.selectedViewId).toBe(divisionView.id);
    expect(result.current.name).toBe("Division view");
    expect(result.current.scope).toBe("division");
    expect(applyState).toHaveBeenLastCalledWith(divisionView.state);
    await waitFor(() => expect(window.localStorage.getItem(AGREEMENT_SELECTION_KEY)).toBe(divisionView.id));

    act(() => result.current.selectView(""));
    expect(result.current.selectedViewId).toBe("");
    expect(result.current.name).toBe("");
    expect(result.current.scope).toBe("personal");
    await waitFor(() => expect(window.localStorage.getItem(AGREEMENT_SELECTION_KEY)).toBeNull());
  });

  it("creates through the loaded local source with the exact captured agreement state and decoder", async () => {
    mockedListSavedViews.mockResolvedValue({ source: "local", views: [] });
    const capturedState = createState({ searchTerm: "capture me", sortField: "offHireDate" });
    const captureState = vi.fn(() => capturedState);
    const { result } = renderSavedViews({ captureState });
    await waitForInitialViews();

    const createdView = createView("local-new", {
      name: "Local operations",
      source: "local",
      state: capturedState,
    });
    mockedCreateSavedView.mockResolvedValue({ source: "local", view: createdView });
    mockedListSavedViews.mockResolvedValue({ source: "local", views: [createdView] });
    act(() => result.current.setName("Local operations"));

    await act(async () => result.current.saveNew());

    expect(mockedCreateSavedView).toHaveBeenCalledWith(
      "local",
      "agreements",
      "Local operations",
      "personal",
      capturedState,
      parseAgreementsSavedViewState,
      [],
    );
    expect(captureState).toHaveBeenCalled();
    expect(result.current.selectedViewId).toBe(createdView.id);
    expect(result.current.feedback).toEqual({ tone: "success", message: "Saved new view: Local operations" });
  });

  it("does not create a view when its trimmed name already exists with different casing", async () => {
    const existing = createView("existing", { name: "New View" });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [existing] });
    const { result } = renderSavedViews();
    await waitFor(() => expect(result.current.views).toEqual([existing]));
    act(() => result.current.setName("  new VIEW "));

    await act(async () => result.current.saveNew());

    expect(mockedCreateSavedView).not.toHaveBeenCalled();
    expect(result.current.feedback).toEqual({
      tone: "error",
      message: "A saved view with this name already exists. Choose a different name.",
    });
  });

  it("blocks shared creation for non-admins before invoking the service", async () => {
    const { result } = renderSavedViews();
    await waitForInitialViews();
    act(() => {
      result.current.setName("Shared view");
      result.current.setScope("division");
    });

    await act(async () => result.current.saveNew());

    expect(mockedCreateSavedView).not.toHaveBeenCalled();
    expect(result.current.feedback).toEqual({
      tone: "error",
      message: "Only admins can create division or global views.",
    });
  });

  it("allows applying views but blocks every persisted-view mutation for read-only users", async () => {
    const selected = createView("read-only");
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected] });
    const applyState = vi.fn();
    const { result } = renderSavedViews({ applyState, canPersistViews: false });
    await waitFor(() => expect(result.current.views).toEqual([selected]));

    act(() => result.current.selectView(selected.id));
    expect(applyState).toHaveBeenCalledWith(selected.state);
    expect(result.current.canEditSelectedView).toBe(false);
    expect(result.current.canDeleteSelectedView).toBe(false);

    await act(async () => result.current.saveNew());
    await act(async () => result.current.updateSelected());
    await act(async () => result.current.deleteSelected());

    expect(mockedCreateSavedView).not.toHaveBeenCalled();
    expect(mockedUpdateSavedView).not.toHaveBeenCalled();
    expect(mockedDeleteSavedView).not.toHaveBeenCalled();
  });

  it("requires a selected view before update or delete", async () => {
    const { result } = renderSavedViews({ canManageSharedViews: true });
    await waitForInitialViews();

    await act(async () => result.current.updateSelected());
    expect(result.current.feedback).toEqual({
      tone: "error",
      message: "Select a saved view to update.",
    });
    expect(mockedUpdateSavedView).not.toHaveBeenCalled();

    await act(async () => result.current.deleteSelected());
    expect(result.current.feedback).toEqual({
      tone: "error",
      message: "Select a saved view to delete.",
    });
    expect(mockedDeleteSavedView).not.toHaveBeenCalled();
  });

  it("updates through the loaded API source with exact arguments when the selected view is editable", async () => {
    const selected = createView("42", { name: "Before update", canEdit: true });
    window.localStorage.setItem(AGREEMENT_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected] });
    const capturedState = createState({ searchTerm: "updated", sortDirection: "asc" });
    const captureState = vi.fn(() => capturedState);
    const { result } = renderSavedViews({ canManageSharedViews: true, captureState });
    await waitFor(() => expect(result.current.name).toBe(selected.name));

    const updated = createView(selected.id, { name: "After update", scope: "global", state: capturedState });
    mockedUpdateSavedView.mockResolvedValue({ source: "api", view: updated });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [updated] });
    act(() => {
      result.current.setName("After update");
      result.current.setScope("global");
    });

    await act(async () => result.current.updateSelected());

    expect(mockedUpdateSavedView).toHaveBeenCalledWith(
      "api",
      "agreements",
      selected.id,
      "After update",
      "global",
      capturedState,
      parseAgreementsSavedViewState,
      [],
    );
    expect(result.current.feedback).toEqual({ tone: "success", message: "Updated view: After update" });
    expect(result.current.name).toBe("After update");
    expect(result.current.scope).toBe("global");
  });

  it("enforces selected-view update and delete permissions before invoking services", async () => {
    const selected = createView("locked", { canEdit: false, canDelete: false });
    window.localStorage.setItem(AGREEMENT_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected] });
    const { result } = renderSavedViews({ canManageSharedViews: true });
    await waitFor(() => expect(result.current.name).toBe(selected.name));

    expect(result.current.canEditSelectedView).toBe(false);
    expect(result.current.canDeleteSelectedView).toBe(false);
    await act(async () => result.current.updateSelected());
    expect(result.current.feedback).toEqual({
      tone: "error",
      message: "You do not have permission to update this view.",
    });
    expect(mockedUpdateSavedView).not.toHaveBeenCalled();

    await act(async () => result.current.deleteSelected());
    expect(result.current.feedback).toEqual({
      tone: "error",
      message: "You do not have permission to delete this view.",
    });
    expect(mockedDeleteSavedView).not.toHaveBeenCalled();
  });

  it("blocks an editable view from moving to a shared scope for non-admins", async () => {
    const selected = createView("editable", { canEdit: true });
    window.localStorage.setItem(AGREEMENT_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected] });
    const { result } = renderSavedViews();
    await waitFor(() => expect(result.current.name).toBe(selected.name));
    act(() => result.current.setScope("division"));

    await act(async () => result.current.updateSelected());

    expect(mockedUpdateSavedView).not.toHaveBeenCalled();
    expect(result.current.feedback).toEqual({
      tone: "error",
      message: "Only admins can create division or global views.",
    });
  });

  it("deletes through the loaded local source and resets selection before refreshing", async () => {
    const selected = createView("local-delete", { name: "Local delete", source: "local", canDelete: true });
    window.localStorage.setItem(AGREEMENT_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "local", views: [selected] });
    const { result } = renderSavedViews();
    await waitFor(() => expect(result.current.canDeleteSelectedView).toBe(true));

    mockedDeleteSavedView.mockResolvedValue({ source: "local", deleted: true });
    mockedListSavedViews.mockResolvedValue({ source: "local", views: [] });
    await act(async () => result.current.deleteSelected());

    expect(mockedDeleteSavedView).toHaveBeenCalledWith("local", "agreements", selected.id);
    expect(result.current.selectedViewId).toBe("");
    expect(result.current.name).toBe("");
    expect(result.current.scope).toBe("personal");
    expect(result.current.feedback).toEqual({ tone: "success", message: "Deleted view: Local delete" });
    expect(window.localStorage.getItem(AGREEMENT_SELECTION_KEY)).toBeNull();
  });

  it("loads recipients and lets a non-admin create a multi-user share", async () => {
    const candidates = [
      { loginName: "alex@aggreko.com", fullName: "Alex Smith" },
      { loginName: "sam@aggreko.com", fullName: "Sam Jones" },
    ];
    mockedListRecipientCandidates.mockResolvedValue(candidates);
    const { result } = renderSavedViews();
    await waitForInitialViews();
    act(() => result.current.setScope("users"));
    expect(result.current.isSharingEditorOpen).toBe(true);
    expect(mockedListRecipientCandidates).not.toHaveBeenCalled();
    act(() => result.current.searchRecipients("alex"));
    await waitFor(() => expect(result.current.recipientCandidates).toEqual(candidates));
    expect(mockedListRecipientCandidates).toHaveBeenCalledWith("alex");
    act(() => {
      result.current.setName("Named team");
      result.current.setRecipients(candidates);
    });
    const created = createView("72", { name: "Named team", scope: "users", recipients: candidates });
    mockedCreateSavedView.mockResolvedValue({ source: "api", view: created });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [created] });

    await act(async () => result.current.saveNew());

    expect(mockedCreateSavedView).toHaveBeenCalledWith(
      "api",
      "agreements",
      "Named team",
      "users",
      expect.any(Object),
      parseAgreementsSavedViewState,
      ["alex@aggreko.com", "sam@aggreko.com"],
    );
  });

  it("retains the user-share draft and shows safe server validation when saving fails", async () => {
    const recipient = { loginName: "removed@aggreko.com", fullName: "Removed User" };
    const { result } = renderSavedViews();
    await waitForInitialViews();
    act(() => {
      result.current.setName("Keep this draft");
      result.current.setScope("users");
      result.current.setRecipients([recipient]);
    });
    const failure = new Error("bad request");
    mockedCreateSavedView.mockRejectedValue(failure);
    mockedGetValidationMessage.mockReturnValue("One or more recipients are no longer available.");

    await act(async () => result.current.saveNew());

    expect(result.current.feedback?.message).toBe("One or more recipients are no longer available.");
    expect(result.current.name).toBe("Keep this draft");
    expect(result.current.scope).toBe("users");
    expect(result.current.recipients).toEqual([recipient]);
  });

  it("saves a received view only as a personal copy with no inherited recipients", async () => {
    const received = createView("received", {
      name: "Owner plan",
      scope: "users",
      owner: "owner@aggreko.com",
      isOwner: false,
      canEdit: false,
      canDelete: false,
      recipients: [{ loginName: "hidden@aggreko.com", fullName: "Hidden" }],
    });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [received] });
    const { result } = renderSavedViews({ captureState: () => received.state });
    await waitFor(() => expect(result.current.views).toEqual([received]));
    act(() => result.current.selectView(received.id));
    expect(result.current.isSelectedViewReceived).toBe(true);
    expect(result.current.scope).toBe("personal");
    expect(result.current.recipients).toEqual([]);
    expect(result.current.isSharingDirty).toBe(false);
    expect(result.current.isDirty).toBe(false);
    act(() => result.current.setName("Owner plan copy"));
    const copy = createView("copy", { name: "Owner plan copy", scope: "personal" });
    mockedCreateSavedView.mockResolvedValue({ source: "api", view: copy });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [received, copy] });

    await act(async () => result.current.saveNew());

    expect(mockedCreateSavedView).toHaveBeenCalledWith(
      "api",
      "agreements",
      "Owner plan copy",
      "personal",
      expect.any(Object),
      parseAgreementsSavedViewState,
      [],
    );
    expect(mockedUpdateSavedView).not.toHaveBeenCalled();
  });

  it("tracks dirty state and updates only sharing fields through the sharing action", async () => {
    const originalRecipient = { loginName: "alex@aggreko.com", fullName: "Alex Smith" };
    const nextRecipient = { loginName: "sam@aggreko.com", fullName: "Sam Jones" };
    const selected = createView("shared", {
      name: "Original operations",
      scope: "users",
      recipients: [originalRecipient],
    });
    window.localStorage.setItem(AGREEMENT_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected] });
    const applyState = vi.fn();
    const { result } = renderSavedViews({ applyState, captureState: () => selected.state });
    await waitFor(() => expect(result.current.selectedViewId).toBe(selected.id));
    expect(result.current.isDirty).toBe(false);

    act(() => {
      result.current.editSharing();
      result.current.setRecipients([nextRecipient]);
    });
    expect(result.current.isSharingDirty).toBe(true);
    expect(result.current.isDirty).toBe(true);

    const updated = createView(selected.id, { ...selected, recipients: [nextRecipient] });
    mockedUpdateSavedView.mockResolvedValue({ source: "api", view: updated });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [updated] });
    await act(async () => result.current.saveSharing());

    expect(mockedUpdateSavedView).toHaveBeenCalledWith(
      "api",
      "agreements",
      selected.id,
      selected.name,
      "users",
      selected.state,
      parseAgreementsSavedViewState,
      [nextRecipient.loginName],
    );
    expect(applyState).toHaveBeenCalledOnce();
    expect(result.current.pendingAction).toBeNull();
    expect(result.current.feedback).toEqual({
      tone: "success",
      message: "Updated sharing: Original operations",
    });
  });

  it("keeps the selected view when discarding changes or deletion is cancelled", async () => {
    const selected = createView("selected", { name: "Operations" });
    const other = createView("other");
    window.localStorage.setItem(AGREEMENT_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected, other] });
    const { result } = renderSavedViews({ captureState: () => selected.state });
    await waitFor(() => expect(result.current.selectedViewId).toBe(selected.id));
    act(() => result.current.setName("Unsaved name"));

    vi.mocked(window.confirm).mockReturnValueOnce(false);
    act(() => result.current.selectView(other.id));
    expect(result.current.selectedViewId).toBe(selected.id);

    vi.mocked(window.confirm).mockReturnValueOnce(false);
    await act(async () => result.current.deleteSelected());
    expect(mockedDeleteSavedView).not.toHaveBeenCalled();
    expect(result.current.selectedViewId).toBe(selected.id);
  });
});
