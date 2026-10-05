import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { TableColumnsMenu } from "../../components/common";
import i18n from "../../i18n";
import {
  repairTableColumnLayout,
  setTableColumnVisibility,
  type TableColumnLayoutItem,
} from "../../lib/tableColumnLayout";
import type { AgreementListItem } from "../../services/agreementsService";
import { ApiFulfilmentStatus } from "../../types";
import { AGREEMENT_COLUMN_CATALOG, type AgreementColumnKey } from "./agreementColumnCatalog";
import type { AgreementsTableProps } from "./AgreementsTable";
import { AgreementsTable, formatDate } from "./AgreementsTable";
import styles from "./AgreementsPage.module.css";

function createAgreement(id: number, overrides: Partial<AgreementListItem> = {}): AgreementListItem {
  return {
    id,
    agreementNumber: `A-${id}`,
    customerName: `Customer ${id}`,
    customerNumber: `C-${id}`,
    division: "01",
    warehouse: "GLA",
    fulfilmentStatus: ApiFulfilmentStatus.Unfulfilled,
    onHireDate: "2026-08-01T12:00:00.000Z",
    offHireDate: "2026-08-31T12:00:00.000Z",
    isDeleted: false,
    orderSource: "D365",
    lineCount: 2,
    deliveryDate: "2026-08-02T12:00:00.000Z",
    validFromDate: "2026-08-03T12:00:00.000Z",
    validToDate: "2026-08-30T12:00:00.000Z",
    terminationDate: "2026-08-29T12:00:00.000Z",
    collectionDate: "2026-09-01T12:00:00.000Z",
    customerAddress: "1 Test Street",
    lastUpdatedByName: "Alex Developer",
    opportunityName: `Opportunity ${id}`,
    ...overrides,
  };
}

function createProps(overrides: Partial<AgreementsTableProps> = {}): AgreementsTableProps {
  return {
    agreements: [createAgreement(1)],
    columnFilters: {},
    columnLayout: repairTableColumnLayout(AGREEMENT_COLUMN_CATALOG),
    currentPage: 1,
    divisionFilter: "110",
    divisionOptions: [
      { label: "110", value: "110" },
      { label: "200", value: "200" },
    ],
    pageSize: 50,
    showAllDivisionOption: true,
    sortDirection: "desc",
    sortField: "onHireDate",
    statusFilter: "",
    totalPages: 1,
    warehouseOptions: [
      { label: "CN1", value: "CN1" },
      { label: "ED1", value: "ED1" },
    ],
    onColumnFilterChange: vi.fn(),
    onColumnLayoutChange: vi.fn(),
    onDateColumnFilterChange: vi.fn(),
    onDivisionFilterChange: vi.fn(),
    onNextPage: vi.fn(),
    onOrderNumberClick: vi.fn(),
    onPageSizeChange: vi.fn(),
    onPreviousPage: vi.fn(),
    onRowClick: vi.fn(),
    onSort: vi.fn(),
    onStatusFilterChange: vi.fn(),
    ...overrides,
  };
}

describe("AgreementsTable", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage("en");
  });

  afterEach(cleanup);

  it("offers every legacy business column and keeps additions optional", () => {
    const legacyKeys: AgreementColumnKey[] = [
      "fromDate",
      "toDate",
      "customerNumber",
      "customerAddress",
      "lastUpdatedDate",
      "opportunityStage",
      "probability",
    ];

    for (const key of legacyKeys) {
      expect(AGREEMENT_COLUMN_CATALOG.find((column) => column.key === key)).toMatchObject({ defaultVisible: false });
    }

    expect(AGREEMENT_COLUMN_CATALOG.map((column) => column.key)).not.toContain("maxFulfilmentStatus");
    expect(AGREEMENT_COLUMN_CATALOG.map((column) => column.key)).not.toContain("minFulfilmentStatus");
  });

  it("shows a note count only when an Agreement has notes", () => {
    render(
      <AgreementsTable
        {...createProps({ agreements: [createAgreement(1, { noteCount: 2 }), createAgreement(2, { noteCount: 0 })] })}
      />,
    );

    expect(screen.getByLabelText("2 notes")).toHaveAttribute("title", "2 notes");
    expect(screen.queryByLabelText("0 notes")).not.toBeInTheDocument();
  });

  it("shows only the customer name in each summary row", () => {
    render(
      <AgreementsTable
        {...createProps({
          agreements: [createAgreement(1, { customerName: "Barhale Ltd", customerNumber: "GB00000138" })],
        })}
      />,
    );

    expect(screen.getByText("Barhale Ltd")).toBeInTheDocument();
    expect(screen.queryByText("GB00000138")).not.toBeInTheDocument();
  });

  it("renders authoritative raw fulfilment statuses 0, 1, 2, and 3", () => {
    const agreements = [
      createAgreement(1, { fulfilmentStatus: ApiFulfilmentStatus.Unfulfilled }),
      createAgreement(2, { fulfilmentStatus: ApiFulfilmentStatus.PartiallyFulfilled }),
      createAgreement(3, { fulfilmentStatus: ApiFulfilmentStatus.Overfulfilled }),
      createAgreement(4, { fulfilmentStatus: ApiFulfilmentStatus.FullyFulfilled }),
    ];

    render(<AgreementsTable {...createProps({ agreements })} />);

    const expectedLabels = ["Unfulfilled", "Partially Fulfilled", "Unknown", "Fully Fulfilled"];
    agreements.forEach((agreement, index) => {
      const agreementCell = screen.getByText(agreement.agreementNumber!);
      const row = agreementCell.closest("tr");

      expect(row).not.toBeNull();
      expect(within(row!).getByLabelText(expectedLabels[index])).toBeInTheDocument();
    });
  });

  it("keeps column filters available when the current filter has no matching agreements", () => {
    render(<AgreementsTable {...createProps({ agreements: [] })} />);

    expect(screen.getByText("No results found")).toBeInTheDocument();
    expect(screen.getByLabelText("Filter Agreement No.")).toBeInTheDocument();
    expect(screen.getByLabelText("Filter Customer")).toBeInTheDocument();
  });

  it("keeps row navigation isolated from the expand control", async () => {
    const user = userEvent.setup();
    const onRowClick = vi.fn();

    render(<AgreementsTable {...createProps({ onRowClick })} />);

    const row = screen.getByText("A-1").closest("tr");
    expect(row).not.toBeNull();

    await user.click(row!);
    expect(onRowClick).toHaveBeenCalledWith(1);
  });

  it("links the agreement number directly to its timeline page", async () => {
    const user = userEvent.setup();
    const onOrderNumberClick = vi.fn();
    const onRowClick = vi.fn();

    render(<AgreementsTable {...createProps({ onOrderNumberClick, onRowClick })} />);

    const orderNumberLink = screen.getByRole("link", { name: "A-1" });
    expect(orderNumberLink).toHaveAttribute("href", "/agreements/1/timeline");

    await user.click(orderNumberLink);
    expect(onOrderNumberClick).toHaveBeenCalledTimes(1);
    expect(onOrderNumberClick).toHaveBeenCalledWith(1);
    expect(onRowClick).not.toHaveBeenCalled();
  });

  it("shows operational values in the row and omits the unneeded customer address", () => {
    const agreement = createAgreement(7, {
      warehouse: "MAN",
      deliveryDate: "2026-08-05T12:00:00.000Z",
      validFromDate: "2026-08-06T12:00:00.000Z",
      validToDate: "2026-08-27T12:00:00.000Z",
      terminationDate: null,
      collectionDate: "2026-09-02T12:00:00.000Z",
      customerAddress: "7 Fulfilment Way",
      lastUpdatedByName: "Jamie Engineer",
      lineCount: 9,
    });

    render(<AgreementsTable {...createProps({ agreements: [agreement] })} />);

    const summaryRow = screen.getByText("A-7").closest("tr");
    expect(summaryRow).not.toBeNull();

    const expectedSummaryValues = [
      "MAN",
      formatDate(agreement.deliveryDate),
      formatDate(agreement.validFromDate),
      formatDate(agreement.validToDate),
      formatDate(agreement.collectionDate),
      "Jamie Engineer",
      "9",
    ];
    expectedSummaryValues.forEach((value) => expect(within(summaryRow!).getByText(value)).toBeInTheDocument());

    expect(screen.queryByText("Customer Address")).not.toBeInTheDocument();
    expect(screen.queryByText("7 Fulfilment Way")).not.toBeInTheDocument();
  });

  it("maps every sort and column-filter control to its field callback", async () => {
    const onSort = vi.fn();
    const onColumnFilterChange = vi.fn();
    const onDivisionFilterChange = vi.fn();
    const onStatusFilterChange = vi.fn();

    const props = createProps({ onSort, onColumnFilterChange, onDivisionFilterChange, onStatusFilterChange });
    const { rerender } = render(<AgreementsTable {...props} />);

    const sortButtons = screen.getAllByRole("button", { name: /^Sort by / });
    expect(sortButtons.map((button) => button.getAttribute("aria-label"))).toEqual([
      "Sort by Status, currently not sorted",
      "Sort by Agreement No., currently not sorted",
      "Sort by Customer, currently not sorted",
      "Sort by Opportunity, currently not sorted",
      "Sort by DIV, currently not sorted",
      "Sort by WHS, currently not sorted",
      "Sort by Delivery Date, currently not sorted",
      "Sort by Valid From, currently not sorted",
      "Sort by Valid To, currently not sorted",
      "Sort by Termination Date, currently not sorted",
      "Sort by Collection Date, currently not sorted",
      "Sort by Last Updated By, currently not sorted",
      "Sort by Lines, currently not sorted",
    ]);
    sortButtons.forEach((button) => fireEvent.click(button));

    expect(onSort.mock.calls).toEqual([
      ["fulfilmentStatus"],
      ["agreementNumber"],
      ["customerName"],
      ["opportunityName"],
      ["division"],
      ["warehouse"],
      ["deliveryDate"],
      ["validFromDate"],
      ["validToDate"],
      ["terminationDate"],
      ["collectionDate"],
      ["lastUpdatedByName"],
      ["lineCount"],
    ]);

    fireEvent.click(screen.getByRole("button", { name: "Filter Status: All" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Partially Fulfilled" }));
    fireEvent.change(screen.getByLabelText("Filter Agreement No."), { target: { value: "A-10" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Agreement No."), { key: "Enter" });
    fireEvent.change(screen.getByLabelText("Filter Customer"), { target: { value: "North" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Customer"), { key: "Enter" });
    fireEvent.change(screen.getByLabelText("Filter Opportunity"), { target: { value: "Renewal" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Opportunity"), { key: "Enter" });
    fireEvent.click(screen.getByRole("button", { name: "Filter DIV: 110" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "200" }));
    fireEvent.click(screen.getByRole("button", { name: "Filter WHS: All" }));
    fireEvent.click(screen.getByRole("checkbox", { name: "CN1" }));
    fireEvent.change(screen.getByLabelText("Filter Last Updated By"), { target: { value: "Jamie" } });
    fireEvent.keyDown(screen.getByLabelText("Filter Last Updated By"), { key: "Enter" });
    fireEvent.change(screen.getByLabelText("Filter Lines"), { target: { value: "9" } });

    expect(onColumnFilterChange.mock.calls).toEqual([
      ["agreementNumber", ["A-10"]],
      ["customerName", ["North"]],
      ["opportunityName", ["Renewal"]],
      ["warehouse", ["CN1"]],
      ["lastUpdatedByName", ["Jamie"]],
      ["lineCount", "9"],
    ]);
    expect(onStatusFilterChange).toHaveBeenCalledWith("1");

    fireEvent.change(screen.getByLabelText("Filter Delivery Date"), { target: { value: "2026-08-05" } });
    expect(props.onDateColumnFilterChange).toHaveBeenCalledWith("deliveryDate", {
      operator: "on",
      value: "2026-08-05",
    });
    const user = userEvent.setup();
    await user.selectOptions(screen.getByLabelText("More date options for Delivery Date"), "today");
    expect(props.onDateColumnFilterChange).toHaveBeenCalledWith("deliveryDate", { operator: "today" });

    expect(onDivisionFilterChange).toHaveBeenCalledWith("110,200");
    rerender(<AgreementsTable {...props} divisionFilter="110,200" statusFilter="0,1" />);
    expect(screen.getByRole("button", { name: "Filter DIV: 2 selected" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Filter Status: 2 selected" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Filter WHS: All" }));
    expect(
      within(screen.getByRole("dialog", { name: "Options for WHS" }))
        .getAllByRole("checkbox")
        .map((option) => option.parentElement?.textContent),
    ).toEqual(["CN1", "ED1"]);
  });

  it("keeps Agreement headers, rows, filters, and colgroups aligned after configuration and resize", async () => {
    const user = userEvent.setup();
    function Harness() {
      const [layout, setLayout] = useState<TableColumnLayoutItem<AgreementColumnKey>[]>(() =>
        repairTableColumnLayout(AGREEMENT_COLUMN_CATALOG),
      );
      return (
        <>
          <TableColumnsMenu
            catalogue={AGREEMENT_COLUMN_CATALOG}
            layout={layout}
            onChange={setLayout}
            onVisibilityChange={(key, visible) =>
              setLayout((current) => setTableColumnVisibility(AGREEMENT_COLUMN_CATALOG, current, key, visible))
            }
          />
          <AgreementsTable {...createProps()} columnLayout={layout} onColumnLayoutChange={setLayout} />
        </>
      );
    }
    const { container } = render(<Harness />);
    await user.click(screen.getByRole("button", { name: "Columns, 13 visible" }));
    expect(screen.getByRole("dialog", { name: "Configure columns" })).toContainElement(
      document.activeElement as HTMLElement | null,
    );
    await user.click(screen.getByRole("button", { name: "Move Opportunity up" }));
    await user.click(screen.getByRole("checkbox", { name: "Last Updated By" }));
    const resize = screen.getByRole("separator", { name: "Resize Opportunity column" });
    resize.focus();
    await user.keyboard("{ArrowRight}");

    const headers = screen.getAllByRole("button", { name: /^Sort by / }).map((button) => button.textContent);
    expect(headers.slice(0, 4)).toEqual([
      expect.stringContaining("Status"),
      expect.stringContaining("Agreement No."),
      expect.stringContaining("Opportunity"),
      expect.stringContaining("Customer"),
    ]);
    expect(screen.queryByLabelText("Filter Last Updated By")).not.toBeInTheDocument();
    const row = screen.getByText("A-1").closest("tr")!;
    expect(
      Array.from(row.cells)
        .slice(1, 4)
        .map((cell) => cell.textContent),
    ).toEqual(["A-1", "Opportunity 1", "Customer 1"]);
    const colgroups = container.querySelectorAll("colgroup");
    expect(colgroups[0].innerHTML).toBe(colgroups[1].innerHTML);
    const printWidths = Array.from(colgroups[0].querySelectorAll("col[data-column-key]"), (column) =>
      Number.parseFloat((column as HTMLElement).style.getPropertyValue("--print-column-width")),
    );
    expect(printWidths.reduce((total, width) => total + width, 0)).toBeCloseTo(100, 4);
    expect(resize).toHaveAttribute("aria-valuenow", "165");
  });

  it("keeps column filters editable and shows the empty state inside the table when no agreements match", () => {
    const onColumnFilterChange = vi.fn();

    render(
      <AgreementsTable
        {...createProps({
          agreements: [],
          columnFilters: { opportunityName: ["dhuvbad"] },
          onColumnFilterChange,
          totalPages: 0,
        })}
      />,
    );

    const emptyCell = screen.getByText("No results found").closest("td");
    expect(emptyCell).toHaveAttribute("colspan", "13");
    expect(screen.getByText("dhuvbad")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Clear all Opportunity filter values" }));
    expect(onColumnFilterChange).toHaveBeenCalledWith("opportunityName", []);
  });

  it("preserves pagination copy, callbacks, and disabled states", async () => {
    const user = userEvent.setup();
    const onNextPage = vi.fn();
    const onPageSizeChange = vi.fn();
    const onPreviousPage = vi.fn();
    const firstPageProps = createProps({
      currentPage: 1,
      totalPages: 3,
      onNextPage,
      onPageSizeChange,
      onPreviousPage,
    });
    const { rerender } = render(<AgreementsTable {...firstPageProps} />);

    expect(screen.getByText("Page 1 of 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "← Prev" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Next →" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "Next →" }));
    expect(onNextPage).toHaveBeenCalledOnce();
    expect(onPreviousPage).not.toHaveBeenCalled();

    rerender(<AgreementsTable {...firstPageProps} currentPage={3} />);
    expect(screen.getByText("Page 3 of 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "← Prev" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Next →" })).toBeDisabled();

    await user.click(screen.getByRole("button", { name: "← Prev" }));
    expect(onPreviousPage).toHaveBeenCalledOnce();

    const pageSizeSelect = screen.getByRole("combobox", { name: "Rows per page" });
    expect(pageSizeSelect).toHaveValue("50");
    expect(
      within(pageSizeSelect)
        .getAllByRole("option")
        .map((option) => option.textContent),
    ).toEqual(["50", "250", "500", "1000"]);
    await user.selectOptions(pageSizeSelect, "250");
    expect(onPageSizeChange).toHaveBeenCalledWith(250);

    rerender(<AgreementsTable {...firstPageProps} currentPage={1} totalPages={1} />);
    expect(screen.getByText("Page 1 of 1")).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Rows per page" })).toBeInTheDocument();
  });

  it("retains the agreement print marker and hides interactive table controls from print", () => {
    const { container } = render(<AgreementsTable {...createProps({ totalPages: 2 })} />);

    expect(container.querySelector('[data-print-table="agreements"]')).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Agreement rows" })).toHaveAttribute("tabindex", "0");
    expect(container.querySelector("thead [data-print-hidden]")).toBeInTheDocument();
    expect(container.querySelector('[data-print-table="agreements"] [data-print-hidden]')).toBeInTheDocument();
    expect((container.querySelector(`.${styles.tableSurface}`) as HTMLElement).style.minWidth).toBe("1118px");
    expect(container.querySelectorAll("col[data-column-key]")).toHaveLength(
      AGREEMENT_COLUMN_CATALOG.filter((column) => column.defaultVisible).length * 2,
    );
  });
});
