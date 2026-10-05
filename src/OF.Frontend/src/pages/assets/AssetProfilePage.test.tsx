import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { fetchAssetEnrichment, fetchAssetProfile } from "../../services/assetsService";
import { fetchAssetNotes } from "../../services/notesService";
import { AssetProfilePage } from "./AssetProfilePage";

vi.mock("../../services/assetsService", () => ({
  fetchAssetEnrichment: vi.fn(),
  fetchAssetProfile: vi.fn(),
}));

vi.mock("../../services/notesService", () => ({
  fetchAssetNotes: vi.fn(),
  createAssetNote: vi.fn(),
  saveAssetNote: vi.fn(),
}));

const capturedTimeline = vi.hoisted(() => ({ props: null as unknown }));
vi.mock("../../components/timeline/FrappeGantt", () => ({
  FrappeGantt: (props: { tasks: Array<{ id: string }> }) => {
    capturedTimeline.props = props;
    return <div data-testid="asset-profile-timeline">{props.tasks.map((task) => task.id).join(",")}</div>;
  },
}));

const mockedFetchAssetProfile = vi.mocked(fetchAssetProfile);
const mockedFetchAssetEnrichment = vi.mocked(fetchAssetEnrichment);
const mockedFetchAssetNotes = vi.mocked(fetchAssetNotes);

describe("AssetProfilePage", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    mockedFetchAssetNotes.mockResolvedValue([]);
    mockedFetchAssetEnrichment.mockResolvedValue({
      location: null,
      serviceHistory: [],
      retrofits: [],
      rentalHistory: [],
    });
  });

  it("shows planner availability, conflicts, and booking events", async () => {
    mockedFetchAssetProfile.mockResolvedValue({
      asset: {
        id: "10003G",
        individualItemNumber: "DP0032PBFE01",
        itemNumber: "DP0032PBFE01",
        description: "DP 32amp PBF AES1",
        status: "OnHire",
        warehouse: "ED1",
        warehouseLocation: "Yard A",
        division: "110",
        facility: "UKN",
        estimatedReadyDate: null,
        telemetryStatus: "Connected",
        agreementNumber: "A752489",
        customerName: "Speedy Asset Services Ltd",
        deliveryDate: "2026-01-05T00:00:00",
        agreementLineValidFromDate: "2026-01-05T00:00:00",
        agreementLineValidToDate: "2026-04-30T00:00:00",
        collectionDate: "2026-04-30T00:00:00",
        terminationDate: null,
        manufacturerName: "Aggreko",
        productGroup: "Distribution",
        productCategory: "Power distribution",
        runHours: 1240,
        serviceCenter: "Edinburgh",
      },
      summary: {
        availabilityStatus: "Committed",
        nextAvailableDate: "2026-05-01T00:00:00",
        nextCommitmentDate: "2026-01-05T00:00:00",
        nextCommitmentLabel: "On hire: A752489",
        conflictCount: 2,
      },
      events: [
        {
          id: "reservation-1",
          type: "Reservation",
          status: "Planned",
          startDate: "2026-04-20T00:00:00",
          endDate: "2026-06-30T00:00:00",
          title: "A753044",
          agreementId: 44,
          agreementNumber: "A753044",
          customerName: "Shaun - UK 2026",
          warehouse: "ED1",
          source: "Reservation",
          hasConflict: true,
          conflictReason: "Overlaps on hire: A752489",
        },
        {
          id: "on-hold-1",
          type: "On hold",
          status: "Current",
          startDate: "2026-09-23T00:00:00",
          endDate: null,
          title: "On hold",
          agreementId: null,
          agreementNumber: null,
          customerName: null,
          warehouse: "EV0",
          source: "Asset status",
          hasConflict: false,
          conflictReason: null,
        },
      ],
    });

    render(
      <MemoryRouter initialEntries={["/assets/10003G"]}>
        <Routes>
          <Route path="/assets/:assetId" element={<AssetProfilePage />} />
        </Routes>
      </MemoryRouter>,
    );

    expect(await screen.findByRole("heading", { name: "10003G" })).toBeInTheDocument();
    expect(screen.getByText("Committed")).toBeInTheDocument();
    expect(screen.getByText("2 to review")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Planning and bookings" }));
    expect(screen.getAllByText("Reservation")).toHaveLength(2);
    expect(screen.getByTestId("asset-profile-timeline")).toHaveTextContent("reservation-1");
    expect(capturedTimeline.props).toMatchObject({
      viewMode: "Day",
    });
    expect((capturedTimeline.props as { tasks: unknown[] }).tasks).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          period: { start: "2026-04-20T00:00:00", end: "2026-06-30T00:00:00" },
          custom_class: "event-reserved",
        }),
      ]),
    );
    expect(screen.getByRole("link", { name: "A753044" })).toHaveAttribute("href", "/agreements/44");
    expect(screen.getByText("Conflict")).toHaveAttribute("title", "Overlaps on hire: A752489");
    expect(screen.queryByRole("button", { name: /Earlier/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Later/ })).not.toBeInTheDocument();
    await waitFor(() => expect(mockedFetchAssetNotes).toHaveBeenCalledWith("10003G"));
    await waitFor(() => expect(mockedFetchAssetEnrichment).toHaveBeenCalledWith("10003G"));

    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-03-01" } });
    fireEvent.change(screen.getByLabelText("To"), { target: { value: "2026-09-30" } });

    fireEvent.click(screen.getByRole("button", { name: "Apply period" }));

    await waitFor(() => expect(mockedFetchAssetProfile).toHaveBeenLastCalledWith("10003G", "2026-03-01", "2026-09-30"));
    await waitFor(() =>
      expect((capturedTimeline.props as { tasks: unknown[] }).tasks).toEqual(
        expect.arrayContaining([
          expect.objectContaining({
            id: "on-hold-1",
            end: "2026-09-30",
            period: expect.objectContaining({ end: null }),
          }),
        ]),
      ),
    );
  });
});
