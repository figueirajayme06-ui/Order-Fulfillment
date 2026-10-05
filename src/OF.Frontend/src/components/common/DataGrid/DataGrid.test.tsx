import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import "../../../i18n";
import { repairTableColumnLayout, type TableColumnDefinition } from "../../../lib/tableColumnLayout";
import { DataGridColumnGroup, DataGridColumnHeaders, DataGridPagination } from "./DataGrid";

const catalogue = [
  { key: "code", labelKey: "code", defaultVisible: true, defaultWidth: 100, minWidth: 80, maxWidth: 180 },
  { key: "name", labelKey: "name", defaultVisible: true, defaultWidth: 200, minWidth: 120, maxWidth: 320 },
] as const satisfies readonly TableColumnDefinition[];

describe("DataGrid primitives", () => {
  it("renders neutral column metadata and reports preferred width changes", () => {
    const layout = repairTableColumnLayout(catalogue);
    const onLayoutChange = vi.fn();
    const { container } = render(
      <table>
        <DataGridColumnGroup
          columns={layout}
          preferredColumns={layout}
          leadingColumns={[{ key: "selection", width: 32, printHidden: true }]}
        />
        <thead>
          <tr>
            <DataGridColumnHeaders
              catalogue={catalogue}
              columns={layout}
              layout={layout}
              getLabel={(definition) => definition.labelKey}
              renderHeader={(_column, _definition, label) => label}
              onLayoutChange={onLayoutChange}
            />
          </tr>
        </thead>
      </table>,
    );

    expect(container.querySelector("col[data-print-hidden]")).toHaveStyle({ width: "32px" });
    expect(container.querySelector('col[data-column-key="name"]')).toHaveStyle({ width: "200px" });
    fireEvent.keyDown(screen.getByRole("separator", { name: "Resize code column" }), { key: "ArrowRight" });
    expect(onLayoutChange).toHaveBeenCalledWith([
      { key: "code", visible: true, width: 105 },
      { key: "name", visible: true, width: 200 },
    ]);
  });

  it("renders controlled pagination without owning application state", () => {
    const onNextPage = vi.fn();
    const onPreviousPage = vi.fn();
    const onPageSizeChange = vi.fn();
    render(
      <DataGridPagination
        currentPage={2}
        totalPages={3}
        pageSize={50}
        labels={{
          previous: "Previous",
          next: "Next",
          page: (current, total) => `Page ${current} of ${total}`,
          pageSize: "Rows per page",
        }}
        onNextPage={onNextPage}
        onPreviousPage={onPreviousPage}
        onPageSizeChange={onPageSizeChange}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Previous" }));
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    fireEvent.change(screen.getByRole("combobox", { name: "Rows per page" }), { target: { value: "250" } });

    expect(onPreviousPage).toHaveBeenCalledOnce();
    expect(onNextPage).toHaveBeenCalledOnce();
    expect(onPageSizeChange).toHaveBeenCalledWith(250);
  });
});
