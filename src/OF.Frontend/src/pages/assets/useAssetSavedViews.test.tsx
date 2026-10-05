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
import { parseAssetsSavedViewState, type AssetsSavedViewState } from "../../types/savedViews";
import { useAssetSavedViews } from "./useAssetSavedViews";

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

const ASSET_SELECTION_KEY = "assets.savedViewSelection.v2";

function createState(overrides: Partial<AssetsSavedViewState> = {}): AssetsSavedViewState {
  return {
    stateVersion: 1,
    viewMode: "table",
    searchTerm: "",
    statusFilter: "",
    warehouseFilter: "",
    hideRemovedStock: true,
    selectedDivision: "01",
    advancedFilters: {},
    columnFilters: {},
    sortField: "id",
    sortDirection: "asc",
    ...overrides,
  };
}

function createView(
  id: string,
  overrides: Partial<PersistedSavedView<AssetsSavedViewState>> = {},
): PersistedSavedView<AssetsSavedViewState> {
  return {
    id,
    name: `View ${id}`,
    page: "assets",
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
    applyState?: (state: AssetsSavedViewState) => void;
    canManageSharedViews?: boolean;
    captureState?: () => AssetsSavedViewState;
  } = {},
) {
  const applyState = options.applyState ?? vi.fn();
  const captureState = options.captureState ?? vi.fn(() => createState({ searchTerm: "captured" }));
  const rendered = renderHook(() =>
    useAssetSavedViews({
      applyState,
      canManageSharedViews: options.canManageSharedViews ?? false,
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

describe("useAssetSavedViews", () => {
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

  it("restores and persists the Asset selection with the exact page and decoder", async () => {
    window.localStorage.setItem(ASSET_SELECTION_KEY, "current-view");
    const restored = createView("current-view", { scope: "division" });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [restored] });
    const applyState = vi.fn();

    const { result } = renderSavedViews({ applyState });

    await waitFor(() => expect(result.current.name).toBe(restored.name));
    expect(result.current.selectedViewId).toBe(restored.id);
    expect(result.current.scope).toBe("division");
    expect(applyState).toHaveBeenCalledWith(restored.state);
    expect(mockedListSavedViews).toHaveBeenCalledWith("assets", parseAssetsSavedViewState);
    expect(window.localStorage.getItem(ASSET_SELECTION_KEY)).toBe(restored.id);
  });

  it("auto-applies a default only once and clears its success feedback after 2800ms", async () => {
    vi.useFakeTimers();
    const defaultView = createView("default", { name: "Default asset plan", isDefault: true });
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
      message: "Default view applied: Default asset plan",
    });
    expect(applyState).toHaveBeenCalledWith(defaultView.state);

    act(() => vi.advanceTimersByTime(2799));
    expect(result.current.feedback).not.toBeNull();
    act(() => vi.advanceTimersByTime(1));
    expect(result.current.feedback).toBeNull();

    const applyCountBeforeClear = applyState.mock.calls.length;
    act(() => result.current.selectView(""));
    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(result.current.selectedViewId).toBe("");
    expect(result.current.name).toBe("");
    expect(result.current.scope).toBe("personal");
    expect(applyState).toHaveBeenCalledTimes(applyCountBeforeClear);
  });

  it("applies known selections and resets only the saved-view fields when selection is cleared", async () => {
    const divisionView = createView("division", { scope: "division", name: "Division assets" });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [divisionView] });
    const applyState = vi.fn();
    const { result } = renderSavedViews({ applyState });
    await waitFor(() => expect(result.current.views).toEqual([divisionView]));

    act(() => result.current.selectView(divisionView.id));
    expect(result.current.selectedViewId).toBe(divisionView.id);
    expect(result.current.name).toBe("Division assets");
    expect(result.current.scope).toBe("division");
    expect(applyState).toHaveBeenLastCalledWith(divisionView.state);
    await waitFor(() => expect(window.localStorage.getItem(ASSET_SELECTION_KEY)).toBe(divisionView.id));

    const applyCountBeforeClear = applyState.mock.calls.length;
    act(() => result.current.selectView(""));
    expect(result.current.selectedViewId).toBe("");
    expect(result.current.name).toBe("");
    expect(result.current.scope).toBe("personal");
    expect(applyState).toHaveBeenCalledTimes(applyCountBeforeClear);
    await waitFor(() => expect(window.localStorage.getItem(ASSET_SELECTION_KEY)).toBeNull());
  });

  it("creates through the loaded local source with the exact captured Asset state and decoder", async () => {
    mockedListSavedViews.mockResolvedValue({ source: "local", views: [] });
    const capturedState = createState({ searchTerm: "capture me", sortField: "daysOffHire" });
    const captureState = vi.fn(() => capturedState);
    const { result } = renderSavedViews({ captureState });
    await waitForInitialViews();

    const createdView = createView("local-new", {
      name: "Local asset plan",
      source: "local",
      state: capturedState,
    });
    mockedCreateSavedView.mockResolvedValue({ source: "local", view: createdView });
    mockedListSavedViews.mockResolvedValue({ source: "local", views: [createdView] });
    act(() => result.current.setName("Local asset plan"));

    await act(async () => result.current.saveNew());

    expect(mockedCreateSavedView).toHaveBeenCalledWith(
      "local",
      "assets",
      "Local asset plan",
      "personal",
      capturedState,
      parseAssetsSavedViewState,
      [],
    );
    expect(captureState).toHaveBeenCalled();
    expect(result.current.selectedViewId).toBe(createdView.id);
    expect(result.current.feedback).toEqual({ tone: "success", message: "Saved new view: Local asset plan" });
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
      result.current.setName("Shared assets");
      result.current.setScope("division");
    });

    await act(async () => result.current.saveNew());

    expect(mockedCreateSavedView).not.toHaveBeenCalled();
    expect(result.current.feedback).toEqual({
      tone: "error",
      message: "Only admins can create division or global views.",
    });
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
    window.localStorage.setItem(ASSET_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected] });
    const capturedState = createState({ searchTerm: "updated", sortDirection: "desc" });
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
      "assets",
      selected.id,
      "After update",
      "global",
      capturedState,
      parseAssetsSavedViewState,
      [],
    );
    expect(result.current.feedback).toEqual({ tone: "success", message: "Updated view: After update" });
    expect(result.current.name).toBe("After update");
    expect(result.current.scope).toBe("global");
  });

  it("enforces selected-view update and delete permissions before invoking services", async () => {
    const selected = createView("locked", { canEdit: false, canDelete: false });
    window.localStorage.setItem(ASSET_SELECTION_KEY, selected.id);
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
    window.localStorage.setItem(ASSET_SELECTION_KEY, selected.id);
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
    window.localStorage.setItem(ASSET_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "local", views: [selected] });
    const { result } = renderSavedViews();
    await waitFor(() => expect(result.current.canDeleteSelectedView).toBe(true));

    mockedDeleteSavedView.mockResolvedValue({ source: "local", deleted: true });
    mockedListSavedViews.mockResolvedValue({ source: "local", views: [] });
    await act(async () => result.current.deleteSelected());

    expect(mockedDeleteSavedView).toHaveBeenCalledWith("local", "assets", selected.id);
    expect(result.current.selectedViewId).toBe("");
    expect(result.current.name).toBe("");
    expect(result.current.scope).toBe("personal");
    expect(result.current.feedback).toEqual({ tone: "success", message: "Deleted view: Local delete" });
    expect(window.localStorage.getItem(ASSET_SELECTION_KEY)).toBeNull();
  });

  it("requires recipients for a user share and identifies received views", async () => {
    const received = createView("81", {
      scope: "users",
      isOwner: false,
      canEdit: false,
      canDelete: false,
      owner: "owner@aggreko.com",
    });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [received] });
    const { result } = renderSavedViews();
    await waitFor(() => expect(result.current.views).toEqual([received]));
    act(() => result.current.selectView(received.id));
    expect(result.current.isSelectedViewReceived).toBe(true);

    act(() => {
      result.current.selectView("");
      result.current.setName("Empty share");
      result.current.setScope("users");
    });
    await act(async () => result.current.saveNew());
    expect(mockedCreateSavedView).not.toHaveBeenCalled();
    expect(result.current.feedback?.message).toBe("Select at least one recipient.");
  });

  it("saves a received view only as a personal copy with no inherited recipients", async () => {
    const received = createView("received", {
      name: "Owner assets",
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
    expect(result.current.scope).toBe("personal");
    expect(result.current.recipients).toEqual([]);
    expect(result.current.isSharingDirty).toBe(false);
    expect(result.current.isDirty).toBe(false);
    act(() => result.current.setName("Owner assets copy"));
    const copy = createView("copy", { name: "Owner assets copy", scope: "personal" });
    mockedCreateSavedView.mockResolvedValue({ source: "api", view: copy });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [received, copy] });

    await act(async () => result.current.saveNew());

    expect(mockedCreateSavedView).toHaveBeenCalledWith(
      "api",
      "assets",
      "Owner assets copy",
      "personal",
      expect.any(Object),
      parseAssetsSavedViewState,
      [],
    );
    expect(mockedUpdateSavedView).not.toHaveBeenCalled();
  });

  it("tracks unsaved metadata and grid state and can cancel switching views", async () => {
    const selected = createView("selected");
    const other = createView("other");
    let currentState = selected.state;
    window.localStorage.setItem(ASSET_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected, other] });
    const { result, rerender } = renderSavedViews({ captureState: () => currentState });
    await waitFor(() => expect(result.current.canDeleteSelectedView).toBe(true));

    expect(result.current.isDirty).toBe(false);
    act(() => result.current.setName("Renamed"));
    expect(result.current.isDirty).toBe(true);
    vi.mocked(window.confirm).mockReturnValueOnce(false);
    act(() => result.current.selectView(other.id));
    expect(result.current.selectedViewId).toBe(selected.id);
    expect(window.confirm).toHaveBeenCalledWith("Discard unsaved changes and switch views?");

    act(() => result.current.setName(selected.name));
    currentState = createState({ searchTerm: "changed grid" });
    rerender();
    expect(result.current.isDirty).toBe(true);
  });

  it("updates sharing without capturing edited grid state or name", async () => {
    const originalRecipient = { loginName: "alex@aggreko.com", fullName: "Alex Smith" };
    const nextRecipient = { loginName: "sam@aggreko.com", fullName: "Sam Jones" };
    const selected = createView("shared", {
      name: "Original name",
      scope: "users",
      recipients: [originalRecipient],
    });
    window.localStorage.setItem(ASSET_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected] });
    const applyState = vi.fn();
    const { result } = renderSavedViews({
      applyState,
      captureState: () => createState({ searchTerm: "unsaved grid change" }),
    });
    await waitFor(() => expect(result.current.selectedViewId).toBe(selected.id));
    act(() => {
      result.current.setName("Unsaved rename");
      result.current.editSharing();
      result.current.setRecipients([nextRecipient]);
    });
    expect(result.current.isSharingDirty).toBe(true);

    const updated = createView(selected.id, { ...selected, recipients: [nextRecipient] });
    mockedUpdateSavedView.mockResolvedValue({ source: "api", view: updated });
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [updated] });
    await act(async () => result.current.saveSharing());

    expect(mockedUpdateSavedView).toHaveBeenCalledWith(
      "api",
      "assets",
      selected.id,
      selected.name,
      "users",
      selected.state,
      parseAssetsSavedViewState,
      [nextRecipient.loginName],
    );
    expect(applyState).toHaveBeenCalledOnce();
    expect(result.current.name).toBe("Unsaved rename");
    expect(result.current.feedback).toEqual({ tone: "success", message: "Updated sharing: Original name" });
  });

  it("confirms shared deletion and ignores a second mutation while one is pending", async () => {
    const selected = createView("shared", {
      name: "Team assets",
      scope: "users",
      recipients: [{ loginName: "alex@aggreko.com", fullName: "Alex Smith" }],
    });
    window.localStorage.setItem(ASSET_SELECTION_KEY, selected.id);
    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected] });
    const { result } = renderSavedViews({ captureState: () => selected.state });
    await waitFor(() => expect(result.current.canDeleteSelectedView).toBe(true));

    vi.mocked(window.confirm).mockReturnValueOnce(false);
    await act(async () => result.current.deleteSelected());
    expect(window.confirm).toHaveBeenCalledWith(
      "Delete “Team assets”? People it is shared with will lose access. This action cannot be undone.",
    );
    expect(mockedDeleteSavedView).not.toHaveBeenCalled();

    let resolveUpdate!: (value: { source: SavedViewSource; view: typeof selected }) => void;
    mockedUpdateSavedView.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveUpdate = resolve;
        }),
    );
    let firstUpdate!: Promise<void>;
    act(() => {
      result.current.setName("Changed");
    });
    act(() => {
      firstUpdate = result.current.updateSelected();
      void result.current.updateSelected();
    });
    expect(result.current.pendingAction).toBe("save-changes");
    expect(mockedUpdateSavedView).toHaveBeenCalledOnce();

    mockedListSavedViews.mockResolvedValue({ source: "api", views: [selected] });
    await act(async () => {
      resolveUpdate({ source: "api", view: selected });
      await firstUpdate;
    });
    expect(result.current.pendingAction).toBeNull();
  });
});
