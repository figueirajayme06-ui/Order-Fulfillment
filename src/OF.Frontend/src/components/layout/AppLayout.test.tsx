import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { AppLayout } from "./AppLayout";
import { THEME_STORAGE_KEY } from "./themePreference";

const authMock = vi.hoisted(() => ({
  value: {
    user: {
      loginName: "planner@example.com",
      displayName: "Fleet Planner",
      division: "110",
      isAdmin: false,
      isSuperAdmin: false,
      isReadOnly: false,
      language: "en",
    },
    isLoading: false,
    error: null as string | null,
  },
}));

vi.mock("../../contexts/auth", () => ({
  useAuth: () => authMock.value,
}));

vi.mock("../../services/appConfigurationService", () => ({
  fetchAppConfiguration: vi.fn().mockResolvedValue({
    environmentLabel: "OF Dev",
    showPreviewBanner: false,
    legacyFrontendUrl: null,
  }),
}));

vi.mock("./GlobalActions", () => ({
  GlobalActions: (props: { showPull?: boolean; showUtilities?: boolean }) => (
    <div
      data-testid="global-actions"
      data-show-pull={String(props.showPull !== false)}
      data-show-utilities={String(props.showUtilities !== false)}
    />
  ),
}));

describe("AppLayout theme", () => {
  beforeEach(() => {
    window.localStorage.clear();
    delete document.documentElement.dataset.theme;
    authMock.value.error = null;
  });

  afterEach(cleanup);

  it("restores and applies a stored workspace theme", () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, "dark");

    renderLayout("/agreements");

    expect(screen.getByRole("combobox", { name: "Theme" })).toHaveValue("dark");
    expect(document.documentElement).toHaveAttribute("data-theme", "dark");
  });

  it("toggles and persists the workspace theme", () => {
    renderLayout("/assets");

    fireEvent.change(screen.getByRole("combobox", { name: "Theme" }), { target: { value: "dark" } });

    expect(document.documentElement).toHaveAttribute("data-theme", "dark");
    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe("dark");
  });

  it("keeps the switch out of the homepage", () => {
    renderLayout("/");

    expect(screen.queryByRole("combobox", { name: "Theme" })).not.toBeInTheDocument();
  });

  it.each(["/agreements", "/assets"])("contains the %s list within the viewport", (path) => {
    const { container } = renderLayout(path);

    expect(screen.getByRole("main")).toHaveAttribute("data-viewport-contained", "true");
    expect(container.querySelector("[data-print-content]")?.className).toContain("pageContentViewportContained");
  });

  it("leaves other workspace pages on normal document scrolling", () => {
    renderLayout("/admin");

    expect(screen.getByRole("main")).not.toHaveAttribute("data-viewport-contained");
  });

  it("renders the access-required page when authentication resolves without a configured user", () => {
    authMock.value.error = "notProvisioned";

    renderLayout("/agreements");

    expect(screen.getByRole("heading", { name: "You don't currently have access to NOF" })).toBeInTheDocument();
    expect(screen.queryByTestId("global-actions")).not.toBeInTheDocument();
  });
});

function renderLayout(initialEntry: string) {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route index element={<div>Home</div>} />
          <Route path="agreements" element={<div>Agreements</div>} />
          <Route path="assets" element={<div>Assets</div>} />
          <Route path="admin" element={<div>Admin</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}
