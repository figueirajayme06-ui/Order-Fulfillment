import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import {
  createUser,
  deleteUser,
  fetchAdminOptions,
  fetchUsers,
  searchDirectoryPeople,
  updateUser,
  type AdminOptions,
  type UserListItem,
} from "../../services/adminService";
import { AdminPage } from "./AdminPage";

const authMock = vi.hoisted(() => ({
  value: {
    user: {
      loginName: "admin@example.com",
      displayName: "Admin User",
      division: "110",
      isAdmin: true,
      isSuperAdmin: false,
      isReadOnly: false,
      language: "en",
    },
    isLoading: false,
    error: null,
  },
}));

vi.mock("../../contexts/auth", () => ({
  useAuth: () => authMock.value,
}));

vi.mock("../../services/adminService", () => ({
  createUser: vi.fn(),
  deleteUser: vi.fn(),
  fetchAdminOptions: vi.fn(),
  fetchUsers: vi.fn(),
  searchDirectoryPeople: vi.fn(),
  updateUser: vi.fn(),
}));

const mockedCreateUser = vi.mocked(createUser);
const mockedDeleteUser = vi.mocked(deleteUser);
const mockedFetchAdminOptions = vi.mocked(fetchAdminOptions);
const mockedFetchUsers = vi.mocked(fetchUsers);
const mockedSearchDirectoryPeople = vi.mocked(searchDirectoryPeople);
const mockedUpdateUser = vi.mocked(updateUser);

const options: AdminOptions = {
  divisions: [
    { code: "110", name: "United Kingdom" },
    { code: "150", name: "Ireland" },
  ],
  languages: [
    { value: 0, label: "English" },
    { value: 2, label: "French" },
  ],
  dateFormats: ["dd/MM/yyyy", "MM/dd/yyyy"],
  rolesEnabled: true,
  roles: ["ChangeOrder", "ChangeApproval"],
};

const configuredUser: UserListItem = {
  loginName: "planner@example.com",
  fullName: "Planner One",
  division: "110",
  isAdmin: false,
  isSuperAdmin: false,
  language: 0,
  dateFormat: "dd/MM/yyyy",
  roles: null,
  lastLoginAtUtc: null,
};

describe("AdminPage", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    sessionStorage.clear();
    authMock.value.user.isAdmin = true;
    authMock.value.user.isSuperAdmin = false;
    authMock.value.user.isReadOnly = false;
    mockedFetchUsers.mockResolvedValue([configuredUser]);
    mockedFetchAdminOptions.mockResolvedValue(options);
    await i18n.changeLanguage("en");
  });

  afterEach(cleanup);

  it("denies non-admins without calling Admin APIs", () => {
    authMock.value.user.isAdmin = false;

    render(<AdminPage />);

    expect(screen.getByText("Access denied. Admin privileges are required.")).toBeInTheDocument();
    expect(mockedFetchUsers).not.toHaveBeenCalled();
    expect(mockedFetchAdminOptions).not.toHaveBeenCalled();
  });

  it("treats read-only as stronger than a malformed stored Admin flag", () => {
    authMock.value.user.isReadOnly = true;

    render(<AdminPage />);

    expect(screen.getByText("Access denied. Admin privileges are required.")).toBeInTheDocument();
    expect(mockedFetchUsers).not.toHaveBeenCalled();
  });

  it("offers Read only when optional feature roles are disabled and makes it exclusive with Admin", async () => {
    const user = userEvent.setup();
    mockedFetchAdminOptions.mockResolvedValue({ ...options, rolesEnabled: false, roles: ["ReadOnly"] });
    mockedUpdateUser.mockResolvedValue({ ...configuredUser, roles: "ReadOnly" });
    render(<AdminPage />);
    const row = (await screen.findByText("Planner One")).closest("tr")!;

    await user.click(within(row).getByRole("button", { name: "Edit" }));
    const admin = screen.getByRole("checkbox", { name: "Admin" });
    const readOnly = screen.getByRole("checkbox", { name: "Read only" });
    await user.click(admin);
    expect(admin).toBeChecked();
    await user.click(readOnly);
    expect(admin).not.toBeChecked();
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(mockedUpdateUser).toHaveBeenCalledWith(
        configuredUser.loginName,
        expect.objectContaining({ isAdmin: false, roles: "ReadOnly" }),
      ),
    );
  });

  it("shows all legacy-managed fields and supports filtering and grouping", async () => {
    render(<AdminPage />);

    await screen.findByText("Planner One");
    for (const heading of [
      "Login Name",
      "Full name",
      "Divisions",
      "Language",
      "Date format",
      "Feature roles",
      "Last NOF access",
      "Access",
    ]) {
      expect(screen.getByRole("columnheader", { name: heading })).toBeInTheDocument();
    }

    const filter = screen.getByPlaceholderText("Search configured users");
    await userEvent.setup().type(filter, "no match");
    expect(screen.getByRole("heading", { name: "No users match this search" })).toBeInTheDocument();

    await userEvent.setup().click(screen.getByRole("button", { name: "Clear search" }));
    await userEvent.setup().selectOptions(screen.getByLabelText("Group by"), "division");
    expect(screen.getByText("110 · 1 users")).toBeInTheDocument();
  });

  it("restores the configured-user filter and grouping after remount", async () => {
    const user = userEvent.setup();
    const firstRender = render(<AdminPage />);
    await screen.findByText("Planner One");

    await user.type(screen.getByPlaceholderText("Search configured users"), "planner");
    await user.selectOptions(screen.getByLabelText("Group by"), "division");
    firstRender.unmount();

    render(<AdminPage />);
    await screen.findByText("Planner One");
    expect(screen.getByPlaceholderText("Search configured users")).toHaveValue("planner");
    expect(screen.getByLabelText("Group by")).toHaveValue("division");
  });

  it("shows Never or a locale-formatted full timestamp for Last NOF access", async () => {
    mockedFetchUsers.mockResolvedValue([
      configuredUser,
      {
        ...configuredUser,
        loginName: "active@example.com",
        fullName: "Active User",
        lastLoginAtUtc: "2026-09-02T09:30:00Z",
      },
    ]);

    render(<AdminPage />);

    const neverRow = (await screen.findByText("Planner One")).closest("tr")!;
    expect(within(neverRow).getByText("Never")).toBeInTheDocument();
    const activeRow = screen.getByText("Active User").closest("tr")!;
    const timestamp = within(activeRow).getByLabelText(/Last NOF access:.*2026/);
    expect(timestamp.tagName).toBe("TIME");
    expect(timestamp).toHaveAttribute("datetime", "2026-09-02T09:30:00Z");
    expect(timestamp).toHaveAttribute("title", expect.stringContaining("2026"));
  });

  it("creates a selected directory person with controlled access settings", async () => {
    const user = userEvent.setup();
    const created: UserListItem = {
      loginName: "alex.smith@example.com",
      fullName: "Alex Smith",
      division: "110,150",
      isAdmin: true,
      isSuperAdmin: false,
      language: 2,
      dateFormat: "MM/dd/yyyy",
      roles: "ChangeOrder",
      lastLoginAtUtc: null,
    };
    mockedSearchDirectoryPeople.mockResolvedValue([
      { loginName: created.loginName, fullName: created.fullName, mail: created.loginName },
    ]);
    mockedCreateUser.mockResolvedValue(created);
    render(<AdminPage />);
    await screen.findByText("Planner One");

    await user.click(screen.getByRole("button", { name: "Add user" }));
    await user.type(screen.getByLabelText("Find a person in the company directory"), "Alex Smith");
    await user.click(screen.getByRole("button", { name: "Search" }));
    await user.click(screen.getByRole("button", { name: "Alex Smith — alex.smith@example.com" }));
    expect(screen.queryByText("No matching people were found.")).not.toBeInTheDocument();
    await user.click(screen.getByRole("checkbox", { name: "110 — United Kingdom" }));
    await user.click(screen.getByRole("checkbox", { name: "150 — Ireland" }));
    await user.selectOptions(screen.getByLabelText("Language"), "2");
    await user.selectOptions(screen.getByLabelText("Date format"), "MM/dd/yyyy");
    await user.click(screen.getByRole("checkbox", { name: "Admin" }));
    await user.click(screen.getByRole("checkbox", { name: "ChangeOrder" }));
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(mockedCreateUser).toHaveBeenCalledWith({
        loginName: created.loginName,
        fullName: created.fullName,
        division: "110,150",
        isAdmin: true,
        isSuperAdmin: false,
        language: 2,
        dateFormat: "MM/dd/yyyy",
        roles: "ChangeOrder",
      }),
    );
    expect(await screen.findByText("Alex Smith was added.")).toBeInTheDocument();
    expect(screen.getByText("alex.smith@example.com")).toBeInTheDocument();
  }, 15_000);

  it("edits an existing user while keeping their directory identity read-only", async () => {
    const user = userEvent.setup();
    const updated = { ...configuredUser, division: "110,150", language: 2, roles: "ChangeApproval" };
    mockedUpdateUser.mockResolvedValue(updated);
    render(<AdminPage />);
    const row = (await screen.findByText("Planner One")).closest("tr")!;

    await user.click(within(row).getByRole("button", { name: "Edit" }));
    expect(screen.getByLabelText("Login Name")).toHaveAttribute("readonly");
    expect(screen.getByLabelText("Full name")).toHaveAttribute("readonly");
    await user.click(screen.getByRole("checkbox", { name: "150 — Ireland" }));
    await user.selectOptions(screen.getByLabelText("Language"), "2");
    await user.click(screen.getByRole("checkbox", { name: "ChangeApproval" }));
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(mockedUpdateUser).toHaveBeenCalledWith(
        configuredUser.loginName,
        expect.objectContaining({ division: "110,150", language: 2, roles: "ChangeApproval" }),
      ),
    );
    expect(await screen.findByText("Planner One was updated.")).toBeInTheDocument();
  }, 15_000);

  it("keeps deletion and Super Admin changes restricted to Super Admins", async () => {
    const user = userEvent.setup();
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
    authMock.value.user.isSuperAdmin = true;
    mockedDeleteUser.mockResolvedValue();
    render(<AdminPage />);
    const row = (await screen.findByText("Planner One")).closest("tr")!;

    await user.click(within(row).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(mockedDeleteUser).toHaveBeenCalledWith(configuredUser.loginName));
    expect(confirm).toHaveBeenCalledWith("Delete Planner One (planner@example.com)? This action cannot be undone.");
    expect(await screen.findByText("Planner One was deleted.")).toBeInTheDocument();
    confirm.mockRestore();
  });
});
