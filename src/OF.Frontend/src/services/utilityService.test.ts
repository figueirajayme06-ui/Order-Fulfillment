import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "./api";
import { fetchAlerts, fetchRefreshStatuses, requestPull } from "./utilityService";

vi.mock("./api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

const mockedApi = vi.mocked(api);

describe("utilityService", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("trims and safely encodes manual pull numbers", async () => {
    mockedApi.post.mockResolvedValue({ data: { type: "Quote", number: "Q-123/4" } });

    await requestPull("  Q-123/4  ");

    expect(mockedApi.post).toHaveBeenCalledWith("/api/pull/Q-123%2F4");
  });

  it("loads data refresh timestamps", async () => {
    const refreshes = [{ key: "CPQ", description: "CPQ data", lastSuccessfulRunUtc: null }];
    mockedApi.get.mockResolvedValue({ data: refreshes });

    await expect(fetchRefreshStatuses()).resolves.toEqual(refreshes);
    expect(mockedApi.get).toHaveBeenCalledWith("/api/status/refreshes");
  });

  it("loads user alerts", async () => {
    const alerts = [{ id: 1, text: "Reservation clash", lineId: 2, headerId: 3 }];
    mockedApi.get.mockResolvedValue({ data: alerts });

    await expect(fetchAlerts()).resolves.toEqual(alerts);
    expect(mockedApi.get).toHaveBeenCalledWith("/api/alerts");
  });
});
