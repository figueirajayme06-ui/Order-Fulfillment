import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { SearchableSelect } from "./SearchableSelect";

describe("SearchableSelect", () => {
  it("filters options by label or value and keeps selection behavior", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SearchableSelect
        ariaLabel="Warehouses"
        searchLabel="Search warehouses"
        placeholder="All warehouses"
        value=""
        options={[
          { label: "GLA — Glasgow", value: "GLA" },
          { label: "MAN — Manchester", value: "MAN" },
        ]}
        onChange={onChange}
      />,
    );

    await user.click(screen.getByRole("combobox", { name: "Warehouses" }));
    await user.type(screen.getByRole("combobox", { name: "Search warehouses" }), "man");
    expect(screen.queryByRole("option", { name: "GLA — Glasgow" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("option", { name: "MAN — Manchester" }));
    expect(onChange).toHaveBeenCalledWith("MAN");
    expect(screen.queryByRole("combobox", { name: "Search warehouses" })).not.toBeInTheDocument();
  });
});
