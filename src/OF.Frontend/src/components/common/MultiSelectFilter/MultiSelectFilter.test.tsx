import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import "../../../i18n";
import { MultiSelectFilter } from "./MultiSelectFilter";

const options = [
  { label: "North", value: "01" },
  { label: "South", value: "02" },
  { label: "East", value: "03" },
];

describe("MultiSelectFilter", () => {
  afterEach(cleanup);

  it("selects multiple options in catalogue order and reports the closed summary", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const { rerender } = render(
      <MultiSelectFilter label="Division" options={options} values={["01"]} onChange={onChange} />,
    );

    await user.click(screen.getByRole("button", { name: "Filter Division: North" }));
    await user.click(screen.getByRole("checkbox", { name: "East" }));
    expect(onChange).toHaveBeenLastCalledWith(["01", "03"]);

    rerender(<MultiSelectFilter label="Division" options={options} values={["01", "03"]} onChange={onChange} />);
    expect(screen.getByRole("button", { name: "Filter Division: 2 selected" })).toHaveTextContent(
      "Division — 2 selected",
    );
  });

  it("clears selections and closes with Escape while returning focus", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<MultiSelectFilter label="Division" options={options} values={["01", "02"]} onChange={onChange} />);

    const trigger = screen.getByRole("button", { name: "Filter Division: 2 selected" });
    await user.click(trigger);
    await user.click(screen.getByRole("button", { name: "Clear all" }));
    expect(onChange).toHaveBeenCalledWith([]);

    fireEvent.keyDown(screen.getByRole("dialog", { name: "Options for Division" }), { key: "Escape" });
    expect(screen.queryByRole("dialog", { name: "Options for Division" })).not.toBeInTheDocument();
    expect(trigger).toHaveFocus();
  });

  it("does not allow the only required value to be cleared", async () => {
    const user = userEvent.setup();
    render(
      <MultiSelectFilter
        label="Division"
        options={options.slice(0, 1)}
        values={["01"]}
        allowEmpty={false}
        onChange={vi.fn()}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Filter Division: North" }));
    expect(screen.getByRole("checkbox", { name: "North" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Clear all" })).toBeDisabled();
  });

  it("filters searchable options by label or value", async () => {
    const user = userEvent.setup();
    render(<MultiSelectFilter label="Division" options={options} values={[]} searchable onChange={vi.fn()} />);

    await user.click(screen.getByRole("button", { name: "Filter Division: All" }));
    await user.type(screen.getByRole("searchbox", { name: "Search Division options" }), "03");

    expect(screen.getByRole("checkbox", { name: "East" })).toBeInTheDocument();
    expect(screen.queryByRole("checkbox", { name: "North" })).not.toBeInTheDocument();
  });
});
