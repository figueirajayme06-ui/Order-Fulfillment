import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { ThemeToggle } from "./ThemeToggle";

describe("ThemeToggle", () => {
  afterEach(cleanup);

  it("exposes the available themes and requests a theme change", () => {
    const onChange = vi.fn();
    render(<ThemeToggle theme="light" onChange={onChange} />);
    const selector = screen.getByRole("combobox", { name: "Theme" });

    expect(selector).toHaveValue("light");
    expect(screen.getByRole("option", { name: /ATS theme/ })).toBeInTheDocument();
    fireEvent.change(selector, { target: { value: "ats" } });
    expect(onChange).toHaveBeenCalledWith("ats");
  });
});
