import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AppConfiguration } from "../types";
import { reloadForStaleRelease } from "./staleReleaseRecovery";

describe("stale release recovery", () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  it("records the first observed release without reloading", () => {
    expect(reloadForStaleRelease(configuration("1.12.0"))).toBe(false);
    expect(window.sessionStorage.getItem("of.release.active")).toBe("1.12.0");
  });

  it("requests only one guarded reload when the deployed release changes", () => {
    reloadForStaleRelease(configuration("1.11.0"));
    const reload = vi.fn();

    expect(reloadForStaleRelease(configuration("1.12.0"), reload)).toBe(true);
    expect(reload).toHaveBeenCalledOnce();
    expect(reloadForStaleRelease(configuration("1.12.0"), reload)).toBe(false);
  });
});

function configuration(version: string): AppConfiguration {
  return {
    environmentLabel: "OF Dev",
    showPreviewBanner: true,
    legacyFrontendUrl: null,
    deploymentInfo: {
      version,
      commitSha: null,
      sourceRef: null,
      buildTimestamp: null,
    },
  };
}
