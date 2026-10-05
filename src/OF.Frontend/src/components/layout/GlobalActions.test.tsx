import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { fetchAlerts, fetchRefreshStatuses } from "../../services/utilityService";
import { GlobalActions } from "./GlobalActions";
import styles from "./GlobalActions.module.css";

vi.mock("../../services/utilityService", () => ({
  fetchAlerts: vi.fn(),
  fetchRefreshStatuses: vi.fn(),
  requestPull: vi.fn(),
}));

const mockedFetchAlerts = vi.mocked(fetchAlerts);
const mockedFetchRefreshStatuses = vi.mocked(fetchRefreshStatuses);

describe("GlobalActions", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it.each([
    [1, "1"],
    [12, "12"],
    [100, "99+"],
  ])("shows %s unread alerts as %s in the sidebar badge", async (count, displayCount) => {
    mockedFetchAlerts.mockResolvedValue(
      Array.from({ length: count }, (_, index) => ({
        id: index + 1,
        text: `Alert ${index + 1}`,
        lineId: index + 1,
        headerId: index + 1,
      })),
    );
    mockedFetchRefreshStatuses.mockResolvedValue([]);

    render(
      <MemoryRouter>
        <GlobalActions appearance="dark" placement="sidebar" showPull={false} showUtilities={false} />
      </MemoryRouter>,
    );

    const alertsControl = await screen.findByLabelText(`${count} unread alerts`);
    const badge = alertsControl.querySelector(`.${styles.badge}`);

    expect(badge).toHaveTextContent(displayCount);
  });

  it("uses labelled Lucide close controls in aligned Alerts and Data refreshes headers", async () => {
    mockedFetchAlerts.mockResolvedValue([]);
    mockedFetchRefreshStatuses.mockResolvedValue([]);

    render(
      <MemoryRouter>
        <GlobalActions appearance="dark" placement="sidebar" showPull={false} />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(mockedFetchAlerts).toHaveBeenCalled();
      expect(mockedFetchRefreshStatuses).toHaveBeenCalled();
    });

    const closeAlerts = screen.getByRole("button", { name: "Close alerts" });
    const closeRefreshes = screen.getByRole("button", { name: "Close data refreshes" });

    expect(closeAlerts).toHaveClass(styles.menuClose);
    expect(closeRefreshes).toHaveClass(styles.menuClose);
    expect(closeAlerts.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
    expect(closeRefreshes.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
    expect(closeAlerts).not.toHaveTextContent("×");
    expect(closeRefreshes).not.toHaveTextContent("×");
    expect(screen.getAllByRole("button", { name: "Refresh" })).toHaveLength(2);

    const alertsDropdown = closeAlerts.closest("details");
    const refreshesDropdown = closeRefreshes.closest("details");
    alertsDropdown?.setAttribute("open", "");
    refreshesDropdown?.setAttribute("open", "");

    fireEvent.click(closeAlerts);
    fireEvent.click(closeRefreshes);

    expect(alertsDropdown).not.toHaveAttribute("open");
    expect(refreshesDropdown).not.toHaveAttribute("open");
  });
});
