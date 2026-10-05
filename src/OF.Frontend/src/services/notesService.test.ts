import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "./api";
import {
  createAgreementNote,
  createAssetNote,
  fetchAgreementNotes,
  fetchAssetNotes,
  saveAgreementNote,
  saveAssetNote,
} from "./notesService";

vi.mock("./api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}));

const mockedApi = vi.mocked(api);

describe("notesService", () => {
  beforeEach(() => vi.resetAllMocks());

  it("uses encoded asset note routes for retrieval, creation, and editing", async () => {
    mockedApi.get.mockResolvedValue({ data: [] });
    mockedApi.post.mockResolvedValue({ data: { id: 8, notes: "New" } });
    mockedApi.put.mockResolvedValue({ data: { id: 8, notes: "Updated" } });

    await fetchAssetNotes("A/B C?");
    await createAssetNote("A/B C?", "New");
    await saveAssetNote("A/B C?", 8, "Updated");

    expect(mockedApi.get).toHaveBeenCalledWith("/api/notes/assets/A%2FB%20C%3F");
    expect(mockedApi.post).toHaveBeenCalledWith("/api/notes/assets/A%2FB%20C%3F", { notes: "New" });
    expect(mockedApi.put).toHaveBeenCalledWith("/api/notes/assets/A%2FB%20C%3F/8", { notes: "Updated" });
  });

  it("uses agreement note routes for retrieval, creation, and editing", async () => {
    mockedApi.get.mockResolvedValue({ data: [] });
    mockedApi.post.mockResolvedValue({ data: { id: 12, notes: "Call customer" } });
    mockedApi.put.mockResolvedValue({ data: { id: 12, notes: "Updated" } });

    await fetchAgreementNotes(42);
    await createAgreementNote(42, "Call customer");
    await saveAgreementNote(42, 12, "Updated");

    expect(mockedApi.get).toHaveBeenCalledWith("/api/notes/agreements/42");
    expect(mockedApi.post).toHaveBeenCalledWith("/api/notes/agreements/42", { notes: "Call customer" });
    expect(mockedApi.put).toHaveBeenCalledWith("/api/notes/agreements/42/12", { notes: "Updated" });
  });
});
