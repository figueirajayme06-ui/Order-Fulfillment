import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "./api";
import {
  addAssetToRingfence,
  fetchAssetEnrichment,
  fetchAssetProfile,
  fetchAssets,
  removeAssetFromRingfence,
  type AssetFilterParams,
} from "./assetsService";

vi.mock("./api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

const mockedApi = vi.mocked(api);

describe("assetsService", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("passes the current asset filter contract without renaming fields", async () => {
    const params: AssetFilterParams = {
      search: "generator",
      warehouse: "ED1",
      status: "Available",
      division: "110,120",
      facility: "UKN",
      itemNumber: "ITEM-1",
      description: "Diesel",
      agreementNumber: "A123",
      deliveryDateFrom: "2026-01-01",
      deliveryDateTo: "2026-01-31",
      validFromDate: "2026-02-01",
      validToDate: "2026-02-28",
      warehouseLocation: "Yard A",
      individualItemNumber: "SERIAL-1",
      terminationDateFrom: "2026-03-01",
      terminationDateTo: "2026-03-31",
      collectionDateFrom: "2026-04-01",
      collectionDateTo: "2026-04-30",
      estimatedReadyDateFrom: "2026-05-01",
      estimatedReadyDateTo: "2026-05-31",
      excludeStatuses: "RemovedStock,Scrap,Sold",
      take: 250,
    };
    const responseData = [{ id: "ASSET-1" }];
    mockedApi.get.mockResolvedValue({ data: responseData });

    const result = await fetchAssets(params);

    expect(mockedApi.get).toHaveBeenCalledOnce();
    expect(mockedApi.get).toHaveBeenCalledWith("/api/assets", { params });
    expect(result).toBe(responseData);
  });

  it("encodes profile asset IDs and preserves the date query names", async () => {
    const responseData = { asset: { id: "ASSET/1" }, summary: {}, events: [] };
    mockedApi.get.mockResolvedValue({ data: responseData });

    const result = await fetchAssetProfile("ASSET/1", "2026-06-01", "2027-05-31");

    expect(mockedApi.get).toHaveBeenCalledWith("/api/assets/ASSET%2F1/profile", {
      params: { fromDate: "2026-06-01", toDate: "2027-05-31" },
    });
    expect(result).toBe(responseData);
  });

  it("loads bounded asset enrichment using the encoded asset ID", async () => {
    const responseData = { location: null, serviceHistory: [], retrofits: [], rentalHistory: [] };
    mockedApi.get.mockResolvedValue({ data: responseData });

    const result = await fetchAssetEnrichment("ASSET/1");

    expect(mockedApi.get).toHaveBeenCalledWith("/api/assets/ASSET%2F1/enrichment", {
      params: { serviceLimit: 20 },
    });
    expect(result).toBe(responseData);
  });

  it("preserves the current ringfence item mutation routes", async () => {
    mockedApi.post.mockResolvedValue({ data: {} });
    mockedApi.delete.mockResolvedValue({ data: {} });

    await addAssetToRingfence(9, "ASSET-1");
    await removeAssetFromRingfence(9, "ASSET-1");

    expect(mockedApi.post).toHaveBeenCalledWith("/api/ringfence/9/items", { assetId: "ASSET-1" });
    expect(mockedApi.delete).toHaveBeenCalledWith("/api/ringfence/9/items/ASSET-1");
  });
});
