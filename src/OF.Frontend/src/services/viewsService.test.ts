import { beforeEach, describe, expect, it, vi } from "vitest";
import type { SavedViewRecord } from "../lib/savedViewsStorage";
import {
  createSavedView,
  deleteSavedView,
  getSavedViewValidationMessage,
  listSavedViews,
  listSavedViewRecipientCandidates,
  updateSavedView,
  type PersistedSavedView,
} from "./viewsService";
import api from "./api";
import {
  createSavedView as createLocalSavedView,
  deleteSavedView as deleteLocalSavedView,
  listSavedViews as listLocalSavedViews,
  updateSavedView as updateLocalSavedView,
} from "../lib/savedViewsStorage";

vi.mock("./api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("../lib/savedViewsStorage", () => ({
  createSavedView: vi.fn(),
  deleteSavedView: vi.fn(),
  listSavedViews: vi.fn(),
  updateSavedView: vi.fn(),
}));

type TestState = {
  kind: string;
};

const decodeState = (value: unknown): TestState | null => {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }

  const kind = (value as Record<string, unknown>).kind;
  return typeof kind === "string" ? { kind } : null;
};

const mockedApi = vi.mocked(api);
const mockedCreateLocalSavedView = vi.mocked(createLocalSavedView);
const mockedDeleteLocalSavedView = vi.mocked(deleteLocalSavedView);
const mockedListLocalSavedViews = vi.mocked(listLocalSavedViews);
const mockedUpdateLocalSavedView = vi.mocked(updateLocalSavedView);

function buildLocalView(id: string, name: string): SavedViewRecord<TestState> {
  return {
    id,
    name,
    page: "agreements",
    state: { kind: "ok" },
    createdAt: "2026-01-01T00:00:00.000Z",
    updatedAt: "2026-01-01T00:00:00.000Z",
  };
}

function buildApiView(id: number, name: string): PersistedSavedView<TestState> {
  return {
    id: String(id),
    name,
    page: "agreements",
    scope: "global",
    owner: "owner@aggreko.com",
    isOwner: true,
    canEdit: true,
    canDelete: true,
    isDefault: true,
    source: "api",
    recipients: [],
    state: { kind: "ok" },
  };
}

describe("viewsService", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("maps API saved views when endpoint is available", async () => {
    mockedApi.get.mockResolvedValue({
      data: [
        {
          id: 9,
          name: "Ops Global",
          page: "agreements",
          scope: "global",
          owner: "owner@aggreko.com",
          isOwner: true,
          canEdit: true,
          canDelete: true,
          isDefault: true,
          recipients: [{ loginName: "recipient@aggreko.com", fullName: "Recipient User" }],
          state: { kind: "ok" },
        },
      ],
    });

    const result = await listSavedViews("agreements", decodeState);

    expect(result.source).toBe("api");
    expect(result.views).toHaveLength(1);
    expect(result.views[0]).toMatchObject({
      ...buildApiView(9, "Ops Global"),
      recipients: [{ loginName: "recipient@aggreko.com", fullName: "Recipient User" }],
    });
    expect(result.views[0].recipients).toEqual([{ loginName: "recipient@aggreko.com", fullName: "Recipient User" }]);
  });

  it("falls back to local storage when API list fails", async () => {
    mockedApi.get.mockRejectedValue(new Error("down"));
    mockedListLocalSavedViews.mockReturnValue([buildLocalView("local-1", "Local Backup")]);

    const result = await listSavedViews("agreements", decodeState);

    expect(result.source).toBe("local");
    expect(result.views).toHaveLength(1);
    expect(result.views[0]).toMatchObject({
      id: "local-1",
      name: "Local Backup",
      scope: "personal",
      source: "local",
    });
  });

  it("falls back to local create when API is unreachable", async () => {
    mockedApi.post.mockRejectedValue({
      isAxiosError: true,
      response: undefined,
    });
    mockedCreateLocalSavedView.mockReturnValue(buildLocalView("local-new", "Created Local"));

    const result = await createSavedView("api", "agreements", "Created Local", "personal", { kind: "ok" }, decodeState);

    expect(result.source).toBe("local");
    expect(result.view?.id).toBe("local-new");
    expect(result.view?.scope).toBe("personal");
  });

  it("never falls back to local storage when a user share cannot reach the API", async () => {
    const networkError = { isAxiosError: true, response: undefined };
    mockedApi.post.mockRejectedValue(networkError);

    await expect(
      createSavedView("api", "agreements", "Shared", "users", { kind: "ok" }, decodeState, ["user@aggreko.com"]),
    ).rejects.toBe(networkError);
    expect(mockedCreateLocalSavedView).not.toHaveBeenCalled();
  });

  it("searches for a bounded set of recipients and preserves best-match server order", async () => {
    mockedApi.get.mockResolvedValue({
      data: [
        { loginName: " zed@aggreko.com ", fullName: "Zed User" },
        { loginName: "amy@aggreko.com", fullName: "Amy User" },
        { loginName: "AMY@aggreko.com", fullName: "Duplicate" },
        { loginName: "", fullName: "Invalid" },
      ],
    });

    await expect(listSavedViewRecipientCandidates(" amy ")).resolves.toEqual([
      { loginName: "zed@aggreko.com", fullName: "Zed User" },
      { loginName: "amy@aggreko.com", fullName: "Amy User" },
    ]);
    expect(mockedApi.get).toHaveBeenCalledWith("/api/views/recipient-candidates", {
      params: { search: "amy", limit: 10 },
    });
  });

  it("does not request recipient candidates before a search", async () => {
    await expect(listSavedViewRecipientCandidates("   ")).resolves.toEqual([]);
    expect(mockedApi.get).not.toHaveBeenCalled();
  });

  it("only exposes bounded validation messages from 400 responses", () => {
    expect(
      getSavedViewValidationMessage({
        isAxiosError: true,
        response: { status: 400, data: { message: " Recipient is no longer available. " } },
      }),
    ).toBe("Recipient is no longer available.");
    expect(
      getSavedViewValidationMessage({
        isAxiosError: true,
        response: { status: 500, data: { message: "internal detail" } },
      }),
    ).toBeNull();
  });

  it("updates local saved views in local mode", async () => {
    mockedUpdateLocalSavedView.mockReturnValue(buildLocalView("local-2", "Updated"));

    const result = await updateSavedView(
      "local",
      "agreements",
      "local-2",
      "Updated",
      "personal",
      { kind: "ok" },
      decodeState,
    );

    expect(result.source).toBe("local");
    expect(result.view?.id).toBe("local-2");
    expect(mockedUpdateLocalSavedView).toHaveBeenCalledTimes(1);
  });

  it("never falls back to local storage when updating a shared user view fails", async () => {
    const networkError = { isAxiosError: true, response: undefined };
    mockedApi.put.mockRejectedValue(networkError);

    await expect(
      updateSavedView("api", "agreements", "42", "Shared", "users", { kind: "ok" }, decodeState, ["user@aggreko.com"]),
    ).rejects.toBe(networkError);
    expect(mockedUpdateLocalSavedView).not.toHaveBeenCalled();
  });

  it("returns deleted false for invalid API ids", async () => {
    mockedDeleteLocalSavedView.mockReturnValue(true);

    const result = await deleteSavedView("api", "agreements", "not-a-number");

    expect(result.source).toBe("api");
    expect(result.deleted).toBe(false);
  });
});
