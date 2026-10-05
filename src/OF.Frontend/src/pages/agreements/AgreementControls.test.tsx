import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import buttonStyles from "../../components/common/Button/Button.module.css";
import i18n from "../../i18n";
import type { AgreementControlsProps } from "./AgreementControls";
import { AgreementControls } from "./AgreementControls";
import styles from "./AgreementsPage.module.css";

function createProps(overrides: Partial<AgreementControlsProps> = {}): AgreementControlsProps {
  return {
    agreementCount: 12,
    divisionLookups: [
      { code: "01", name: "North" },
      { code: "02", name: "South" },
      { code: "03", name: "East" },
    ],
    hasActiveFilters: false,
    isReadOnly: false,
    isSuperAdmin: false,
    orderTypeFilter: "",
    savedViewControls: <div data-testid="saved-view-controls">Saved view controls</div>,
    searchTerm: "",
    selectedDivision: "01",
    showHistorical: false,
    statusFilter: "",
    userDivisionCodes: ["01"],
    viewMode: "table",
    onOrderTypeFilterChange: vi.fn(),
    onRefresh: vi.fn(),
    onResetFilters: vi.fn(),
    onSearchTermChange: vi.fn(),
    onSelectedDivisionChange: vi.fn(),
    onShowHistoricalChange: vi.fn(),
    onStatusFilterChange: vi.fn(),
    onViewModeChange: vi.fn(),
    ...overrides,
  };
}

describe("AgreementControls", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage("en");
  });

  afterEach(cleanup);

  it("preserves the page heading, historical and view controls, CSS, and print markers", () => {
    const onShowHistoricalChange = vi.fn();
    const onViewModeChange = vi.fn();
    const props = createProps({
      showHistorical: true,
      onShowHistoricalChange,
      onViewModeChange,
    });
    const { container, rerender } = render(<AgreementControls {...props} />);

    const header = screen.getByRole("banner");
    expect(header).toHaveClass(styles.header);
    expect(within(header).getByRole("heading", { name: "Agreements" })).toBeInTheDocument();
    expect(within(header).getByText("12 agreements")).toBeInTheDocument();
    expect(within(header).getByRole("textbox", { name: "Quote or agreement number" })).toBeInTheDocument();
    expect(within(header).getByRole("button", { name: "Pull quote or agreement" })).toBeDisabled();
    expect(screen.getByTestId("saved-view-controls").parentElement).toHaveClass(styles.controlsPanel);
    expect(container.querySelectorAll("[data-print-hidden]")).toHaveLength(2);
    expect(screen.queryByRole("button", { name: /More filters/ })).not.toBeInTheDocument();

    const historical = screen.getByRole("checkbox", { name: "Show historical agreements" });
    expect(historical).toBeChecked();
    fireEvent.click(historical);
    expect(onShowHistoricalChange).toHaveBeenCalledWith(false);

    const tableButton = screen.getByRole("button", { name: "Table" });
    const timelineButton = screen.getByRole("button", { name: "Timeline" });
    expect(tableButton).toHaveClass(buttonStyles.primary);
    expect(timelineButton).toHaveClass(buttonStyles.secondary);
    fireEvent.click(tableButton);
    fireEvent.click(timelineButton);
    expect(onViewModeChange.mock.calls).toEqual([["table"], ["timeline"]]);

    rerender(<AgreementControls {...props} viewMode="timeline" />);
    expect(screen.getByRole("button", { name: "Table" })).toHaveClass(buttonStyles.secondary);
    expect(screen.getByRole("button", { name: "Timeline" })).toHaveClass(buttonStyles.primary);
  });

  it("keeps search controlled and refreshes on Enter or from the visible action", () => {
    const onRefresh = vi.fn();
    const onSearchTermChange = vi.fn();
    const props = createProps({ searchTerm: "Northwind", onRefresh, onSearchTermChange });
    const { rerender } = render(<AgreementControls {...props} />);
    const search = screen.getByPlaceholderText("Search");

    expect(search).toHaveValue("Northwind");
    fireEvent.change(search, { target: { value: "Renewal" } });
    fireEvent.keyDown(search, { key: "ArrowDown" });
    expect(onSearchTermChange).toHaveBeenCalledWith("Renewal");
    expect(onRefresh).not.toHaveBeenCalled();
    expect(search).toHaveValue("Northwind");

    fireEvent.keyDown(search, { key: "Enter" });
    expect(onRefresh).toHaveBeenCalledOnce();

    fireEvent.click(screen.getByRole("button", { name: "Refresh" }));
    expect(onRefresh).toHaveBeenCalledTimes(2);

    rerender(<AgreementControls {...props} searchTerm="Renewal" />);
    expect(screen.getByPlaceholderText("Search")).toHaveValue("Renewal");
  });

  it("preserves division visibility for single, multi-division, and super-admin users", () => {
    const onSelectedDivisionChange = vi.fn();
    const props = createProps({ onSelectedDivisionChange });
    const { rerender } = render(<AgreementControls {...props} />);

    let divisionFilter = screen.getByRole("button", { name: "Filter Division: 01 — North" });
    fireEvent.click(divisionFilter);
    let divisionOptions = within(screen.getByRole("dialog", { name: "Options for Division" })).getAllByRole("checkbox");
    expect(
      divisionOptions.map((option) => option.getAttribute("aria-label") ?? option.parentElement?.textContent),
    ).toEqual(["01 — North"]);
    expect(divisionOptions[0]).toBeChecked();
    expect(divisionOptions[0]).toBeDisabled();
    fireEvent.click(divisionFilter);

    rerender(<AgreementControls {...props} selectedDivision="" userDivisionCodes={["01", "03"]} />);
    divisionFilter = screen.getByRole("button", { name: "Filter Division: All" });
    fireEvent.click(divisionFilter);
    divisionOptions = within(screen.getByRole("dialog", { name: "Options for Division" })).getAllByRole("checkbox");
    expect(divisionOptions.map((option) => option.parentElement?.textContent)).toEqual(["01 — North", "03 — East"]);
    fireEvent.click(screen.getByRole("checkbox", { name: "03 — East" }));
    expect(onSelectedDivisionChange).toHaveBeenCalledWith("03");
    fireEvent.click(divisionFilter);

    rerender(<AgreementControls {...props} isSuperAdmin selectedDivision="" userDivisionCodes={["01"]} />);
    divisionFilter = screen.getByRole("button", { name: "Filter Division: All" });
    fireEvent.click(divisionFilter);
    divisionOptions = within(screen.getByRole("dialog", { name: "Options for Division" })).getAllByRole("checkbox");
    expect(divisionOptions.map((option) => option.parentElement?.textContent)).toEqual([
      "01 — North",
      "02 — South",
      "03 — East",
    ]);
  });

  it("preserves order types and only the supported raw status options 0, 1, and 3", () => {
    const onOrderTypeFilterChange = vi.fn();
    const onStatusFilterChange = vi.fn();
    render(
      <AgreementControls
        {...createProps({ orderTypeFilter: "quote", statusFilter: "1", onOrderTypeFilterChange, onStatusFilterChange })}
      />,
    );
    const orderTypeFilter = screen.getByRole("button", { name: "Filter Order Type: Salesforce Quote (Q)" });
    fireEvent.click(orderTypeFilter);
    expect(
      within(screen.getByRole("dialog", { name: "Options for Order Type" }))
        .getAllByRole("checkbox")
        .map((option) => option.parentElement?.textContent),
    ).toEqual(["Salesforce Quote (Q)", "M3 Temporary Agreement (T)", "M3 Agreement (A)"]);
    fireEvent.click(screen.getByRole("checkbox", { name: "M3 Temporary Agreement (T)" }));
    expect(onOrderTypeFilterChange).toHaveBeenCalledWith("quote,temporaryAgreement");
    fireEvent.click(orderTypeFilter);

    const statusFilter = screen.getByRole("button", { name: "Filter Fulfilment Status: Partially Fulfilled" });
    fireEvent.click(statusFilter);
    expect(
      within(screen.getByRole("dialog", { name: "Options for Fulfilment Status" }))
        .getAllByRole("checkbox")
        .map((option) => option.parentElement?.textContent),
    ).toEqual(["Unfulfilled", "Partially Fulfilled", "Fully Fulfilled"]);
    fireEvent.click(screen.getByRole("checkbox", { name: "Fully Fulfilled" }));
    expect(onStatusFilterChange).toHaveBeenCalledWith("1,3");
  });

  it("enables reset only when filters are active and forwards the reset action", () => {
    const onResetFilters = vi.fn();
    const props = createProps({ onResetFilters });
    const { rerender } = render(<AgreementControls {...props} />);

    expect(screen.getByRole("button", { name: "Reset filters" })).toBeDisabled();

    rerender(<AgreementControls {...props} hasActiveFilters />);
    const resetButton = screen.getByRole("button", { name: "Reset filters" });
    expect(resetButton).toBeEnabled();
    fireEvent.click(resetButton);
    expect(onResetFilters).toHaveBeenCalledOnce();
  });
});
