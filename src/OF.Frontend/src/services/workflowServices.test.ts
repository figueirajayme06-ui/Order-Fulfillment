import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "./api";
import { activateAgreement, cancelActivation } from "./activationService";
import { fetchAvailabilitySummary } from "./availabilityService";
import { bulkDepotFulfil, bulkRehire } from "./bulkActionsService";
import { fetchAssetEvents } from "./eventsService";
import { fetchUsers, fetchWarehouses } from "./lookupsService";

vi.mock("./api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

const mockedApi = vi.mocked(api);

describe("workflow service contracts", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("preserves activation and cancellation routes", async () => {
    const activation = { type: "Activation", headerId: 42 };
    mockedApi.post.mockResolvedValueOnce({ data: activation }).mockResolvedValueOnce({ data: undefined });

    const result = await activateAgreement(42);
    await cancelActivation(42);

    expect(mockedApi.post).toHaveBeenNthCalledWith(1, "/api/activation/42");
    expect(mockedApi.post).toHaveBeenNthCalledWith(2, "/api/activation/42/cancel");
    expect(result).toBe(activation);
  });

  it("preserves depot-fulfil and rehire payloads", async () => {
    const request = {
      headerId: 42,
      lineIds: [7, 9],
      warehouse: "ED1",
      includeAlreadyFulfilled: true,
    };
    mockedApi.post.mockResolvedValueOnce({ data: { processed: 2 } }).mockResolvedValueOnce({ data: { processed: 1 } });

    const depotResult = await bulkDepotFulfil(request);
    const rehireResult = await bulkRehire(request);

    expect(mockedApi.post).toHaveBeenNthCalledWith(1, "/api/bulkactions/depot-fulfil", request);
    expect(mockedApi.post).toHaveBeenNthCalledWith(2, "/api/bulkactions/rehire", request);
    expect(depotResult).toEqual({ processed: 2 });
    expect(rehireResult).toEqual({ processed: 1 });
  });

  it("passes the complete asset-event request without changing division delimiters", async () => {
    const request = {
      startDate: "2026-06-01",
      endDate: "2026-08-31",
      divisions: "110;120",
      assetIds: ["ASSET-1", "ASSET-2"],
    };
    const events = { "ASSET-1": [{ assetId: "ASSET-1" }] };
    mockedApi.post.mockResolvedValue({ data: { events } });

    const result = await fetchAssetEvents(request);

    expect(mockedApi.post).toHaveBeenCalledWith("/api/event/events", request);
    expect(result).toBe(events);
  });

  it("retains an empty event map when the response omits events", async () => {
    mockedApi.post.mockResolvedValue({ data: {} });

    await expect(fetchAssetEvents({ startDate: "2026-06-01" })).resolves.toEqual({});
  });

  it("preserves availability query names and URL encoding", async () => {
    const availability = [{ warehouse: "ED1" }];
    mockedApi.get.mockResolvedValue({ data: availability });

    const result = await fetchAvailabilitySummary(
      "GEN / 1",
      "voltage=400 V",
      "2026-06-01",
      "2026-06-30",
      "110,120",
      "ITEM / 1",
      73,
    );

    expect(mockedApi.get).toHaveBeenCalledWith(
      "/api/availability/summary?genericCode=GEN+%2F+1&attributes=voltage%3D400+V&startDate=2026-06-01&endDate=2026-06-30&division=110%2C120&itemNumber=ITEM+%2F+1&lineId=73",
    );
    expect(result).toBe(availability);
  });

  it("requests the configured warehouse catalogue for the selected divisions", async () => {
    const warehouses = [
      {
        warehouseCode: "EE1",
        warehouse: "East England repair",
        facility: "UKC",
        divisionCode: "110",
        divisionName: "United Kingdom",
      },
    ];
    mockedApi.get.mockResolvedValue({ data: warehouses });

    const result = await fetchWarehouses("110,150");

    expect(mockedApi.get).toHaveBeenCalledWith("/api/lookups/warehouses?division=110%2C150");
    expect(result).toBe(warehouses);
  });

  it("requests division-scoped Ringfence owners with URL encoding", async () => {
    const users = [{ loginName: "planner@example.com", fullName: "Fleet Planner" }];
    mockedApi.get.mockResolvedValue({ data: users });

    const result = await fetchUsers("110, UK & IE");

    expect(mockedApi.get).toHaveBeenCalledWith("/api/lookups/users?division=110%2C+UK+%26+IE");
    expect(result).toBe(users);
  });
});
