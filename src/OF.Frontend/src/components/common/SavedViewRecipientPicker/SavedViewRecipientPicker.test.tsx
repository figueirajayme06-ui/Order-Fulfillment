import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import "../../../i18n";
import { SavedViewRecipientPicker } from "./SavedViewRecipientPicker";

const candidates = [
  { loginName: "alex@aggreko.com", fullName: "Alex Smith" },
  { loginName: "sam@aggreko.com", fullName: "Sam Jones" },
];

describe("SavedViewRecipientPicker", () => {
  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it("stays idle until search, requests matches, adds a recipient, and exposes removable chips", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    const onSearch = vi.fn();
    const { rerender } = render(
      <SavedViewRecipientPicker
        candidates={candidates}
        error={null}
        isLoading={false}
        selected={[]}
        onCancel={vi.fn()}
        onChange={onChange}
        onSearch={onSearch}
        onRetry={vi.fn()}
      />,
    );

    const search = screen.getByRole("combobox", { name: "Share with people" });
    expect(screen.queryByRole("option", { name: /Alex Smith/ })).not.toBeInTheDocument();
    expect(screen.queryByText(/eligible people/i)).not.toBeInTheDocument();
    await user.type(search, "sam@");
    await waitFor(() => expect(onSearch).toHaveBeenLastCalledWith("sam@"));
    rerender(
      <SavedViewRecipientPicker
        candidates={[candidates[1]]}
        error={null}
        isLoading={false}
        selected={[]}
        onCancel={vi.fn()}
        onChange={onChange}
        onSearch={onSearch}
        onRetry={vi.fn()}
      />,
    );
    expect(screen.queryByRole("option", { name: /Alex Smith/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole("option", { name: /Sam Jones/ }));
    expect(onChange).toHaveBeenCalledWith([candidates[1]]);

    rerender(
      <SavedViewRecipientPicker
        candidates={candidates}
        error={null}
        isLoading={false}
        selected={[candidates[1]]}
        onCancel={vi.fn()}
        onChange={onChange}
        onSearch={onSearch}
        onRetry={vi.fn()}
      />,
    );
    await user.click(screen.getByRole("button", { name: "Remove Sam Jones" }));
    expect(onChange).toHaveBeenLastCalledWith([]);
    expect(search).toHaveFocus();
  });

  it("announces loading, empty, and retryable failure states", async () => {
    const user = userEvent.setup();
    const retry = vi.fn();
    const props = {
      candidates: [],
      selected: [],
      onCancel: vi.fn(),
      onChange: vi.fn(),
      onSearch: vi.fn(),
      onRetry: retry,
    };
    const { rerender } = render(<SavedViewRecipientPicker {...props} error={null} isLoading />);
    expect(screen.getByText("Loading eligible people…")).toBeInTheDocument();

    rerender(<SavedViewRecipientPicker {...props} error={null} isLoading={false} />);
    expect(screen.queryByText(/No eligible users are available for your divisions/)).not.toBeInTheDocument();

    rerender(<SavedViewRecipientPicker {...props} error="Unable to load eligible users." isLoading={false} />);
    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(retry).toHaveBeenCalledOnce();
  });

  it("focuses search on open and cancels with Escape", async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    render(
      <SavedViewRecipientPicker
        candidates={[]}
        error={null}
        isLoading={false}
        selected={[]}
        onCancel={onCancel}
        onChange={vi.fn()}
        onSearch={vi.fn()}
        onRetry={vi.fn()}
      />,
    );

    const search = screen.getByRole("combobox", { name: "Share with people" });
    expect(search).toHaveFocus();
    await user.keyboard("{Escape}");
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it("navigates results with arrow keys and selects with Enter", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SavedViewRecipientPicker
        candidates={candidates}
        error={null}
        isLoading={false}
        selected={[]}
        onCancel={vi.fn()}
        onChange={onChange}
        onSearch={vi.fn()}
        onRetry={vi.fn()}
      />,
    );

    const search = screen.getByRole("combobox", { name: "Share with people" });
    await user.type(search, "a");
    expect(search).toHaveAttribute("aria-activedescendant");
    await user.keyboard("{ArrowDown}{Enter}");
    expect(onChange).toHaveBeenCalledWith([candidates[1]]);
  });

  it("shows neutral recipient guidance until validation is requested", () => {
    const props = {
      candidates: [],
      error: null,
      isLoading: false,
      selected: [],
      onCancel: vi.fn(),
      onChange: vi.fn(),
      onSearch: vi.fn(),
      onRetry: vi.fn(),
    };
    const { rerender } = render(<SavedViewRecipientPicker {...props} />);

    const guidance = screen.getByText("Search for and select at least one person.");
    const search = screen.getByRole("combobox", { name: "Share with people" });
    expect(guidance).not.toHaveAttribute("role", "alert");
    expect(search).not.toHaveAttribute("aria-invalid");

    rerender(<SavedViewRecipientPicker {...props} showValidation />);
    expect(screen.getByRole("alert")).toHaveTextContent("Select at least one recipient.");
    expect(search).toHaveAttribute("aria-invalid", "true");
  });
});
