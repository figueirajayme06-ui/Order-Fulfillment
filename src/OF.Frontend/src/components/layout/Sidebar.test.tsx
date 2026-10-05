import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { Sidebar } from "./Sidebar";

const user = {
  loginName: "planner@example.com",
  displayName: "Fleet Planner",
  division: "110",
  isAdmin: false,
  isSuperAdmin: false,
  isReadOnly: false,
  language: "en",
};

describe("Sidebar", () => {
  afterEach(cleanup);

  it("provides accessible destination names and a collapse control", () => {
    render(
      <MemoryRouter>
        <Sidebar
          user={user}
          collapsed={false}
          mobileOpen={false}
          onToggleCollapsed={vi.fn()}
          onCloseMobile={vi.fn()}
          theme="light"
          onChangeTheme={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole("button", { name: "Collapse navigation" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Agreements" })).toHaveAttribute("href", "/agreements");
    expect(screen.getByRole("link", { name: "Assets" })).toHaveAttribute("href", "/assets");
    expect(screen.getByLabelText("Need help?")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Admin" })).not.toBeInTheDocument();
    expect(screen.getByLabelText("Data refreshes")).toBeInTheDocument();
    expect(screen.getByLabelText("0 unread alerts")).toBeInTheDocument();
    expect(screen.getByText("FP")).toBeInTheDocument();
    expect(screen.queryByText("110")).not.toBeInTheDocument();
  });

  it("retains destination names when collapsed", () => {
    render(
      <MemoryRouter>
        <Sidebar
          user={user}
          collapsed
          mobileOpen={false}
          onToggleCollapsed={vi.fn()}
          onCloseMobile={vi.fn()}
          theme="light"
          onChangeTheme={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole("button", { name: "Expand navigation" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ringfence" })).toHaveAttribute("title", "Ringfence");
    expect(screen.getByLabelText("Need help?")).toHaveAttribute("title", "Need help?");
  });

  it("opens a full-height help panel from the utility control", () => {
    render(
      <MemoryRouter>
        <Sidebar
          user={user}
          collapsed={false}
          mobileOpen={false}
          onToggleCollapsed={vi.fn()}
          onCloseMobile={vi.fn()}
          theme="light"
          onChangeTheme={vi.fn()}
        />
      </MemoryRouter>,
    );

    const helpControl = screen.getByLabelText("Need help?");
    fireEvent.click(helpControl);

    expect(helpControl.closest("details")).toHaveAttribute("open");
    expect(screen.getByRole("button", { name: "Close help" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Raise a NOF request" })).toHaveAttribute(
      "href",
      "https://aggreko.freshservice.com/support/catalog/items/250",
    );
    expect(screen.getByRole("link", { name: "User guides" })).toHaveAttribute(
      "href",
      "https://aggreko.sharepoint.com/:f:/r/sites/ConImp_CustomerExperienceManagement/Process%20Training%20Sessions/Master%20Training%20Decks/Order%20Fulfilment/New%20OF",
    );
  });

  it("prints the current page from the navigation", () => {
    const print = vi.spyOn(window, "print").mockImplementation(() => undefined);

    render(
      <MemoryRouter>
        <Sidebar
          user={user}
          collapsed={false}
          mobileOpen={false}
          onToggleCollapsed={vi.fn()}
          onCloseMobile={vi.fn()}
          theme="light"
          onChangeTheme={vi.fn()}
        />
      </MemoryRouter>,
    );

    screen.getByRole("button", { name: "Print current page" }).click();

    expect(print).toHaveBeenCalledOnce();
    print.mockRestore();
  });

  it("presents workspace cards on the homepage", () => {
    render(
      <MemoryRouter>
        <Sidebar
          user={user}
          isHome
          collapsed={false}
          mobileOpen={false}
          onToggleCollapsed={vi.fn()}
          onCloseMobile={vi.fn()}
          theme="light"
          onChangeTheme={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole("heading", { name: /Good to see you,\s*Fleet\./ })).toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: "Workspaces" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Collapse navigation" })).not.toBeInTheDocument();
  });

  it("shows read-only access and suppresses malformed admin access", () => {
    render(
      <MemoryRouter>
        <Sidebar
          user={{ ...user, isAdmin: true, isReadOnly: true }}
          collapsed={false}
          mobileOpen={false}
          onToggleCollapsed={vi.fn()}
          onCloseMobile={vi.fn()}
          theme="light"
          onChangeTheme={vi.fn()}
        />
      </MemoryRouter>,
    );

    expect(screen.getByText("Read only")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Admin" })).not.toBeInTheDocument();
  });
});
