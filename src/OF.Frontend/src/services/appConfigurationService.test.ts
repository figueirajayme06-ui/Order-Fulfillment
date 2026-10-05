import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "./api";
import { fetchAppConfiguration } from "./appConfigurationService";
import type { AppConfiguration } from "../types";

vi.mock("./api", () => ({
  default: {
    get: vi.fn(),
  },
}));

const mockedApi = vi.mocked(api);

describe("appConfigurationService", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("loads runtime configuration from the same-origin endpoint", async () => {
    const configuration: AppConfiguration = {
      environmentLabel: "OF Dev",
      showPreviewBanner: true,
      legacyFrontendUrl: "https://asofdev.azurewebsites.net/",
    };
    mockedApi.get.mockResolvedValueOnce({ data: configuration });

    await expect(fetchAppConfiguration()).resolves.toBe(configuration);
    expect(mockedApi.get).toHaveBeenCalledWith("/api/app-config");
  });
});
