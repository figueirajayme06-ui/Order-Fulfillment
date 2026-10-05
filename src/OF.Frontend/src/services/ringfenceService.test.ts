import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "./api";
import { addRingfenceItems, preflightRingfenceItems, type RingfenceItemBatchResult } from "./ringfenceService";

vi.mock("./api", () => ({
  default: {
    post: vi.fn(),
  },
}));

const mockedApi = vi.mocked(api);

const batchResult: RingfenceItemBatchResult = {
  readyAssetIds: ["ASSET-1"],
  alreadyAssignedAssetIds: ["ASSET-2"],
  unavailableAssetIds: ["ASSET-3"],
  addedAssetIds: [],
  overlaps: [
    {
      ringfenceId: 21,
      assetIds: ["ASSET-1"],
      title: "Existing ringfence",
      fromDate: "2026-08-24",
      toDate: "2026-08-31",
      owner: "planner@example.com",
    },
  ],
  requiresOverlapAcknowledgement: true,
};

describe("ringfenceService batch assignment contracts", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("sends preflight requests to the non-mutating batch route and returns the server classification", async () => {
    mockedApi.post.mockResolvedValue({ data: batchResult });

    const result = await preflightRingfenceItems(14, ["ASSET-1", "ASSET-2", "ASSET-3"]);

    expect(mockedApi.post).toHaveBeenCalledWith("/api/ringfence/14/items/preflight", {
      assetIds: ["ASSET-1", "ASSET-2", "ASSET-3"],
    });
    expect(result).toBe(batchResult);
  });

  it("does not acknowledge overlaps unless the caller explicitly confirms them", async () => {
    mockedApi.post.mockResolvedValue({ data: batchResult });

    await addRingfenceItems(14, ["ASSET-1"]);

    expect(mockedApi.post).toHaveBeenCalledWith("/api/ringfence/14/items/batch", {
      assetIds: ["ASSET-1"],
      acknowledgeOverlaps: false,
    });
  });

  it("passes an explicit overlap acknowledgement through to the atomic batch mutation", async () => {
    const added = { ...batchResult, addedAssetIds: ["ASSET-1"], requiresOverlapAcknowledgement: false };
    mockedApi.post.mockResolvedValue({ data: added });

    const result = await addRingfenceItems(14, ["ASSET-1"], true);

    expect(mockedApi.post).toHaveBeenCalledWith("/api/ringfence/14/items/batch", {
      assetIds: ["ASSET-1"],
      acknowledgeOverlaps: true,
    });
    expect(result).toBe(added);
  });
});
