import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import "../../i18n";
import { FrontendPreviewBanner } from "./FrontendPreviewBanner";

describe("FrontendPreviewBanner", () => {
  afterEach(cleanup);

  it("shows environment context and a same-tab return link", () => {
    render(
      <FrontendPreviewBanner
        configuration={{
          environmentLabel: "OF Dev",
          showPreviewBanner: true,
          legacyFrontendUrl: "https://asofdev.azurewebsites.net/",
        }}
      />,
    );

    expect(screen.getByText("OF Dev · Preview")).toBeInTheDocument();
    const link = screen.getByRole("link", { name: /Return to current Order Fulfilment/i });
    expect(link).toHaveAttribute("href", "https://asofdev.azurewebsites.net/");
    expect(link).not.toHaveAttribute("target");
  });

  it("omits the return action when no legacy URL is configured", () => {
    render(
      <FrontendPreviewBanner
        configuration={{ environmentLabel: "OF Dev", showPreviewBanner: true, legacyFrontendUrl: null }}
      />,
    );

    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });

  it("does not render when the preview banner is disabled", () => {
    const { container } = render(
      <FrontendPreviewBanner
        configuration={{ environmentLabel: "OF Dev", showPreviewBanner: false, legacyFrontendUrl: null }}
      />,
    );

    expect(container).toBeEmptyDOMElement();
  });

  it("shows non-production deployment identity and a validated commit link", () => {
    render(
      <FrontendPreviewBanner
        configuration={{
          environmentLabel: "OF SIT",
          showPreviewBanner: true,
          legacyFrontendUrl: null,
          deploymentInfo: {
            version: "1.12.0",
            commitSha: "abcdef1234567890",
            sourceRef: "refs/heads/main",
            buildTimestamp: "2026-09-03T10:30:00Z",
          },
        }}
      />,
    );

    expect(screen.getByText("OF SIT · 1.12.0 · abcdef1")).toBeInTheDocument();
    const commitLink = screen.getByRole("link", { name: "abcdef1234567890" });
    expect(commitLink).toHaveAttribute(
      "href",
      "https://github.com/AggrekoTechnologyServices/Order-Fulfillment/commit/abcdef1234567890",
    );
    expect(commitLink).toHaveAttribute("target", "_blank");
    expect(commitLink).toHaveAttribute("rel", "noopener noreferrer");
  });

  it("does not create a link for malformed deployment commits", () => {
    render(
      <FrontendPreviewBanner
        configuration={{
          environmentLabel: "OF Test",
          showPreviewBanner: true,
          legacyFrontendUrl: null,
          deploymentInfo: {
            version: null,
            commitSha: "not-a-sha",
            sourceRef: null,
            buildTimestamp: null,
          },
        }}
      />,
    );

    expect(screen.getByText("Commit unavailable")).toBeInTheDocument();
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });
});
