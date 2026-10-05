import { act, cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import { fetchAssets } from "../../services/assetsService";
import { fetchAvailabilitySummary } from "../../services/availabilityService";
import { fetchAssetEvents } from "../../services/eventsService";
import { fetchDivisions, fetchWarehouses, type WarehouseLookup } from "../../services/lookupsService";
import type { Asset, AssetEvent } from "../../types";
import type { AvailabilityItem } from "../../types/availability";
import { AvailabilityPanel } from "./AvailabilityPanel";

vi.mock("../../services/availabilityService", () => ({
  fetchAvailabilitySummary: vi.fn(),
}));

vi.mock("../../services/assetsService", () => ({
  fetchAssets: vi.fn(),
}));

vi.mock("../../services/eventsService", () => ({
  fetchAssetEvents: vi.fn(),
}));

vi.mock("../../services/lookupsService", () => ({
  fetchDivisions: vi.fn(),
  fetchWarehouses: vi.fn(),
}));

const mockedFetchAvailabilitySummary = vi.mocked(fetchAvailabilitySummary);
const mockedFetchAssets = vi.mocked(fetchAssets);
const mockedFetchAssetEvents = vi.mocked(fetchAssetEvents);
const mockedFetchDivisions = vi.mocked(fetchDivisions);
const mockedFetchWarehouses = vi.mocked(fetchWarehouses);

const homeAvailability: AvailabilityItem = {
  warehouseCode: "EE0",
  warehouse: "East England",
  facility: "East",
  divisionCode: "01",
  divisionName: "United Kingdom",
  genericCode: "CH0100",
  genericDescription: "Chiller",
  itemNumber: "CH0100REVAIC",
  descriptionIntl: "Chiller 100kW Reversible Air-Cooled",
  available: 0,
  count: 1,
  genericOnly: false,
  reservationMode: "asset",
};

const additionalAvailability: AvailabilityItem = {
  ...homeAvailability,
  warehouseCode: "IE0",
  warehouse: "Dublin",
  facility: "Dublin",
  divisionCode: "02",
  divisionName: "Ireland",
  available: 4,
  count: 5,
};

const northAvailability: AvailabilityItem = {
  ...homeAvailability,
  warehouseCode: "EN0",
  warehouse: "North England",
  facility: "North",
};

const repairWarehouse: WarehouseLookup = {
  warehouseCode: "EE1",
  warehouse: "East repair",
  facility: "East",
  divisionCode: "01",
  divisionName: "United Kingdom",
};

const repairAsset: Asset = {
  id: "REPAIR-100",
  individualItemNumber: "REPAIR-100",
  itemNumber: "CH0100REVAIC",
  status: "Repair",
  warehouse: "EE0",
  division: "01",
  facility: null,
  estimatedReadyDate: null,
  telemetryStatus: null,
  agreementNumber: null,
  customerName: null,
  deliveryDate: null,
  agreementLineValidFromDate: null,
  agreementLineValidToDate: null,
  description: "Chiller 100kW Reversible Air-Cooled",
  warehouseLocation: null,
  collectionDate: null,
  terminationDate: null,
};

function makeAssetEvent(overrides: Partial<AssetEvent> = {}): AssetEvent {
  return {
    assetId: repairAsset.id,
    eventType: "RESERVED",
    startDate: "2026-08-20",
    endDate: "2026-09-10",
    title: "Reserved for A731959",
    cssClass: "reserved_event",
    ...overrides,
  };
}

describe("AvailabilityPanel", () => {
  beforeEach(async () => {
    vi.resetAllMocks();
    await i18n.changeLanguage("en");
    mockedFetchDivisions.mockResolvedValue([
      { code: "01", name: "United Kingdom" },
      { code: "02", name: "Ireland" },
    ]);
    mockedFetchAvailabilitySummary.mockResolvedValue([homeAvailability]);
    mockedFetchWarehouses.mockResolvedValue([]);
    mockedFetchAssets.mockResolvedValue([repairAsset]);
    mockedFetchAssetEvents.mockResolvedValue({});
  });

  afterEach(cleanup);

  it("shows catalogue facility names without changing facility selection keys", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([homeAvailability, { ...northAvailability, available: 1 }]);
    mockedFetchWarehouses.mockResolvedValue([
      { ...homeAvailability, facilityName: "Eastern operations" },
      { ...northAvailability, facilityName: "Northern operations" },
    ]);
    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" />);

    expect(await screen.findByRole("columnheader", { name: "East Eastern operations" })).toBeInTheDocument();
    const selector = screen.getByLabelText("Facilities");
    expect(selector).toHaveTextContent("North — Northern operations");
    await user.selectOptions(selector, "01|NORTH");
    expect(
      await screen.findByRole("button", { name: "Hide North — Northern operations facility in division 01" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /EN0/ })).toBeInTheDocument();
  });

  it("expands every stocked item in one warehouse, including more than 20 assets, and switches warehouses", async () => {
    const user = userEvent.setup();
    const reserve = vi.fn();
    mockedFetchAvailabilitySummary.mockResolvedValue([
      { ...homeAvailability, count: 25 },
      { ...homeAvailability, itemNumber: "SECOND", available: 1 },
      { ...northAvailability, available: 2, count: 2 },
    ]);
    mockedFetchAssets.mockImplementation(async (params) =>
      Array.from({ length: params.itemNumber === "SECOND" ? 1 : 25 }, (_, index) => ({
        ...repairAsset,
        id: `${params.warehouse}-${params.itemNumber}-${index}`,
        itemNumber: params.itemNumber!,
      })),
    );
    render(
      <AvailabilityPanel
        genericCode="CH0100"
        warehouse="EE0"
        division="01"
        onReserveAsset={reserve}
        startDate="2023-04-03T00:00:00+01:00"
        endDate="2027-04-03T00:00:00+01:00"
      />,
    );
    await screen.findByText("SECOND");
    const viewOptions = screen.getByText("View options").closest("details");
    await user.click(screen.getByText("View options"));
    await user.click(screen.getByRole("checkbox", { name: "Single warehouse — all stock options" }));
    expect(viewOptions).not.toHaveAttribute("open");
    expect(await screen.findByText("EE0-CH0100REVAIC-24")).toBeInTheDocument();
    expect(await screen.findByText("EE0-SECOND-0")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /View assets for/ })).not.toBeInTheDocument();
    expect(screen.getAllByRole("columnheader", { name: "Asset ID" })).toHaveLength(1);
    expect(screen.getAllByRole("table")).toHaveLength(1);
    expect(screen.getByRole("columnheader", { name: "Item Number" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /Apr 3, 2023\s*Apr 3, 2027/ })).toBeInTheDocument();
    expect(screen.queryByText("0 / 25")).not.toBeInTheDocument();
    expect(screen.queryByText(/Assets .* at East England/)).not.toBeInTheDocument();
    expect(mockedFetchAssets).toHaveBeenCalledWith(expect.objectContaining({ warehouse: "EE0", exactMatch: true }));
    expect(mockedFetchAssets.mock.calls.every(([params]) => params.take === undefined)).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reserve EE0-CH0100REVAIC-0" }));
    expect(reserve).toHaveBeenCalledWith(expect.objectContaining({ id: "EE0-CH0100REVAIC-0" }));
    await user.click(screen.getByRole("combobox", { name: "Single warehouse" }));
    await user.click(screen.getByRole("option", { name: "EN0 — North England" }));
    expect(await screen.findByText("EN0-CH0100REVAIC-24")).toBeInTheDocument();
    expect(screen.queryByText("EE0-SECOND-0")).not.toBeInTheDocument();
    expect(screen.queryByText("SECOND")).not.toBeInTheDocument();
    await user.click(screen.getByText("View options"));
    await user.click(screen.getByRole("checkbox", { name: "Single warehouse — all stock options" }));
    expect(await screen.findByRole("button", { name: /View assets for CH0100REVAIC at EE0/ })).toBeInTheDocument();
    expect(screen.queryByText("EN0-CH0100REVAIC-24")).not.toBeInTheDocument();
  });

  it("labels and colour-codes assets by availability for the selected period", async () => {
    const user = userEvent.setup();
    const availableAsset = {
      ...repairAsset,
      id: "AVAILABLE-100",
      individualItemNumber: "AVAILABLE-100",
      status: "Available",
    };
    const committedAsset = {
      ...availableAsset,
      id: "COMMITTED-200",
      individualItemNumber: "COMMITTED-200",
    };
    mockedFetchAssets.mockResolvedValue([availableAsset, committedAsset]);
    mockedFetchAssetEvents.mockResolvedValue({
      "COMMITTED-200": [makeAssetEvent({ assetId: "COMMITTED-200" })],
    });

    render(
      <AvailabilityPanel
        genericCode="CH0100"
        warehouse="EE0"
        division="01"
        startDate="2026-09-01"
        endDate="2026-09-30"
      />,
    );
    await screen.findByText("CH0100REVAIC");
    await user.click(screen.getByText("View options"));
    await user.click(screen.getByRole("checkbox", { name: "Single warehouse — all stock options" }));

    const availableRow = (await screen.findByText("AVAILABLE-100")).closest("tr");
    const unavailableRow = screen.getByText("COMMITTED-200").closest("tr");
    expect(availableRow).toHaveAttribute("data-availability", "available");
    expect(within(availableRow!).getByLabelText("Available")).toHaveTextContent("✓");
    expect(unavailableRow).toHaveAttribute("data-availability", "unavailable");
    expect(within(unavailableRow!).getByLabelText("Unavailable")).toHaveTextContent("×");
    expect(within(unavailableRow!).queryByText("Available")).not.toBeInTheDocument();
  });

  it("shows quantity options expanded in read-only mode without allowing reservations", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([
      { ...homeAvailability, reservationMode: "quantity", available: 8, count: 10 },
    ]);
    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" readOnly onReserveStock={vi.fn()} />);
    await screen.findByText("CH0100REVAIC");
    await user.click(screen.getByText("View options"));
    await user.click(screen.getByRole("checkbox", { name: "Single warehouse — all stock options" }));
    expect(screen.getByText("Quantity stock").closest("tr")).toHaveAttribute("data-availability", "available");
    expect(screen.getByLabelText("Available")).toHaveTextContent("✓");
    expect(screen.getByRole("spinbutton")).toBeDisabled();
    expect(screen.getByRole("button", { name: /Reserve 1/ })).toBeDisabled();
    expect(mockedFetchAssets).not.toHaveBeenCalled();
  });

  it("marks related specific substitutions and requests them for the selected item", async () => {
    mockedFetchAvailabilitySummary.mockResolvedValue([
      homeAvailability,
      {
        ...homeAvailability,
        itemNumber: "CH0100RELATED",
        substitutionReason: "RELATED",
      },
    ]);

    render(<AvailabilityPanel genericCode="CH0100" itemNumber="CH0100BASE" division="01" lineId={73} />);

    expect(await screen.findByLabelText("Related substitute")).toHaveAttribute("title", "Related substitute");
    expect(mockedFetchAvailabilitySummary).toHaveBeenCalledWith(
      "CH0100",
      undefined,
      undefined,
      undefined,
      "01",
      "CH0100BASE",
      73,
    );
  });

  it("allows a zero-availability cell to select a repair asset in its division", async () => {
    const user = userEvent.setup();
    const onReserveAsset = vi.fn();

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={onReserveAsset} />);

    const zeroCell = await screen.findByRole("button", {
      name: "View assets for CH0100REVAIC at EE0: 0 of 1 available",
    });
    expect(zeroCell).toHaveTextContent("0 / 1");
    await user.click(zeroCell);

    await waitFor(() => {
      expect(mockedFetchAssets).toHaveBeenCalledWith({
        itemNumber: "CH0100REVAIC",
        warehouse: "EE0",
        division: "01",
        excludeStatuses: "RemovedStock,Scrap,Sold",
        take: 20,
        exactMatch: true,
      });
    });
    expect(screen.getByRole("columnheader", { name: "Asset ID" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Status" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: repairAsset.id })).toBeInTheDocument();
    expect(await screen.findByText("Repair")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Reserve" }));
    expect(onReserveAsset).toHaveBeenCalledWith(repairAsset);

    await user.selectOptions(screen.getByLabelText("Divisions"), "02");
    expect(
      await screen.findByRole("button", {
        name: "View assets for CH0100REVAIC at EE0: 0 of 1 available",
      }),
    ).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByText("Repair")).not.toBeInTheDocument();
  });

  it("shows warehouses with a zero-availability result by default and hides warehouses with no result", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([
      homeAvailability,
      { ...homeAvailability, itemNumber: "CH0100REMOTE", count: 0 },
      {
        ...homeAvailability,
        itemNumber: "CH0100REMOTE",
        warehouseCode: "EE1",
        warehouse: "East repair",
        count: 1,
      },
      { ...homeAvailability, itemNumber: "CH0100EMPTY", count: 0 },
    ]);
    mockedFetchWarehouses.mockResolvedValue([{ ...repairWarehouse, warehouseCode: "EN1", warehouse: "East empty" }]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={vi.fn()} />);

    expect(await screen.findByText("CH0100REVAIC")).toBeInTheDocument();
    expect(await screen.findByText("CH0100REMOTE")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "View assets for CH0100REMOTE at EE1: 0 of 1 available" }),
    ).toHaveTextContent("0 / 1");
    expect(screen.queryByText("CH0100EMPTY")).not.toBeInTheDocument();

    const warehouseSelector = screen.getByLabelText("Warehouses");
    await waitFor(() => expect(warehouseSelector).toHaveTextContent("EN1"));
    expect(screen.queryByRole("columnheader", { name: /EN1/ })).not.toBeInTheDocument();

    await user.selectOptions(warehouseSelector, "EN1");
    expect(await screen.findByRole("columnheader", { name: /EN1/ })).toBeInTheDocument();
  });

  it("loads one schedule batch and shows asset location, commitments, and an explicit empty schedule", async () => {
    const user = userEvent.setup();
    const secondAsset = {
      ...repairAsset,
      id: "READY-200",
      individualItemNumber: "READY-200",
      status: "Ready",
      warehouseLocation: "Ready bay 2",
    };
    mockedFetchAssets.mockResolvedValue([{ ...repairAsset, facility: "East" }, secondAsset]);
    mockedFetchAssetEvents.mockResolvedValue({
      " repair-100 ": [
        makeAssetEvent(),
        makeAssetEvent({
          eventType: "SERVICE",
          startDate: "2026-09-15",
          endDate: "2026-09-16",
          title: "Planned service",
          cssClass: "service_event",
        }),
      ],
    });

    render(
      <AvailabilityPanel
        genericCode="CH0100"
        warehouse="EE0"
        division="01"
        startDate="2026-09-01"
        endDate="2026-09-30"
        onReserveAsset={vi.fn()}
      />,
    );

    await user.click(
      await screen.findByRole("button", {
        name: "View assets for CH0100REVAIC at EE0: 0 of 1 available",
      }),
    );

    await waitFor(() =>
      expect(mockedFetchAssetEvents).toHaveBeenCalledWith({
        startDate: "2026-09-01",
        endDate: "2026-09-30",
        divisions: "01",
        assetIds: ["REPAIR-100", "READY-200"],
      }),
    );
    expect(screen.getByRole("columnheader", { name: "Location" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /Commitments.*Sep 1, 2026 to Sep 30, 2026/ })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "East — EE0" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Ready bay 2" })).toBeInTheDocument();
    expect(await screen.findByText("Reserved for A731959")).toBeInTheDocument();
    expect(screen.getByText("Aug 20, 2026 to Sep 10, 2026")).toBeInTheDocument();
    expect(screen.getByText("Planned service")).toBeInTheDocument();
    expect(screen.getByText("No reservations in this period")).toBeInTheDocument();
    expect(mockedFetchAssetEvents).toHaveBeenCalledTimes(1);
  });

  it("keeps assets and Reserve available when schedule context fails, then retries only the schedule", async () => {
    const user = userEvent.setup();
    const onReserveAsset = vi.fn();
    mockedFetchAssetEvents.mockRejectedValueOnce(new Error("events unavailable")).mockResolvedValueOnce({});

    render(
      <AvailabilityPanel
        genericCode="CH0100"
        warehouse="EE0"
        division="01"
        startDate="2026-09-01"
        endDate="2026-09-30"
        onReserveAsset={onReserveAsset}
      />,
    );

    await user.click(
      await screen.findByRole("button", {
        name: "View assets for CH0100REVAIC at EE0: 0 of 1 available",
      }),
    );

    expect(await screen.findByText("Reservation context could not be loaded.")).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: repairAsset.id })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Reserve" }));
    expect(onReserveAsset).toHaveBeenCalledWith(repairAsset);

    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("No reservations in this period")).toBeInTheDocument();
    expect(screen.queryByText("Reservation context could not be loaded.")).not.toBeInTheDocument();
    expect(mockedFetchAssets).toHaveBeenCalledTimes(1);
    expect(mockedFetchAssetEvents).toHaveBeenCalledTimes(2);
  });

  it("keeps the latest cell schedule when an older event request completes last", async () => {
    const user = userEvent.setup();
    let resolveStaleEvents: ((events: Record<string, AssetEvent[]>) => void) | undefined;
    mockedFetchAvailabilitySummary.mockResolvedValue([
      { ...homeAvailability, available: 1 },
      { ...homeAvailability, warehouseCode: "EE1", warehouse: "East repair", available: 1 },
    ]);
    mockedFetchAssetEvents
      .mockImplementationOnce(
        () =>
          new Promise<Record<string, AssetEvent[]>>((resolve) => {
            resolveStaleEvents = resolve;
          }),
      )
      .mockResolvedValueOnce({
        "REPAIR-100": [makeAssetEvent({ title: "Latest reservation" })],
      });

    render(
      <AvailabilityPanel
        genericCode="CH0100"
        warehouse="EE0"
        division="01"
        startDate="2026-09-01"
        endDate="2026-09-30"
        onReserveAsset={vi.fn()}
      />,
    );

    await user.click(
      await screen.findByRole("button", {
        name: "View assets for CH0100REVAIC at EE0: 1 of 1 available",
      }),
    );
    await waitFor(() => expect(mockedFetchAssetEvents).toHaveBeenCalledTimes(1));
    await user.click(
      screen.getByRole("button", {
        name: "View assets for CH0100REVAIC at EE1: 1 of 1 available",
      }),
    );

    expect(await screen.findByText("Latest reservation")).toBeInTheDocument();
    await act(async () => resolveStaleEvents?.({ "REPAIR-100": [makeAssetEvent({ title: "Stale reservation" })] }));
    expect(screen.getByText("Latest reservation")).toBeInTheDocument();
    expect(screen.queryByText("Stale reservation")).not.toBeInTheDocument();
  });

  it("reserves the quantity of non-serialised stock entered by the user", async () => {
    const user = userEvent.setup();
    const onReserveStock = vi.fn().mockResolvedValue(undefined);
    mockedFetchAvailabilitySummary.mockResolvedValue([
      {
        ...homeAvailability,
        available: 10,
        count: 20,
        reservationMode: "quantity",
      },
    ]);

    render(
      <AvailabilityPanel
        genericCode="CH0100"
        warehouse="EE0"
        division="01"
        lineId={73}
        requiredQuantity={4}
        fulfilledQuantity={1}
        onReserveStock={onReserveStock}
      />,
    );

    await user.click(
      await screen.findByRole("button", {
        name: "Allocate quantity stock for CH0100REVAIC at EE0: 10 of 20 available",
      }),
    );

    expect(mockedFetchAvailabilitySummary).toHaveBeenCalledWith(
      "CH0100",
      undefined,
      undefined,
      undefined,
      "01",
      undefined,
      73,
    );
    expect(mockedFetchAssets).not.toHaveBeenCalled();
    expect(screen.getByText("Reserve CH0100REVAIC — East England")).toBeInTheDocument();
    expect(screen.queryByText("Stock available: 10")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Quantity to reserve")).toHaveValue(3);
    expect(screen.queryByText(/agreement-line unit/i)).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Reserve 3 stock units" }));
    await waitFor(() => {
      expect(onReserveStock).toHaveBeenCalledWith({
        lineId: 73,
        itemNumber: "CH0100REVAIC",
        warehouse: "EE0",
        quantity: 3,
      });
    });
  });

  it("adds and removes an authorised division and groups its warehouses", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockImplementation(async (_generic, _attributes, _start, _end, divisions) =>
      divisions === "01,02" ? [homeAvailability, additionalAvailability] : [homeAvailability],
    );

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" />);

    const agreementWarehouseHeader = await screen.findByRole("columnheader", {
      name: /EE0.*East England.*agreement warehouse/i,
    });
    expect(agreementWarehouseHeader).toHaveTextContent(/^EE0\s*✓$/);
    expect(agreementWarehouseHeader).not.toHaveTextContent(/agreement/i);
    expect(screen.queryByRole("button", { name: "Remove 01 division" })).not.toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText("Divisions"), "02");

    await waitFor(() => {
      expect(mockedFetchAvailabilitySummary).toHaveBeenLastCalledWith(
        "CH0100",
        undefined,
        undefined,
        undefined,
        "01,02",
        undefined,
        undefined,
      );
    });

    const facilitySelector = screen.getByLabelText("Facilities");
    expect(await screen.findByRole("columnheader", { name: /02.*Ireland/ })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Dublin" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /IE0/ })).toBeInTheDocument();
    expect(facilitySelector).toHaveTextContent("All facilities shown");

    const warehouseHeaders = screen.getAllByRole("columnheader").map((header) => header.textContent ?? "");
    expect(warehouseHeaders.findIndex((header) => header.startsWith("EE0"))).toBeLessThan(
      warehouseHeaders.findIndex((header) => header.startsWith("IE0")),
    );

    await user.click(screen.getByRole("button", { name: "Remove 02 division" }));
    expect(screen.getByRole("combobox", { name: "Choose divisions" })).toHaveFocus();
    await waitFor(() => {
      expect(mockedFetchAvailabilitySummary).toHaveBeenLastCalledWith(
        "CH0100",
        undefined,
        undefined,
        undefined,
        "01",
        undefined,
        undefined,
      );
    });
    expect(screen.queryByRole("columnheader", { name: /IE0/ })).not.toBeInTheDocument();
  });

  it("hides and restores non-agreement facilities without refetching availability", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([homeAvailability, { ...northAvailability, available: 1 }]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={vi.fn()} />);

    expect(await screen.findByRole("columnheader", { name: /EE0/ })).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /EN0/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Hide East facility in division 01" })).not.toBeInTheDocument();

    const facilitySelector = screen.getByLabelText("Facilities");
    expect(facilitySelector).toHaveTextContent("North");
    const availabilityCallCount = mockedFetchAvailabilitySummary.mock.calls.length;

    await user.selectOptions(facilitySelector, "01|NORTH");
    expect(await screen.findByRole("columnheader", { name: /EN0/ })).toBeInTheDocument();
    expect(facilitySelector).toHaveTextContent("All facilities shown");
    expect(mockedFetchAvailabilitySummary).toHaveBeenCalledTimes(availabilityCallCount);

    await user.click(
      screen.getByRole("button", {
        name: "View assets for CH0100REVAIC at EN0: 1 of 1 available",
      }),
    );
    expect(await screen.findByText("Repair")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Hide North facility in division 01" }));

    expect(screen.getByRole("combobox", { name: "Choose facilities" })).toHaveFocus();
    expect(screen.queryByRole("columnheader", { name: /EN0/ })).not.toBeInTheDocument();
    expect(screen.queryByText("Repair")).not.toBeInTheDocument();
    expect(facilitySelector).toHaveTextContent("North");
    expect(mockedFetchAvailabilitySummary).toHaveBeenCalledTimes(availabilityCallCount);

    await user.selectOptions(facilitySelector, "01|NORTH");

    expect(await screen.findByRole("columnheader", { name: /EN0/ })).toBeInTheDocument();
    expect(facilitySelector).toHaveTextContent("All facilities shown");
    expect(mockedFetchAvailabilitySummary).toHaveBeenCalledTimes(availabilityCallCount);
  });

  it("hides and restores a non-agreement warehouse without refetching or reviving stale assets", async () => {
    const user = userEvent.setup();
    let resolveAssets: ((assets: Asset[]) => void) | undefined;
    mockedFetchAvailabilitySummary.mockResolvedValue([
      homeAvailability,
      { ...homeAvailability, warehouseCode: " ee1 ", warehouse: "East local" },
    ]);
    mockedFetchAssets.mockImplementation(
      () =>
        new Promise<Asset[]>((resolve) => {
          resolveAssets = resolve;
        }),
    );

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={vi.fn()} />);

    expect(
      screen.queryByRole("button", { name: "Hide EE0 warehouse in East facility, division 01" }),
    ).not.toBeInTheDocument();
    const warehouseSelector = screen.getByLabelText("Warehouses");
    expect(await screen.findByRole("columnheader", { name: /EE1/ })).toBeInTheDocument();
    await user.click(
      await screen.findByRole("button", {
        name: "View assets for CH0100REVAIC at EE1: 0 of 1 available",
      }),
    );
    await waitFor(() => expect(mockedFetchAssets).toHaveBeenCalledTimes(1));

    const availabilityCallCount = mockedFetchAvailabilitySummary.mock.calls.length;
    await user.click(screen.getByRole("button", { name: "Hide EE1 warehouse in East facility, division 01" }));

    expect(screen.getByRole("combobox", { name: "Choose warehouses" })).toHaveFocus();
    expect(screen.queryByRole("columnheader", { name: /EE1/ })).not.toBeInTheDocument();
    expect(warehouseSelector).toHaveTextContent("EE1");
    expect(mockedFetchAvailabilitySummary).toHaveBeenCalledTimes(availabilityCallCount);

    await act(async () => resolveAssets?.([repairAsset]));
    expect(screen.queryByText("Repair")).not.toBeInTheDocument();

    await user.selectOptions(warehouseSelector, "EE1");
    expect(await screen.findByRole("columnheader", { name: /EE1/ })).toBeInTheDocument();
    expect(warehouseSelector).toHaveTextContent("All warehouses shown");
    expect(mockedFetchAvailabilitySummary).toHaveBeenCalledTimes(availabilityCallCount);
  });

  it("keeps a manually restored empty warehouse visible when the fulfilment line changes", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([
      homeAvailability,
      { ...homeAvailability, warehouseCode: "EE1", warehouse: "East repair", count: 0 },
    ]);
    const { rerender } = render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" lineId={10} />);

    const warehouseSelector = screen.getByLabelText("Warehouses");
    await waitFor(() => expect(warehouseSelector).toHaveTextContent("EE1"));
    await user.selectOptions(warehouseSelector, "EE1");
    expect(await screen.findByRole("columnheader", { name: /EE1/ })).toBeInTheDocument();
    await user.click(screen.getByLabelText("Include empty warehouses"));

    rerender(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" lineId={11} />);

    expect(await screen.findByRole("columnheader", { name: /EE1/ })).toBeInTheDocument();
    expect(screen.getByLabelText("Include empty warehouses")).toBeChecked();
  });

  it("uses clean warehouse labels when hierarchy metadata is unavailable", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([
      homeAvailability,
      {
        ...homeAvailability,
        warehouseCode: "ZZ1",
        warehouse: "Overflow",
        facility: "",
        divisionCode: "",
        divisionName: "",
        count: 0,
      },
    ]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" />);

    const warehouseSelector = screen.getByLabelText("Warehouses");
    await waitFor(() => expect(warehouseSelector).toHaveTextContent("ZZ1 — Overflow"));
    expect(screen.queryByRole("columnheader", { name: /ZZ1/ })).not.toBeInTheDocument();

    await user.selectOptions(warehouseSelector, "ZZ1");
    expect(await screen.findByRole("button", { name: "Hide unknown facility" })).toBeInTheDocument();
    await user.click(await screen.findByRole("button", { name: "Hide ZZ1 warehouse" }));

    expect(warehouseSelector).toHaveTextContent("ZZ1 — Overflow");
    expect(warehouseSelector).not.toHaveTextContent(/—\s*—/);
  });

  it("protects and labels an agreement facility when its metadata is blank", async () => {
    mockedFetchAvailabilitySummary.mockResolvedValue([{ ...homeAvailability, facility: "" }, northAvailability]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" />);

    expect(await screen.findByRole("columnheader", { name: "Unknown facility" })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /EE0.*agreement warehouse/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Hide unknown facility" })).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /EN0/ })).not.toBeInTheDocument();
    expect(screen.getByLabelText("Facilities")).toHaveTextContent("North");
  });

  it("prunes empty location headers and keeps colspans aligned after hiding a sole warehouse", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([homeAvailability, additionalAvailability]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={vi.fn()} />);

    const agreementDivisionHeader = await screen.findByRole("columnheader", {
      name: /01.*United Kingdom.*Agreement/,
    });
    expect(await screen.findByRole("columnheader", { name: /IE0/ })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Hide IE0 warehouse in Dublin facility, division 02" }));

    expect(screen.queryByRole("columnheader", { name: /02.*Ireland/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Dublin" })).not.toBeInTheDocument();
    expect(agreementDivisionHeader).toHaveAttribute("colspan", "1");

    await user.click(
      screen.getByRole("button", {
        name: "View assets for CH0100REVAIC at EE0: 0 of 1 available",
      }),
    );
    const assetHeading = await screen.findByText(/Assets.*CH0100REVAIC.*East England/);
    expect(assetHeading.closest("td")).toHaveAttribute("colspan", "3");
  });

  it("treats differently cased agreement and response division codes as the same division", async () => {
    mockedFetchDivisions.mockResolvedValue([
      { code: "uk", name: "United Kingdom" },
      { code: "aa", name: "Another area" },
    ]);
    mockedFetchAvailabilitySummary.mockResolvedValue([
      { ...homeAvailability, divisionCode: "AA", divisionName: "Another area", warehouseCode: "AA0" },
      { ...homeAvailability, divisionCode: "UK", warehouseCode: "UK0" },
    ]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="UK0" division="uk" />);

    expect(await screen.findByRole("columnheader", { name: /UK.*United Kingdom.*Agreement/ })).toBeInTheDocument();
    const divisionSelector = screen.getByLabelText("Divisions");
    expect(divisionSelector).not.toHaveTextContent("UK — United Kingdom");
    expect(screen.queryByRole("button", { name: "Remove UK division" })).not.toBeInTheDocument();
  });

  it("includes empty warehouses on request without refetching", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([
      homeAvailability,
      { ...homeAvailability, warehouseCode: " ee1 ", warehouse: "East local", count: 0 },
      { ...homeAvailability, warehouseCode: "EE3", warehouse: "Central fleet", count: 0 },
    ]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={vi.fn()} />);

    expect(await screen.findByRole("columnheader", { name: /EE0/ })).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /EE1/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /EE3/ })).not.toBeInTheDocument();
    expect(
      screen.getByText("No available assets are reported. Use Add warehouse to inspect another warehouse."),
    ).toBeInTheDocument();

    const availabilityCallCount = mockedFetchAvailabilitySummary.mock.calls.length;
    const warehouseSelector = screen.getByLabelText("Warehouses");
    expect(warehouseSelector).toHaveTextContent("EE1");
    expect(warehouseSelector).toHaveTextContent("EE3");
    const viewOptionsTrigger = screen.getByText("View options").closest("summary");
    const viewOptions = viewOptionsTrigger?.closest("details");
    expect(viewOptionsTrigger).toBeInTheDocument();
    expect(viewOptions).not.toHaveAttribute("open");
    await user.click(viewOptionsTrigger!);
    expect(viewOptions).toHaveAttribute("open");
    const includeEmptyToggle = screen.getByLabelText("Include empty warehouses");
    expect(includeEmptyToggle).not.toBeChecked();
    await user.click(includeEmptyToggle);
    expect(viewOptions).not.toHaveAttribute("open");
    expect(within(viewOptionsTrigger!).getByText("1")).toBeInTheDocument();

    expect(await screen.findByRole("columnheader", { name: /EE1/ })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /EE3/ })).toBeInTheDocument();
    expect(screen.queryByText(/No available assets are reported/)).not.toBeInTheDocument();
    expect(mockedFetchAvailabilitySummary).toHaveBeenCalledTimes(availabilityCallCount);

    await user.click(viewOptionsTrigger!);
    await user.click(screen.getByLabelText("Include empty warehouses"));
    expect(viewOptions).not.toHaveAttribute("open");
    await waitFor(() => expect(screen.queryByRole("columnheader", { name: /EE1/ })).not.toBeInTheDocument());
    expect(within(viewOptionsTrigger!).queryByText("1")).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /EE3/ })).not.toBeInTheDocument();
    expect(warehouseSelector).toHaveTextContent("EE1");
    expect(warehouseSelector).toHaveTextContent("EE3");
  });

  it("keeps an individually restored warehouse visible when include-empty is switched off", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([
      homeAvailability,
      { ...homeAvailability, warehouseCode: "EE1", warehouse: "East local", count: 0 },
    ]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" />);

    const warehouseSelector = screen.getByLabelText("Warehouses");
    await waitFor(() => expect(warehouseSelector).toHaveTextContent("EE1"));
    await user.selectOptions(warehouseSelector, "EE1");
    expect(await screen.findByRole("columnheader", { name: /EE1/ })).toBeInTheDocument();

    const includeEmptyToggle = screen.getByLabelText("Include empty warehouses");
    await user.click(includeEmptyToggle);
    await user.click(includeEmptyToggle);

    expect(screen.getByRole("columnheader", { name: /EE1/ })).toBeInTheDocument();
    expect(warehouseSelector).toHaveTextContent("All warehouses shown");
  });

  it("does not reveal a manually hidden warehouse when empty warehouses are included", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([
      homeAvailability,
      { ...homeAvailability, warehouseCode: "EE1", warehouse: "East local", available: 1 },
    ]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" />);

    expect(await screen.findByRole("columnheader", { name: /EE1/ })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Hide EE1 warehouse in East facility, division 01" }));
    expect(screen.queryByRole("columnheader", { name: /EE1/ })).not.toBeInTheDocument();

    await user.click(screen.getByLabelText("Include empty warehouses"));

    expect(screen.queryByRole("columnheader", { name: /EE1/ })).not.toBeInTheDocument();
    expect(screen.getByLabelText("Warehouses")).toHaveTextContent("EE1");
  });

  it("keeps configured empty warehouses restorable as genuine missing-data cells", async () => {
    const user = userEvent.setup();
    mockedFetchWarehouses.mockResolvedValue([
      repairWarehouse,
      {
        ...repairWarehouse,
        warehouseCode: " ee3 ",
        warehouse: "Central fleet",
        facility: "Central",
      },
    ]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={vi.fn()} />);

    expect(await screen.findByRole("columnheader", { name: /EE0/ })).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /EE1/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /EE3/ })).not.toBeInTheDocument();
    expect(mockedFetchWarehouses).toHaveBeenCalledWith("01");

    const warehouseSelector = screen.getByLabelText("Warehouses");
    await waitFor(() => expect(warehouseSelector).toHaveTextContent("EE1"));
    await user.selectOptions(warehouseSelector, "EE1");

    const itemRow = screen.getByText("CH0100REVAIC").closest("tr");
    expect(itemRow).not.toBeNull();
    const localRepairCell = within(itemRow!).getAllByRole("cell").at(-1);
    expect(localRepairCell).toHaveTextContent("\u2014");
    expect(within(localRepairCell!).queryByRole("button")).not.toBeInTheDocument();

    expect(screen.queryByRole("columnheader", { name: /EE3/ })).not.toBeInTheDocument();

    await user.selectOptions(warehouseSelector, "EE3");

    expect(await screen.findByRole("columnheader", { name: /EE3/ })).toBeInTheDocument();
    const centralCell = within(itemRow!).getAllByRole("cell").at(-1);
    expect(centralCell).toHaveTextContent("\u2014");
    expect(within(centralCell!).queryByRole("button")).not.toBeInTheDocument();
  });

  it("keeps the agreement facility protected for the selected warehouse", async () => {
    const user = userEvent.setup();
    mockedFetchAvailabilitySummary.mockResolvedValue([
      { ...homeAvailability, count: 0 },
      { ...northAvailability, count: 0 },
      {
        ...homeAvailability,
        warehouseCode: "EE3",
        warehouse: "Central fleet",
        facility: "East",
      },
    ]);

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE3" division="01" />);

    const agreementWarehouseHeader = await screen.findByRole("columnheader", {
      name: /EE3.*Central fleet.*agreement warehouse/i,
    });
    expect(agreementWarehouseHeader).toHaveTextContent(/^EE3\s*✓$/);
    expect(screen.queryByRole("columnheader", { name: /EE0/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Hide East facility in division 01" })).not.toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /EN0/ })).not.toBeInTheDocument();

    const facilitySelector = screen.getByLabelText("Facilities");
    expect(facilitySelector).toHaveTextContent("North");
    await user.selectOptions(screen.getByLabelText("Warehouses"), "EN0");
    expect(await screen.findByRole("button", { name: "Hide North facility in division 01" })).toBeInTheDocument();

    expect(screen.getByRole("columnheader", { name: /EE3.*agreement warehouse/i })).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Hide EE3 warehouse in East facility, division 01" }),
    ).not.toBeInTheDocument();
  });

  it("closes an expanded cell when a late catalogue resolves a different agreement facility", async () => {
    const user = userEvent.setup();
    let resolveCatalog: ((warehouses: WarehouseLookup[]) => void) | undefined;
    let resolveAssets: ((assets: Asset[]) => void) | undefined;
    mockedFetchWarehouses.mockImplementation(
      () =>
        new Promise<WarehouseLookup[]>((resolve) => {
          resolveCatalog = resolve;
        }),
    );
    mockedFetchAvailabilitySummary.mockResolvedValue([homeAvailability, { ...northAvailability, available: 1 }]);
    mockedFetchAssets.mockImplementation(
      () =>
        new Promise<Asset[]>((resolve) => {
          resolveAssets = resolve;
        }),
    );

    render(<AvailabilityPanel genericCode="CH0100" warehouse="AG0" division="01" onReserveAsset={vi.fn()} />);

    await user.click(
      await screen.findByRole("button", {
        name: "View assets for CH0100REVAIC at EN0: 1 of 1 available",
      }),
    );
    expect(await screen.findByText(/Assets.*CH0100REVAIC.*North England/)).toBeInTheDocument();

    await act(async () =>
      resolveCatalog?.([
        {
          warehouseCode: "AG0",
          warehouse: "Agreement depot",
          facility: "East",
          divisionCode: "01",
          divisionName: "United Kingdom",
        },
      ]),
    );

    await waitFor(() => expect(screen.queryByRole("columnheader", { name: /EN0/ })).not.toBeInTheDocument());
    expect(screen.queryByText(/Assets.*CH0100REVAIC.*North England/)).not.toBeInTheDocument();

    await act(async () => resolveAssets?.([repairAsset]));
    expect(screen.queryByText("Repair")).not.toBeInTheDocument();
  });

  it("ignores an older availability response after divisions change", async () => {
    const user = userEvent.setup();
    let resolveInitialRequest: ((items: AvailabilityItem[]) => void) | undefined;
    mockedFetchAvailabilitySummary
      .mockImplementationOnce(
        () =>
          new Promise<AvailabilityItem[]>((resolve) => {
            resolveInitialRequest = resolve;
          }),
      )
      .mockResolvedValueOnce([homeAvailability, additionalAvailability]);

    render(<AvailabilityPanel genericCode="CH0100" division="01" />);

    await waitFor(() => expect(screen.getByLabelText("Divisions")).toBeEnabled());
    await user.selectOptions(screen.getByLabelText("Divisions"), "02");
    expect(await screen.findByRole("columnheader", { name: /IE0/ })).toBeInTheDocument();

    await act(async () => resolveInitialRequest?.([homeAvailability]));

    expect(screen.getByRole("columnheader", { name: /IE0/ })).toBeInTheDocument();
  });

  it("ignores an older warehouse catalogue after divisions change", async () => {
    const user = userEvent.setup();
    let resolveInitialCatalog: ((warehouses: WarehouseLookup[]) => void) | undefined;
    mockedFetchWarehouses
      .mockImplementationOnce(
        () =>
          new Promise<WarehouseLookup[]>((resolve) => {
            resolveInitialCatalog = resolve;
          }),
      )
      .mockResolvedValueOnce([
        {
          ...repairWarehouse,
          warehouseCode: "IE1",
          warehouse: "Dublin repair",
          facility: "Dublin",
          divisionCode: "02",
          divisionName: "Ireland",
        },
      ]);
    mockedFetchAvailabilitySummary
      .mockResolvedValueOnce([homeAvailability])
      .mockResolvedValueOnce([homeAvailability, additionalAvailability]);

    render(<AvailabilityPanel genericCode="CH0100" division="01" />);

    await waitFor(() => expect(screen.getByLabelText("Divisions")).toBeEnabled());
    await user.selectOptions(screen.getByLabelText("Divisions"), "02");
    const warehouseSelector = screen.getByLabelText("Warehouses");
    await waitFor(() => expect(warehouseSelector).toHaveTextContent("IE1"));
    await user.selectOptions(warehouseSelector, "IE1");
    expect(await screen.findByRole("columnheader", { name: /IE1/ })).toBeInTheDocument();

    await act(async () =>
      resolveInitialCatalog?.([
        {
          ...repairWarehouse,
          warehouseCode: "ZZ1",
          warehouse: "Stale warehouse",
        },
      ]),
    );

    expect(screen.getByRole("columnheader", { name: /IE1/ })).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /ZZ1/ })).not.toBeInTheDocument();
  });

  it("keeps agreement availability usable when additional divisions fail to load", async () => {
    mockedFetchDivisions.mockRejectedValue(new Error("lookup unavailable"));

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={vi.fn()} />);

    expect(await screen.findByText("Additional divisions could not be loaded", { selector: "p" })).toBeInTheDocument();
    expect(
      await screen.findByRole("button", {
        name: "View assets for CH0100REVAIC at EE0: 0 of 1 available",
      }),
    ).toBeInTheDocument();
  });

  it("keeps summary availability usable when the configured warehouse catalogue fails", async () => {
    mockedFetchWarehouses.mockRejectedValue(new Error("warehouse lookup unavailable"));

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={vi.fn()} />);

    expect(await screen.findByText("Configured warehouses could not be loaded", { selector: "p" })).toBeInTheDocument();
    expect(
      await screen.findByRole("button", {
        name: "View assets for CH0100REVAIC at EE0: 0 of 1 available",
      }),
    ).toBeInTheDocument();
  });

  it("distinguishes an asset lookup failure from a genuinely empty warehouse", async () => {
    const user = userEvent.setup();
    mockedFetchAssets.mockRejectedValue(new Error("asset lookup unavailable"));

    render(<AvailabilityPanel genericCode="CH0100" warehouse="EE0" division="01" onReserveAsset={vi.fn()} />);

    await user.click(
      await screen.findByRole("button", {
        name: "View assets for CH0100REVAIC at EE0: 0 of 1 available",
      }),
    );

    expect(await screen.findByRole("alert")).toHaveTextContent("Individual assets could not be loaded");
    expect(screen.queryByText("No individual assets found")).not.toBeInTheDocument();
  });

  it("disables the add control when the agreement is the only authorised division", async () => {
    mockedFetchDivisions.mockResolvedValue([{ code: "01", name: "United Kingdom" }]);

    render(<AvailabilityPanel genericCode="CH0100" division="01" />);

    const divisionSelector = screen.getByLabelText("Divisions");
    await waitFor(() => expect(divisionSelector).toHaveTextContent("No additional divisions"));
    expect(divisionSelector).toBeDisabled();
  });

  it("distinguishes availability failures from empty results", async () => {
    mockedFetchAvailabilitySummary.mockRejectedValue(new Error("availability unavailable"));

    render(<AvailabilityPanel genericCode="CH0100" division="01" />);

    expect(await screen.findByText("Failed to load availability data")).toBeInTheDocument();
    expect(screen.queryByText("No availability data found")).not.toBeInTheDocument();
  });

  it("shows the empty state when availability succeeds without rows", async () => {
    mockedFetchAvailabilitySummary.mockResolvedValue([]);

    render(<AvailabilityPanel genericCode="CH0100" division="01" />);

    expect(await screen.findByText("No availability data found")).toBeInTheDocument();
    expect(screen.queryByText("Failed to load availability data")).not.toBeInTheDocument();
  });
});
