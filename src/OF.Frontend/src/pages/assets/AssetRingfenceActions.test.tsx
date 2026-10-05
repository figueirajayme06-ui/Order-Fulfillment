import { useState } from "react";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import type { RingfenceListItem } from "../../services/ringfenceService";
import { AssetRingfenceActions, type AssetRingfenceActionsProps } from "./AssetRingfenceActions";

function createRingfence(id: number, title: string): RingfenceListItem {
  return {
    id,
    title,
    fromDate: "2026-08-01T00:00:00.000Z",
    toDate: "2026-08-31T00:00:00.000Z",
    divisions: "01",
    warehouse: "GLA",
    owner: "developer@example.com",
    assetCount: 0,
    createdBy: "developer@example.com",
    createdAt: "2026-07-01T00:00:00.000Z",
  };
}

const ringfences = [createRingfence(7, "Priority hire"), createRingfence(8, "Service reserve")];

function createProps(overrides: Partial<AssetRingfenceActionsProps> = {}): AssetRingfenceActionsProps {
  return {
    isBusy: false,
    ringfences,
    selectedAssetCount: 2,
    selectedAssetsOnPageCount: 1,
    selectedRingfenceId: 7,
    contextualReturnPath: null,
    isContextualTarget: false,
    onAdd: vi.fn(),
    onClear: vi.fn(),
    onSelectedRingfenceChange: vi.fn(),
    ...overrides,
  };
}

function ControlledActions({
  initialRingfenceId = "",
  ...overrides
}: Partial<AssetRingfenceActionsProps> & { initialRingfenceId?: number | "" }) {
  const [selectedRingfenceId, setSelectedRingfenceId] = useState<number | "">(initialRingfenceId);

  return (
    <AssetRingfenceActions
      {...createProps(overrides)}
      selectedRingfenceId={selectedRingfenceId}
      onSelectedRingfenceChange={setSelectedRingfenceId}
    />
  );
}

describe("AssetRingfenceActions", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage("en");
  });

  afterEach(cleanup);

  it("renders one persistent Add action and keeps selection context and clearing in the summary", () => {
    const { container } = render(<AssetRingfenceActions {...createProps()} />);

    const target = screen.getByRole("combobox", { name: "Add selected assets to Priority hire" });
    expect(target).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Add to Ringfence (2)" })).toBeEnabled();
    expect(screen.queryByRole("button", { name: "Add Selected (2)" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Clear Selection" })).toBeEnabled();

    const selectionStatus = screen.getByRole("status");
    expect(selectionStatus).toHaveAttribute("aria-live", "polite");
    expect(selectionStatus).toHaveTextContent("2 assets selected");
    expect(selectionStatus).toHaveTextContent("1 on this page");
    expect(selectionStatus).toHaveTextContent("Target: Priority hire");
    expect(container.querySelectorAll("[data-print-hidden]")).toHaveLength(2);
  });

  it("uses singular copy, disables Add without a target, and displays a numeric fallback target", () => {
    const props = createProps({ selectedAssetCount: 1, selectedAssetsOnPageCount: 1, selectedRingfenceId: "" });
    const { rerender } = render(<AssetRingfenceActions {...props} />);

    expect(screen.getByRole("combobox", { name: "Add selected assets to Select a ringfence" })).toBeEnabled();
    expect(screen.getByRole("status")).toHaveTextContent("1 asset selected");
    expect(screen.getByRole("status")).toHaveTextContent("Select a ringfence to continue");
    expect(screen.getByRole("button", { name: "Add to Ringfence (1)" })).toBeDisabled();

    rerender(<AssetRingfenceActions {...props} selectedRingfenceId={99} />);
    expect(screen.getByRole("combobox", { name: "Add selected assets to Ringfence #99" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Add to Ringfence (1)" })).toBeEnabled();
  });

  it("keeps the toolbar visible and its Add disabled when nothing is selected", () => {
    const { container } = render(
      <AssetRingfenceActions {...createProps({ selectedAssetCount: 0, selectedAssetsOnPageCount: 0 })} />,
    );

    expect(screen.getByRole("combobox")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Add to Ringfence (0)" })).toBeDisabled();
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /^Add Selected/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Clear Selection" })).not.toBeInTheDocument();
    expect(container.querySelectorAll("[data-print-hidden]")).toHaveLength(1);
  });

  it("locks the picker and actions while busy or explicitly target-locked", () => {
    const { rerender } = render(<AssetRingfenceActions {...createProps({ isBusy: true })} />);

    expect(screen.getByRole("button", { name: "Adding..." })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Clear Selection" })).toBeDisabled();
    expect(screen.getByRole("combobox")).toBeDisabled();

    rerender(<AssetRingfenceActions {...createProps({ isTargetLocked: true })} />);
    expect(screen.getByRole("combobox")).toBeDisabled();
    expect(screen.getByRole("button", { name: "Add to Ringfence (2)" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Clear Selection" })).toBeDisabled();
  });

  it("preserves the target, single Add, and Clear callback contracts", async () => {
    const user = userEvent.setup();
    const onAdd = vi.fn();
    const onClear = vi.fn();
    const onSelectedRingfenceChange = vi.fn();
    render(<AssetRingfenceActions {...createProps({ onAdd, onClear, onSelectedRingfenceChange })} />);

    await user.click(screen.getByRole("combobox"));
    await user.click(screen.getByRole("option", { name: "Service reserve" }));
    expect(onSelectedRingfenceChange).toHaveBeenCalledWith(8);

    await user.click(screen.getByRole("button", { name: "Add to Ringfence (2)" }));
    expect(onAdd).toHaveBeenCalledOnce();

    await user.click(screen.getByRole("button", { name: "Clear Selection" }));
    expect(onClear).toHaveBeenCalledOnce();
  });

  it("opens into a focused search and selects a filtered result with the mouse", async () => {
    const user = userEvent.setup();
    render(<ControlledActions />);

    await user.click(screen.getByRole("combobox"));
    const search = screen.getByRole("combobox", { name: "Add selected assets to" });
    expect(search).toHaveFocus();
    expect(search).toHaveAttribute("aria-autocomplete", "list");

    await user.type(search, "service");
    expect(screen.getAllByRole("option")).toHaveLength(1);
    await user.click(screen.getByRole("option", { name: "Service reserve" }));

    expect(screen.getByRole("combobox", { name: "Add selected assets to Service reserve" })).toHaveFocus();
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Add to Ringfence (2)" })).toBeEnabled();
  });

  it("supports Arrow keys and Enter for selection, Escape to close, and Tab for normal focus order", async () => {
    const user = userEvent.setup();
    render(<ControlledActions initialRingfenceId={7} />);

    await user.click(screen.getByRole("combobox"));
    await user.keyboard("{ArrowDown}{Enter}");
    expect(screen.getByRole("combobox", { name: "Add selected assets to Service reserve" })).toHaveFocus();

    await user.click(screen.getByRole("combobox"));
    await user.type(screen.getByRole("combobox", { name: "Add selected assets to" }), "missing");
    await user.keyboard("{Escape}");
    expect(screen.getByRole("combobox", { name: "Add selected assets to Service reserve" })).toHaveFocus();

    await user.click(screen.getByRole("combobox"));
    await user.tab();
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Add to Ringfence (2)" })).toHaveFocus();
  });

  it("shows no results only after searching and retains a previously selected target", async () => {
    const user = userEvent.setup();
    render(<ControlledActions initialRingfenceId={7} />);

    await user.click(screen.getByRole("combobox"));
    expect(screen.queryByText("No Ringfences match this name")).not.toBeInTheDocument();
    await user.type(screen.getByRole("combobox", { name: "Add selected assets to" }), "missing");

    expect(screen.getByText("No Ringfences match this name")).toBeInTheDocument();
    expect(screen.queryByRole("option")).not.toBeInTheDocument();
    await user.keyboard("{Escape}");
    expect(screen.getByRole("combobox", { name: "Add selected assets to Priority hire" })).toBeInTheDocument();
  });

  it("keeps a long result set bounded and keyboard-selectable", async () => {
    const user = userEvent.setup();
    const longList = Array.from({ length: 75 }, (_, index) => createRingfence(index + 1, `Ringfence ${index + 1}`));
    render(<ControlledActions ringfences={longList} />);

    await user.click(screen.getByRole("combobox"));
    const listbox = screen.getByRole("listbox");
    expect(within(listbox).getAllByRole("option")).toHaveLength(75);
    await user.keyboard("{ArrowUp}{Enter}");
    expect(screen.getByRole("combobox", { name: "Add selected assets to Ringfence 75" })).toBeInTheDocument();
  });

  it("preserves the locked native target and return link for contextual hand-offs", () => {
    const onSelectedRingfenceChange = vi.fn();
    render(
      <MemoryRouter>
        <AssetRingfenceActions
          {...createProps({
            contextualReturnPath: "/ringfence?ringfenceId=7",
            isContextualTarget: true,
            isTargetLocked: true,
            onSelectedRingfenceChange,
          })}
        />
      </MemoryRouter>,
    );

    const target = screen.getByRole("combobox", { name: "Add selected assets to" });
    expect(target).toBeDisabled();
    expect(target).toHaveValue("7");
    expect(within(target).getAllByRole("option")).toHaveLength(3);
    expect(screen.queryByRole("searchbox")).not.toBeInTheDocument();
    expect(screen.getByText("Adding assets to Priority hire.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Return to Priority hire" })).toHaveAttribute(
      "href",
      "/ringfence?ringfenceId=7",
    );
    expect(onSelectedRingfenceChange).not.toHaveBeenCalled();
  });
});
