import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import styles from "../../components/common/SavedViewControls/SavedViewControls.module.css";
import "../../i18n";
import type { PersistedSavedView, SavedViewScope } from "../../services/viewsService";
import type { AgreementsSavedViewState } from "../../types/savedViews";
import { AgreementSavedViewControls, type AgreementSavedViewControlsProps } from "./AgreementSavedViewControls";

function createSavedViewState(): AgreementsSavedViewState {
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
  };
}

function createView(
  id: string,
  scope: SavedViewScope,
  overrides: Partial<PersistedSavedView<AgreementsSavedViewState>> = {},
): PersistedSavedView<AgreementsSavedViewState> {
  return {
    id,
    name: `View ${id}`,
    page: "agreements",
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

function createProps(overrides: Partial<AgreementSavedViewControlsProps> = {}): AgreementSavedViewControlsProps {
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

describe("AgreementSavedViewControls", () => {
  afterEach(cleanup);

  it("groups Personal, Division, and Global views and prefixes defaults", () => {
    const views = [
      createView("global", "global", { name: "All teams" }),
      createView("personal-default", "personal", { name: "My default", isDefault: true }),
      createView("division", "division", { name: "Division plan" }),
      createView("personal", "personal", { name: "My second view" }),
    ];

    render(<AgreementSavedViewControls {...createProps({ views })} />);

    const selector = screen.getByLabelText("Agreement View");
    const groups = Array.from(selector.querySelectorAll("optgroup"));

    expect(within(selector).getByRole("option", { name: "Agreement View" })).toHaveValue("");
    expect(groups.map((group) => group.label)).toEqual(["Personal", "Division", "Global"]);
    expect(
      within(groups[0])
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["Default: My default", "My second view"]);
    expect(within(groups[1]).getByRole("option")).toHaveTextContent("Division plan");
    expect(within(groups[2]).getByRole("option")).toHaveTextContent("All teams");
  });

  it("groups received user shares separately with owner text", () => {
    const views = [
      createView("owned", "users", { name: "My team", isOwner: true }),
      createView("received", "users", { name: "Morning plan", isOwner: false, owner: "planner@aggreko.com" }),
    ];
    render(<AgreementSavedViewControls {...createProps({ views })} />);
    const groups = screen.getByLabelText("Agreement View").querySelectorAll("optgroup");
    expect(Array.from(groups).map((group) => group.label)).toEqual(["Personal", "Shared with me"]);
    expect(within(groups[0]).getByRole("option")).toHaveTextContent("My team — shared");
    expect(within(groups[1]).getByRole("option")).toHaveTextContent("Morning plan — shared by planner@aggreko.com");
  });

  it("does not expose the recipient editor to a read-only recipient", () => {
    const received = createView("received", "users", { isOwner: false, canEdit: false, canDelete: false });
    render(
      <AgreementSavedViewControls
        {...createProps({ canPersistViews: false, scope: "users", selectedViewId: received.id, views: [received] })}
      />,
    );
    expect(screen.queryByRole("combobox", { name: "Share with people" })).not.toBeInTheDocument();
    expect(screen.getByRole("option", { name: /shared by developer@example.com/ })).toBeInTheDocument();
  });

  it("keeps owned sharing collapsed until Edit sharing is chosen", () => {
    const onEditSharing = vi.fn();
    const owned = createView("owned", "users");
    render(
      <AgreementSavedViewControls
        {...createProps({
          canEditSelectedView: true,
          scope: "users",
          selectedViewId: owned.id,
          views: [owned],
          onEditSharing,
        })}
      />,
    );

    expect(screen.queryByRole("combobox", { name: "Share with people" })).not.toBeInTheDocument();
    expect(screen.getByText("Shared with 0 people")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Edit sharing" }));
    expect(onEditSharing).toHaveBeenCalledOnce();
  });

  it("updates an existing view in place when saving sharing changes", () => {
    const onSaveNew = vi.fn();
    const onUpdate = vi.fn();
    const owned = createView("owned", "users");

    render(
      <AgreementSavedViewControls
        {...createProps({
          canEditSelectedView: true,
          isSharingEditorOpen: true,
          scope: "users",
          selectedViewId: owned.id,
          views: [owned],
          onSaveNew,
          onUpdate,
        })}
      />,
    );

    expect(screen.queryByRole("button", { name: "Save as new" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Save sharing" }));
    expect(onUpdate).toHaveBeenCalledOnce();
    expect(onSaveNew).not.toHaveBeenCalled();
  });

  it("offers only a personal copy path for a received view", () => {
    const onSaveNew = vi.fn();
    const received = createView("received", "users", { isOwner: false, canEdit: false, canDelete: false });
    const props = createProps({
      isSelectedViewReceived: true,
      name: received.name,
      scope: "personal",
      selectedViewId: received.id,
      views: [received],
      onSaveNew,
    });
    const { rerender } = render(<AgreementSavedViewControls {...props} />);

    expect(screen.getByLabelText("Saved view scope")).toBeDisabled();
    expect(screen.queryByRole("combobox", { name: "Share with people" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save changes" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Delete" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Save a copy" })).toBeDisabled();

    rerender(<AgreementSavedViewControls {...props} name="View received copy" />);
    fireEvent.click(screen.getByRole("button", { name: "Save a copy" }));
    expect(onSaveNew).toHaveBeenCalledOnce();
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
    const { rerender } = render(<AgreementSavedViewControls {...props} />);

    expect(screen.getByLabelText("Agreement View")).toHaveValue("division");
    expect(screen.getByLabelText("Saved view name")).toHaveValue("Current name");
    expect(screen.getByLabelText("Saved view scope")).toHaveValue("division");

    fireEvent.change(screen.getByLabelText("Agreement View"), { target: { value: "personal" } });
    fireEvent.change(screen.getByLabelText("Saved view name"), { target: { value: "Renamed view" } });
    fireEvent.change(screen.getByLabelText("Saved view scope"), { target: { value: "global" } });

    expect(onSelectionChange).toHaveBeenCalledWith("personal");
    expect(onNameChange).toHaveBeenCalledWith("Renamed view");
    expect(onScopeChange).toHaveBeenCalledWith("global");
    expect(screen.getByLabelText("Agreement View")).toHaveValue("division");
    expect(screen.getByLabelText("Saved view name")).toHaveValue("Current name");
    expect(screen.getByLabelText("Saved view scope")).toHaveValue("division");

    rerender(<AgreementSavedViewControls {...props} name="Renamed view" scope="global" selectedViewId="personal" />);
    expect(screen.getByLabelText("Agreement View")).toHaveValue("personal");
    expect(screen.getByLabelText("Saved view name")).toHaveValue("Renamed view");
    expect(screen.getByLabelText("Saved view scope")).toHaveValue("global");
  });

  it("disables shared scopes for non-admins and enables them for admins", () => {
    const props = createProps({ canManageSharedViews: false });
    const { rerender } = render(<AgreementSavedViewControls {...props} />);
    const scopeSelector = screen.getByLabelText("Saved view scope");

    expect(within(scopeSelector).getByRole("option", { name: "Personal" })).toBeEnabled();
    expect(within(scopeSelector).getByRole("option", { name: "Division" })).toBeDisabled();
    expect(within(scopeSelector).getByRole("option", { name: "Global" })).toBeDisabled();

    rerender(<AgreementSavedViewControls {...props} canManageSharedViews />);
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
    const { rerender } = render(<AgreementSavedViewControls {...props} />);

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
      <AgreementSavedViewControls {...props} canDeleteSelectedView={false} canEditSelectedView onDelete={onDelete} />,
    );
    expect(screen.getByRole("button", { name: "Save changes" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Delete" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(onUpdate).toHaveBeenCalledOnce();
    expect(onDelete).toHaveBeenCalledOnce();
  });

  it("renders success and error feedback with the shared tone classes and exact text", () => {
    const props = createProps({ feedback: { tone: "success", message: "Saved new view: Morning plan" } });
    const { rerender } = render(<AgreementSavedViewControls {...props} />);

    expect(screen.getByRole("status")).toHaveTextContent("Saved new view: Morning plan");
    expect(screen.getByRole("status")).toHaveClass(styles.success);
    expect(screen.getByRole("status")).toHaveAttribute("aria-live", "polite");

    rerender(
      <AgreementSavedViewControls
        {...props}
        feedback={{ tone: "error", message: "Unable to save this view right now." }}
      />,
    );
    expect(screen.getByRole("alert")).toHaveTextContent("Unable to save this view right now.");
    expect(screen.getByRole("alert")).toHaveClass(styles.error);

    rerender(<AgreementSavedViewControls {...props} feedback={null} />);
    expect(screen.queryByText("Unable to save this view right now.")).not.toBeInTheDocument();
  });

  it("associates recipient validation and moves focus to the search field", () => {
    render(
      <AgreementSavedViewControls
        {...createProps({
          scope: "users",
          isSharingEditorOpen: true,
          feedback: { tone: "error", message: "Select at least one recipient." },
        })}
      />,
    );
    const search = screen.getByRole("combobox", { name: "Share with people" });
    expect(search).toHaveFocus();
    expect(search).toHaveAttribute("aria-invalid", "true");
    expect(search.getAttribute("aria-describedby")?.split(" ")).toHaveLength(2);
  });
});
