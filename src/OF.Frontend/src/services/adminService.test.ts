import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "./api";
import {
  createUser,
  deleteUser,
  fetchAdminOptions,
  fetchUsers,
  searchDirectoryPeople,
  updateUser,
  type AdminUserInput,
} from "./adminService";

vi.mock("./api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

const mockedApi = vi.mocked(api);
const input: AdminUserInput = {
  fullName: "Alex Smith",
  division: "110,150",
  isAdmin: true,
  isSuperAdmin: false,
  language: 2,
  dateFormat: "dd/MM/yyyy",
  roles: "ChangeOrder",
};

describe("adminService", () => {
  beforeEach(() => vi.resetAllMocks());

  it("uses the canonical list and options routes", async () => {
    mockedApi.get.mockResolvedValueOnce({ data: [{ loginName: "alex@example.com" }] });
    mockedApi.get.mockResolvedValueOnce({ data: { divisions: [] } });

    await expect(fetchUsers()).resolves.toEqual([{ loginName: "alex@example.com" }]);
    await expect(fetchAdminOptions()).resolves.toEqual({ divisions: [] });

    expect(mockedApi.get.mock.calls).toEqual([["/api/admin/users"], ["/api/admin/options"]]);
  });

  it("encodes directory searches and user route identities", async () => {
    mockedApi.get.mockResolvedValue({ data: [] });
    mockedApi.put.mockResolvedValue({ data: { loginName: "alex+ops@example.com" } });
    mockedApi.delete.mockResolvedValue({});

    await searchDirectoryPeople("Alex Smith + Ops");
    await updateUser("alex+ops@example.com", input);
    await deleteUser("alex+ops@example.com");

    expect(mockedApi.get).toHaveBeenCalledWith("/api/admin/people?search=Alex+Smith+%2B+Ops");
    expect(mockedApi.put).toHaveBeenCalledWith("/api/admin/users/alex%2Bops%40example.com", input);
    expect(mockedApi.delete).toHaveBeenCalledWith("/api/admin/users/alex%2Bops%40example.com");
  });

  it("posts every editable setting when creating a user", async () => {
    mockedApi.post.mockResolvedValue({ data: { loginName: "alex@example.com" } });

    await createUser({ ...input, loginName: "alex@example.com" });

    expect(mockedApi.post).toHaveBeenCalledWith("/api/admin/users", {
      ...input,
      loginName: "alex@example.com",
    });
  });
});
