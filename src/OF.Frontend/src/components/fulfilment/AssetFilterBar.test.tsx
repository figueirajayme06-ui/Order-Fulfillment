import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { AssetFilterBar } from "./AssetFilterBar";

afterEach(cleanup);

function setup(isLoading = false) {
  const onSearch = vi.fn();
  const onClear = vi.fn();
  render(
    <AssetFilterBar
      values={{ search: "", status: "", warehouse: "", division: "", itemNumber: "", description: "" }}
      onChange={vi.fn()}
      onSearch={onSearch}
      onClear={onClear}
      isLoading={isLoading}
    />,
  );
  return { user: userEvent.setup(), onSearch, onClear };
}

describe("AssetFilterBar", () => {
  it("submits once with Enter from a labelled field or the search button", async () => {
    const { user, onSearch } = setup();
    await user.click(screen.getByRole("textbox", { name: "Item number" }));
    await user.keyboard("{Enter}");
    expect(onSearch).toHaveBeenCalledTimes(1);
    await user.click(screen.getByRole("button", { name: "Search" }));
    expect(onSearch).toHaveBeenCalledTimes(2);
  });

  it("does not submit when clearing or while a search is pending", async () => {
    const { user, onSearch, onClear } = setup(true);
    await user.click(screen.getByRole("button", { name: "Clear" }));
    expect(onClear).toHaveBeenCalledOnce();
    await user.click(screen.getByRole("textbox", { name: "Description" }));
    await user.keyboard("{Enter}");
    expect(onSearch).not.toHaveBeenCalled();
  });
});
