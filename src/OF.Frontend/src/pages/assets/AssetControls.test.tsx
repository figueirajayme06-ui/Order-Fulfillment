import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import buttonStyles from "../../components/common/Button/Button.module.css";
import "../../i18n";
import { AssetControls, ASSET_STATUSES, type AssetControlsProps } from "./AssetControls";
import styles from "./AssetsPage.module.css";

function createProps(overrides: Partial<AssetControlsProps> = {}): AssetControlsProps {
  return {
    assetCount: 12,
    canManageRemovedStock: false,
    divisionLookups: [
      { code: "01", name: "North" },
      { code: "02", name: "South" },
      { code: "03", name: "East" },
    ],
    hideRemovedStock: true,
    hasActiveFilters: false,
    isSuperAdmin: false,
    savedViewControls: <div data-testid="saved-view-controls">Saved asset view controls</div>,
    searchTerm: "",
    selectedDivision: "01",
    statusFilter: "",
    userDivisionCodes: ["01"],
    viewMode: "table",
    onHideRemovedStockChange: vi.fn(),
    onRefresh: vi.fn(),
    onResetFilters: vi.fn(),
    onSearchTermChange: vi.fn(),
    onSelectedDivisionChange: vi.fn(),
    onStatusFilterChange: vi.fn(),
    onViewModeChange: vi.fn(),
    ...overrides,
  };
}

describe("AssetControls", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(cleanup);

  it("preserves the heading, count, saved-view slot, view controls, CSS, and print markers", () => {
    const onViewModeChange = vi.fn();
    const props = createProps({ onViewModeChange });
    const { container, rerender } = render(<AssetControls {...props} />);

    const header = screen.getByRole("banner");
    expect(header).toHaveClass(styles.header);
    expect(within(header).getByRole("heading", { name: "Assets" })).toBeInTheDocument();
    expect(within(header).getByText("12 assets")).toBeInTheDocument();
    expect(screen.getByTestId("saved-view-controls").parentElement).toHaveClass(styles.controlsPanel);
    expect(container.querySelectorAll("[data-print-hidden]")).toHaveLength(2);

    const tableButton = screen.getByRole("button", { name: "Table" });
    const timelineButton = screen.getByRole("button", { name: "Timeline" });
    expect(tableButton).toHaveClass(buttonStyles.primary);
    expect(timelineButton).toHaveClass(buttonStyles.secondary);
    fireEvent.click(tableButton);
    fireEvent.click(timelineButton);
    expect(onViewModeChange.mock.calls).toEqual([["table"], ["timeline"]]);

    rerender(<AssetControls {...props} viewMode="timeline" />);
    expect(screen.getByRole("button", { name: "Table" })).toHaveClass(buttonStyles.secondary);
    expect(screen.getByRole("button", { name: "Timeline" })).toHaveClass(buttonStyles.primary);
  });

  it("shows the removed-stock control only to authorized users and keeps it controlled", () => {
    const onHideRemovedStockChange = vi.fn();
    const props = createProps({ onHideRemovedStockChange });
    const { rerender } = render(<AssetControls {...props} />);

    expect(screen.queryByRole("checkbox", { name: "Hide removed/scrapped/sold" })).not.toBeInTheDocument();

    rerender(<AssetControls {...props} canManageRemovedStock />);
    const removedStock = screen.getByRole("checkbox", { name: "Hide removed/scrapped/sold" });
    expect(removedStock).toBeChecked();
    fireEvent.click(removedStock);
    expect(onHideRemovedStockChange).toHaveBeenCalledWith(false);
    expect(removedStock).toBeChecked();
  });

  it("applies search changes while typing and refreshes on Enter or from the visible action", () => {
    const onRefresh = vi.fn();
    const onSearchTermChange = vi.fn();
    const props = createProps({
      searchTerm: "ASSET-1",
      onRefresh,
      onSearchTermChange,
    });
    const { rerender } = render(<AssetControls {...props} />);
    const search = screen.getByPlaceholderText("Search");

    expect(search).toHaveValue("ASSET-1");
    expect(screen.queryByPlaceholderText("Warehouse / location")).not.toBeInTheDocument();
    fireEvent.change(search, { target: { value: "ITEM-2" } });
    fireEvent.keyDown(search, { key: "ArrowDown" });
    expect(onSearchTermChange).toHaveBeenCalledWith("ITEM-2");
    expect(onRefresh).not.toHaveBeenCalled();
    expect(search).toHaveValue("ASSET-1");

    fireEvent.keyDown(search, { key: "Enter" });
    expect(onRefresh).toHaveBeenCalledOnce();

    fireEvent.click(screen.getByRole("button", { name: "Refresh" }));
    expect(onRefresh).toHaveBeenCalledTimes(2);

    rerender(<AssetControls {...props} searchTerm="ITEM-2" />);
    expect(search).toHaveValue("ITEM-2");
  });

  it("preserves division choices for single, multi-division, and super-admin users", () => {
    const onSelectedDivisionChange = vi.fn();
    const props = createProps({ onSelectedDivisionChange });
    const { rerender } = render(<AssetControls {...props} />);

    let divisionFilter = screen.getByRole("button", { name: "Filter Division: 01 — North" });
    fireEvent.click(divisionFilter);
    let divisionOptions = within(screen.getByRole("dialog", { name: "Options for Division" })).getAllByRole("checkbox");
    expect(divisionOptions.map((option) => option.parentElement?.textContent)).toEqual(["01 — North"]);
    expect(divisionOptions[0]).toBeDisabled();
    fireEvent.click(divisionFilter);

    rerender(<AssetControls {...props} selectedDivision="" userDivisionCodes={["01", "03"]} />);
    divisionFilter = screen.getByRole("button", { name: "Filter Division: All" });
    fireEvent.click(divisionFilter);
    divisionOptions = within(screen.getByRole("dialog", { name: "Options for Division" })).getAllByRole("checkbox");
    expect(divisionOptions.map((option) => option.parentElement?.textContent)).toEqual(["01 — North", "03 — East"]);
    fireEvent.click(screen.getByRole("checkbox", { name: "03 — East" }));
    expect(onSelectedDivisionChange).toHaveBeenCalledWith("03");
    fireEvent.click(divisionFilter);

    rerender(<AssetControls {...props} isSuperAdmin selectedDivision="" userDivisionCodes={["01"]} />);
    divisionFilter = screen.getByRole("button", { name: "Filter Division: All" });
    fireEvent.click(divisionFilter);
    divisionOptions = within(screen.getByRole("dialog", { name: "Options for Division" })).getAllByRole("checkbox");
    expect(divisionOptions.map((option) => option.parentElement?.textContent)).toEqual([
      "01 — North",
      "02 — South",
      "03 — East",
    ]);
  });

  it("preserves the exact status options and controlled callback", () => {
    const onStatusFilterChange = vi.fn();
    render(<AssetControls {...createProps({ statusFilter: "Repair", onStatusFilterChange })} />);
    fireEvent.click(screen.getByRole("button", { name: "Filter Status: Repair" }));
    expect(
      within(screen.getByRole("dialog", { name: "Options for Status" }))
        .getAllByRole("checkbox")
        .map((option) => option.parentElement?.textContent),
    ).toEqual([...ASSET_STATUSES]);
    fireEvent.click(screen.getByRole("checkbox", { name: "Sold" }));
    expect(onStatusFilterChange).toHaveBeenCalledWith("Repair,Sold");
  });

  it("does not show More filters and enables Reset filters only when filters are active", () => {
    const onResetFilters = vi.fn();
    const props = createProps({ onResetFilters });
    const { rerender } = render(<AssetControls {...props} />);

    expect(screen.queryByRole("button", { name: /More filters/ })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reset filters" })).toBeDisabled();

    rerender(<AssetControls {...props} hasActiveFilters />);
    const reset = screen.getByRole("button", { name: "Reset filters" });
    expect(reset).toBeEnabled();
    expect(reset.parentElement).toHaveClass(styles.resetFiltersAction);
    fireEvent.click(reset);
    expect(onResetFilters).toHaveBeenCalledOnce();
  });
});
