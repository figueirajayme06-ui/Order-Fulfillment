import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import styles from "../../components/common/SavedViewControls/SavedViewControls.module.css";
import "../../i18n";
import type { PersistedSavedView, SavedViewScope } from "../../services/viewsService";
import type { AssetsSavedViewState } from "../../types/savedViews";
import { AssetSavedViewControls, type AssetSavedViewControlsProps } from "./AssetSavedViewControls";

function createSavedViewState(): AssetsSavedViewState {
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
  };
}

function createView(
  id: string,
  scope: SavedViewScope,
  overrides: Partial<PersistedSavedView<AssetsSavedViewState>> = {},
): PersistedSavedView<AssetsSavedViewState> {
  return {
    id,
    name: `View ${id}`,
    page: "assets",
    scope,
    owner: "developer@example.com",
    isOwner: true,
    canEdit: true,
    canDelete: true,
    isDefault: false,
    source: "api",
    recipients: [],
    state: createSavedViewState(),
    ...overrides,
  };
}

function createProps(overrides: Partial<AssetSavedViewControlsProps> = {}): AssetSavedViewControlsProps {
  return {
    canDeleteSelectedView: false,
    canEditSelectedView: false,
    canManageSharedViews: false,
    canPersistViews: true,
    feedback: null,
    name: "",
    recipientCandidates: [],
    recipientError: null,
    recipients: [],
    isRecipientLoading: false,
    isSelectedViewReceived: false,
    isSharingEditorOpen: false,
    scope: "personal",
    selectedViewId: "",
    views: [],
    onCancelSharingEdit: vi.fn(),
    onDelete: vi.fn(),
    onEditSharing: vi.fn(),
    onNameChange: vi.fn(),
    onRecipientsChange: vi.fn(),
    onRetryRecipients: vi.fn(),
    onSearchRecipients: vi.fn(),
    onSaveNew: vi.fn(),
    onScopeChange: vi.fn(),
    onSelectionChange: vi.fn(),
    onUpdate: vi.fn(),
    ...overrides,
  };
}

describe("AssetSavedViewControls", () => {
  afterEach(cleanup);

  it("groups Personal, Division, and Global views and prefixes defaults", () => {
    const views = [
      createView("global", "global", { name: "All depots" }),
      createView("personal-default", "personal", { name: "My default", isDefault: true }),
      createView("division", "division", { name: "Division stock" }),
      createView("personal", "personal", { name: "My second view" }),
    ];

    const { container } = render(<AssetSavedViewControls {...createProps({ views })} />);

    const selector = screen.getByLabelText("Asset View");
    const groups = Array.from(selector.querySelectorAll("optgroup"));

    expect(container.querySelector(`.${styles.bar}`)).toBeInTheDocument();
    expect(within(selector).getByRole("option", { name: "Asset View" })).toHaveValue("");
    expect(groups.map((group) => group.label)).toEqual(["Personal", "Division", "Global"]);
    expect(
      within(groups[0])
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["Default: My default", "My second view"]);
    expect(within(groups[1]).getByRole("option")).toHaveTextContent("Division stock");
    expect(within(groups[2]).getByRole("option")).toHaveTextContent("All depots");
  });

  it("distinguishes an owned shared view from a personal view with the same name", () => {
    const views = [
      createView("personal", "personal", { name: "scrapped" }),
      createView("shared", "users", { name: "scrapped" }),
    ];

    render(<AssetSavedViewControls {...createProps({ views })} />);

    const options = within(screen.getByRole("group", { name: "Personal" })).getAllByRole("option");
    expect(options.map((option) => option.textContent)).toEqual(["scrapped", "scrapped — shared"]);
  });

  it("shows the accessible recipient editor and disables mutations while candidates load", () => {
    render(
      <AssetSavedViewControls
        {...createProps({ scope: "users", isRecipientLoading: true, isSharingEditorOpen: true })}
      />,
    );
    expect(screen.getByRole("combobox", { name: "Share with people" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Save as new" })).toBeDisabled();
    expect(screen.getByText("Loading eligible people…")).toBeInTheDocument();
  });

  it("keeps owned sharing collapsed and hides all sharing controls from a received view", () => {
    const owned = createView("owned", "users");
    const props = createProps({
      canEditSelectedView: true,
      scope: "users",
      selectedViewId: owned.id,
      views: [owned],
    });
    const { rerender } = render(<AssetSavedViewControls {...props} />);
    expect(screen.queryByRole("combobox", { name: "Share with people" })).not.toBeInTheDocument();
    expect(screen.getByText("Shared with 0 people")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Edit sharing" })).toBeInTheDocument();

    const received = createView("received", "users", { isOwner: false, canEdit: false, canDelete: false });
    rerender(
      <AssetSavedViewControls
        {...props}
        canEditSelectedView={false}
        isSelectedViewReceived
        scope="personal"
        selectedViewId={received.id}
        views={[received]}
      />,
    );
    expect(screen.getByLabelText("Saved view scope")).toBeDisabled();
    expect(screen.queryByRole("button", { name: "Edit sharing" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save a copy" })).toBeEnabled();
  });

  it("uses the recipient-only action when saving sharing changes", () => {
    const onSaveNew = vi.fn();
    const onSaveSharing = vi.fn();
    const onUpdate = vi.fn();
    const owned = createView("owned", "users");

    render(
      <AssetSavedViewControls
        {...createProps({
          canEditSelectedView: true,
          isSharingEditorOpen: true,
          scope: "users",
          selectedViewId: owned.id,
          views: [owned],
          onSaveNew,
          onSaveSharing,
          onUpdate,
        })}
      />,
    );

    expect(screen.queryByRole("button", { name: "Save as new" })).not.toBeInTheDocument();
    expect(screen.getByLabelText("Saved view name")).toBeDisabled();
    expect(screen.getByLabelText("Saved view scope")).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Save sharing" }));
    expect(onSaveSharing).toHaveBeenCalledOnce();
    expect(onUpdate).not.toHaveBeenCalled();
    expect(onSaveNew).not.toHaveBeenCalled();
  });

  it("keeps the selected view, name, and scope controlled and reports changes", () => {
    const onNameChange = vi.fn();
    const onScopeChange = vi.fn();
    const onSelectionChange = vi.fn();
    const views = [createView("personal", "personal"), createView("division", "division")];
    const props = createProps({
      canManageSharedViews: true,
      name: "Current name",
      scope: "division",
      selectedViewId: "division",
      views,
      onNameChange,
      onScopeChange,
      onSelectionChange,
    });
    const { rerender } = render(<AssetSavedViewControls {...props} />);

    expect(screen.getByLabelText("Asset View")).toHaveValue("division");
    expect(screen.getByLabelText("Saved view name")).toHaveValue("Current name");
    expect(screen.getByLabelText("Saved view scope")).toHaveValue("division");

    fireEvent.change(screen.getByLabelText("Asset View"), { target: { value: "personal" } });
    fireEvent.change(screen.getByLabelText("Saved view name"), { target: { value: "Renamed view" } });
    fireEvent.change(screen.getByLabelText("Saved view scope"), { target: { value: "global" } });

    expect(onSelectionChange).toHaveBeenCalledWith("personal");
    expect(onNameChange).toHaveBeenCalledWith("Renamed view");
    expect(onScopeChange).toHaveBeenCalledWith("global");
    expect(screen.getByLabelText("Asset View")).toHaveValue("division");
    expect(screen.getByLabelText("Saved view name")).toHaveValue("Current name");
    expect(screen.getByLabelText("Saved view scope")).toHaveValue("division");

    rerender(<AssetSavedViewControls {...props} name="Renamed view" scope="global" selectedViewId="personal" />);
    expect(screen.getByLabelText("Asset View")).toHaveValue("personal");
    expect(screen.getByLabelText("Saved view name")).toHaveValue("Renamed view");
    expect(screen.getByLabelText("Saved view scope")).toHaveValue("global");
  });

  it("disables shared scopes for non-admins and enables them for admins", () => {
    const props = createProps({ canManageSharedViews: false });
    const { rerender } = render(<AssetSavedViewControls {...props} />);
    const scopeSelector = screen.getByLabelText("Saved view scope");

    expect(within(scopeSelector).getByRole("option", { name: "Personal" })).toBeEnabled();
    expect(within(scopeSelector).getByRole("option", { name: "Division" })).toBeDisabled();
    expect(within(scopeSelector).getByRole("option", { name: "Global" })).toBeDisabled();

    rerender(<AssetSavedViewControls {...props} canManageSharedViews />);
    expect(within(scopeSelector).getByRole("option", { name: "Division" })).toBeEnabled();
    expect(within(scopeSelector).getByRole("option", { name: "Global" })).toBeEnabled();
  });

  it("enables update and delete from their permissions and invokes allowed actions", () => {
    const onDelete = vi.fn();
    const onSaveNew = vi.fn();
    const onUpdate = vi.fn();
    const selected = createView("selected", "personal");
    const props = createProps({
      canDeleteSelectedView: true,
      canEditSelectedView: false,
      selectedViewId: selected.id,
      views: [selected],
      onDelete,
      onSaveNew,
      onUpdate,
    });
    const { rerender } = render(<AssetSavedViewControls {...props} />);

    expect(screen.getByRole("button", { name: "Save as new" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Delete" })).toBeEnabled();
    fireEvent.click(screen.getByRole("button", { name: "Save as new" }));
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(onSaveNew).toHaveBeenCalledOnce();
    expect(onUpdate).not.toHaveBeenCalled();
    expect(onDelete).toHaveBeenCalledOnce();

    rerender(
      <AssetSavedViewControls {...props} canDeleteSelectedView={false} canEditSelectedView onDelete={onDelete} />,
    );
    expect(screen.getByRole("button", { name: "Save changes" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Delete" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(onUpdate).toHaveBeenCalledOnce();
    expect(onDelete).toHaveBeenCalledOnce();
  });

  it("does not show a duplicate-name validation message when selecting an existing view", () => {
    const selected = createView("selected", "users", { name: "New View" });
    const props = createProps({
      canEditSelectedView: true,
      isDirty: true,
      name: "  new view  ",
      scope: "users",
      selectedViewId: selected.id,
      views: [selected],
    });
    const { rerender } = render(<AssetSavedViewControls {...props} />);

    expect(screen.getByRole("button", { name: "Save as new" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Save changes" })).toBeEnabled();
    expect(screen.queryByText("Enter a different name to save this as a new view.")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Saved view name")).not.toHaveAttribute("aria-invalid", "true");

    rerender(<AssetSavedViewControls {...props} name="New View copy" />);
    expect(screen.getByRole("button", { name: "Save as new" })).toBeEnabled();
    expect(screen.queryByText("Enter a different name to save this as a new view.")).not.toBeInTheDocument();
  });

  it("prevents renaming a view to another saved view's name", () => {
    const selected = createView("selected", "personal", { name: "Morning plan" });
    const existing = createView("existing", "personal", { name: "Evening plan" });

    render(
      <AssetSavedViewControls
        {...createProps({
          canEditSelectedView: true,
          isDirty: true,
          name: "evening PLAN",
          selectedViewId: selected.id,
          views: [selected, existing],
        })}
      />,
    );

    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Save as new" })).toBeDisabled();
    expect(screen.getByLabelText("Saved view name")).toHaveAttribute("aria-invalid", "true");
    expect(
      screen.getByText("A saved view with this name already exists. Choose a different name."),
    ).toBeInTheDocument();
  });

  it("renders success and error feedback with the shared tone classes and exact text", () => {
    const props = createProps({ feedback: { tone: "success", message: "Saved new view: Morning assets" } });
    const { rerender } = render(<AssetSavedViewControls {...props} />);

    expect(screen.getByRole("status")).toHaveTextContent("Saved new view: Morning assets");
    expect(screen.getByRole("status")).toHaveClass(styles.success);

    rerender(
      <AssetSavedViewControls
        {...props}
        feedback={{ tone: "error", message: "Unable to save this view right now." }}
      />,
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Unable to save this view right now.");
    expect(screen.getByRole("alert")).toHaveClass(styles.error);

    rerender(<AssetSavedViewControls {...props} feedback={null} />);
    expect(screen.queryByText("Unable to save this view right now.")).not.toBeInTheDocument();
  });

  it("labels only the active mutation and locks competing actions", () => {
    const selected = createView("selected", "personal");
    const props = createProps({
      canDeleteSelectedView: true,
      canEditSelectedView: true,
      isDirty: true,
      selectedViewId: selected.id,
      views: [selected],
    });
    const { rerender } = render(<AssetSavedViewControls {...props} pendingAction="save-new" />);

    expect(screen.getByRole("button", { name: "Saving…" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Delete" })).toBeDisabled();

    rerender(<AssetSavedViewControls {...props} pendingAction="delete" />);
    expect(screen.getByRole("button", { name: "Deleting…" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Save as new" })).toBeDisabled();
  });
});
