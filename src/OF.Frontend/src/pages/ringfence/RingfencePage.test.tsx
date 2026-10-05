import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import {
  addRingfenceItems,
  fetchRingfence,
  fetchRingfences,
  preflightRingfenceItems,
  updateRingfence,
  type RingfenceDetail,
  type RingfenceItemBatchResult,
  type RingfenceListItem,
  type RingfenceOverlap,
} from "../../services/ringfenceService";
import { fetchDivisions, fetchUsers, fetchWarehouses } from "../../services/lookupsService";
import { RingfencePage } from "./RingfencePage";

const authMock = vi.hoisted(() => ({
  value: {
    user: {
      loginName: "planner@example.com",
      displayName: "Planner",
      division: "01",
      isAdmin: false,
      isSuperAdmin: false,
      isReadOnly: false,
      language: "en",
    },
    isLoading: false,
    error: null,
  },
}));

vi.mock("../../contexts/auth", () => ({
  useAuth: () => authMock.value,
}));

vi.mock("../../services/assetsService", () => ({
  removeAssetFromRingfence: vi.fn(),
}));

vi.mock("../../services/lookupsService", () => ({
  fetchDivisions: vi.fn(),
  fetchUsers: vi.fn(),
  fetchWarehouses: vi.fn(),
}));

vi.mock("../../services/ringfenceService", () => ({
  addRingfenceItems: vi.fn(),
  createRingfence: vi.fn(),
  deleteRingfence: vi.fn(),
  fetchRingfence: vi.fn(),
  fetchRingfences: vi.fn(),
  preflightRingfenceItems: vi.fn(),
  updateRingfence: vi.fn(),
}));

const mockedAddRingfenceItems = vi.mocked(addRingfenceItems);
const mockedFetchDivisions = vi.mocked(fetchDivisions);
const mockedFetchRingfence = vi.mocked(fetchRingfence);
const mockedFetchRingfences = vi.mocked(fetchRingfences);
const mockedFetchUsers = vi.mocked(fetchUsers);
const mockedFetchWarehouses = vi.mocked(fetchWarehouses);
const mockedPreflightRingfenceItems = vi.mocked(preflightRingfenceItems);
const mockedUpdateRingfence = vi.mocked(updateRingfence);

const originalShowModal = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, "showModal");
const originalClose = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, "close");

function createRingfence(id: number, title: string): RingfenceListItem {
  return {
    id,
    title,
    fromDate: "2026-08-01T00:00:00.000Z",
    toDate: "2026-08-31T00:00:00.000Z",
    divisions: "01",
    warehouse: "GLA",
    owner: "planner@example.com",
    assetCount: 0,
    createdBy: "planner@example.com",
    createdAt: "2026-07-01T00:00:00.000Z",
  };
}

function createDetail(
  id: number,
  title: string,
  overrides: Partial<RingfenceDetail["ringfence"]> = {},
): RingfenceDetail {
  return {
    ringfence: {
      id,
      title,
      fromDate: "2026-08-01T00:00:00.000Z",
      toDate: "2026-08-31T00:00:00.000Z",
      divisions: "01",
      warehouse: "GLA",
      owner: "planner@example.com",
      createdBy: "planner@example.com",
      createdAt: "2026-07-01T00:00:00.000Z",
      ...overrides,
    },
    items: [],
    assets: [],
  };
}

function createBatchResult(overrides: Partial<RingfenceItemBatchResult> = {}): RingfenceItemBatchResult {
  return {
    readyAssetIds: [],
    alreadyAssignedAssetIds: [],
    unavailableAssetIds: [],
    addedAssetIds: ["ASSET-A", "ASSET-B"],
    overlaps: [],
    requiresOverlapAcknowledgement: false,
    ...overrides,
  };
}

function createOverlap(): RingfenceOverlap {
  return {
    ringfenceId: 12,
    assetIds: ["ASSET-A"],
    title: "Existing reserve",
    fromDate: "2026-08-10T00:00:00.000Z",
    toDate: "2026-08-20T00:00:00.000Z",
    owner: "other@example.com",
  };
}

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
}

function renderRingfencePage(initialEntry = "/ringfence?ringfenceId=7") {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <RingfencePage />
      <LocationProbe />
    </MemoryRouter>,
  );
}

async function openDirectAssetEntry() {
  await screen.findByRole("heading", { name: "Priority hire" });
  const addButtons = screen.getAllByRole("button", { name: "Add assets" });
  await userEvent.setup().click(addButtons[0]);
  return screen.getByLabelText("Asset IDs");
}

describe("RingfencePage", () => {
  beforeAll(() => {
    Object.defineProperty(HTMLDialogElement.prototype, "showModal", {
      configurable: true,
      value: function showModal(this: HTMLDialogElement) {
        this.setAttribute("open", "");
      },
    });
    Object.defineProperty(HTMLDialogElement.prototype, "close", {
      configurable: true,
      value: function close(this: HTMLDialogElement) {
        this.removeAttribute("open");
      },
    });
  });

  beforeEach(async () => {
    vi.clearAllMocks();
    sessionStorage.clear();
    authMock.value.user.division = "01";
    authMock.value.user.isReadOnly = false;
    await i18n.changeLanguage("en");

    mockedFetchDivisions.mockResolvedValue([{ code: "01", name: "United Kingdom" }]);
    mockedFetchUsers.mockResolvedValue([]);
    mockedFetchWarehouses.mockResolvedValue([]);
    mockedFetchRingfences.mockResolvedValue([createRingfence(7, "Priority hire")]);
    mockedFetchRingfence.mockImplementation(async (id) =>
      createDetail(id, id === 8 ? "Service reserve" : "Priority hire"),
    );
    mockedPreflightRingfenceItems.mockResolvedValue(createBatchResult());
    mockedAddRingfenceItems.mockResolvedValue(createBatchResult());
    mockedUpdateRingfence.mockResolvedValue(createDetail(7, "Priority hire").ringfence);
  });

  afterEach(cleanup);

  afterAll(() => {
    if (originalShowModal) Object.defineProperty(HTMLDialogElement.prototype, "showModal", originalShowModal);
    else delete (HTMLDialogElement.prototype as Partial<HTMLDialogElement>).showModal;
    if (originalClose) Object.defineProperty(HTMLDialogElement.prototype, "close", originalClose);
    else delete (HTMLDialogElement.prototype as Partial<HTMLDialogElement>).close;
  });

  it("keeps a Ringfence selected from the URL instead of resetting to the first record", async () => {
    mockedFetchRingfences.mockResolvedValue([
      createRingfence(7, "Priority hire"),
      createRingfence(8, "Service reserve"),
    ]);

    renderRingfencePage("/ringfence?ringfenceId=8");

    await screen.findByRole("heading", { name: "Service reserve" });
    expect(screen.getByTestId("location")).toHaveTextContent("/ringfence?ringfenceId=8");
    await waitFor(() => expect(mockedFetchRingfence).toHaveBeenCalledWith(8));
    expect(mockedFetchRingfences).toHaveBeenCalledTimes(1);
    expect(mockedFetchRingfence).not.toHaveBeenCalledWith(7);
  });

  it("restores filters for the Ringfence list and selected-assets grid after remount", async () => {
    mockedFetchRingfence.mockResolvedValue({
      ...createDetail(7, "Priority hire"),
      assets: [
        {
          id: "ASSET-1",
          division: "01",
          warehouse: "GLA",
          description: "Generator",
          itemNumber: "ITEM-1",
          status: "Available",
          warehouseLocation: "Yard",
        },
      ],
    });
    const firstRender = renderRingfencePage();
    await screen.findByRole("heading", { name: "Assets (1)" });

    const user = userEvent.setup();
    await user.type(screen.getByRole("searchbox", { name: "Search ringfences" }), "priority");
    await user.type(screen.getByRole("searchbox", { name: "Search protected assets" }), "asset-1");
    firstRender.unmount();

    renderRingfencePage();
    await screen.findByRole("heading", { name: "Assets (1)" });

    expect(screen.getByRole("searchbox", { name: "Search ringfences" })).toHaveValue("priority");
    expect(screen.getByRole("searchbox", { name: "Search protected assets" })).toHaveValue("asset-1");
  });

  it("shows ringfence details without create, edit, delete, add, or remove actions for read-only users", async () => {
    authMock.value.user.isReadOnly = true;
    mockedFetchRingfence.mockResolvedValue(createDetail(7, "Priority hire", { title: "Priority hire" }));

    renderRingfencePage();

    await screen.findByRole("heading", { name: "Priority hire" });
    expect(screen.queryByRole("button", { name: /create/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /edit details/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /add assets/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /delete/i })).not.toBeInTheDocument();
  });

  it("separates division and warehouse into their own master-grid columns", async () => {
    renderRingfencePage();

    await screen.findByRole("heading", { name: "Priority hire" });

    expect(screen.getByRole("columnheader", { name: /^Division/ })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /^Warehouse/ })).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: "Context" })).not.toBeInTheDocument();
    expect(screen.getAllByRole("cell").map((cell) => cell.textContent)).toContain("GLA");
  });

  it("defaults a new Ringfence owner to the signed-in user and only offers suffix-0 warehouses in create mode", async () => {
    const user = userEvent.setup();
    mockedFetchDivisions.mockResolvedValue([
      { code: "01", name: "United Kingdom" },
      { code: "02", name: "Ireland" },
    ]);
    mockedFetchUsers.mockResolvedValue([{ loginName: "planner@example.com", fullName: "Planner" }]);
    mockedFetchWarehouses.mockResolvedValue([
      {
        warehouseCode: "UK0",
        warehouse: "United Kingdom depot",
        facility: "UKC",
        divisionCode: "01",
        divisionName: "United Kingdom",
      },
      {
        warehouseCode: "UK1",
        warehouse: "United Kingdom repair",
        facility: "UKC",
        divisionCode: "01",
        divisionName: "United Kingdom",
      },
      {
        warehouseCode: "IE0",
        warehouse: "Ireland depot",
        facility: "IEC",
        divisionCode: "02",
        divisionName: "Ireland",
      },
    ]);

    renderRingfencePage();
    await screen.findByRole("heading", { name: "Priority hire" });
    await user.click(screen.getAllByRole("button", { name: "Create Ringfence" })[0]);

    expect(screen.getByLabelText("Owner")).toHaveValue("planner@example.com");

    await user.selectOptions(screen.getByLabelText("Divisions"), "01");
    await waitFor(() => {
      expect(mockedFetchWarehouses).toHaveBeenLastCalledWith("01");
    });

    const warehouse = screen.getByLabelText("Warehouse");
    await waitFor(() => expect(warehouse).toHaveTextContent("UK0"));
    expect(warehouse).not.toHaveTextContent("UK1");
    expect(warehouse).not.toHaveTextContent("IE0");
  });

  it("explains when no suffix-0 warehouse is configured and prevents create", async () => {
    const user = userEvent.setup();
    mockedFetchUsers.mockResolvedValue([{ loginName: "planner@example.com", fullName: "Planner" }]);
    mockedFetchWarehouses.mockResolvedValue([
      {
        warehouseCode: "UK1",
        warehouse: "United Kingdom repair",
        facility: "UKC",
        divisionCode: "01",
        divisionName: "United Kingdom",
      },
    ]);

    renderRingfencePage();
    await screen.findByRole("heading", { name: "Priority hire" });
    await user.click(screen.getAllByRole("button", { name: "Create Ringfence" })[0]);
    await user.selectOptions(screen.getByLabelText("Divisions"), "01");

    expect(
      await screen.findByText(/no warehouse code ending in 0 is configured for the selected divisions/i),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
    expect(screen.getByLabelText("Warehouse")).toBeDisabled();
  });

  it("clears a warehouse that no longer matches the selected division and groups multi-division choices", async () => {
    const user = userEvent.setup();
    authMock.value.user.division = "01,02";
    mockedFetchDivisions.mockResolvedValue([
      { code: "01", name: "United Kingdom" },
      { code: "02", name: "Ireland" },
    ]);
    mockedFetchUsers.mockResolvedValue([{ loginName: "planner@example.com", fullName: "Planner" }]);
    mockedFetchWarehouses.mockImplementation(async (division) => {
      const allWarehouses = [
        {
          warehouseCode: "UK0",
          warehouse: "United Kingdom depot",
          facility: "UKC",
          divisionCode: "01",
          divisionName: "United Kingdom",
        },
        {
          warehouseCode: "IE0",
          warehouse: "Ireland depot",
          facility: "IEC",
          divisionCode: "02",
          divisionName: "Ireland",
        },
      ];
      return division === "01" ? allWarehouses : allWarehouses.reverse();
    });

    renderRingfencePage();
    await screen.findByRole("heading", { name: "Priority hire" });
    await user.click(screen.getAllByRole("button", { name: "Create Ringfence" })[0]);

    const divisions = screen.getByLabelText("Divisions");
    const warehouse = screen.getByLabelText("Warehouse");
    await user.selectOptions(divisions, "01");
    await waitFor(() => expect(warehouse).toHaveTextContent("UK0"));
    await user.selectOptions(warehouse, "UK0");
    expect(warehouse).toHaveValue("UK0");

    await user.selectOptions(divisions, "02");
    await user.deselectOptions(divisions, "01");
    expect(warehouse).toHaveValue("");
    await waitFor(() => {
      expect(mockedFetchWarehouses).toHaveBeenLastCalledWith("02");
    });
    expect(warehouse).not.toHaveTextContent("UK0");

    await user.selectOptions(divisions, ["01", "02"]);
    await waitFor(() => {
      expect(mockedFetchWarehouses).toHaveBeenLastCalledWith("01,02");
    });
    await waitFor(() => expect(warehouse.querySelectorAll("optgroup")).toHaveLength(2));
    expect(Array.from(warehouse.querySelectorAll("optgroup"), (group) => group.label)).toEqual(
      expect.arrayContaining([expect.stringContaining("01"), expect.stringContaining("02")]),
    );
  });

  it("keeps unavailable legacy owner and warehouse values when only record details change", async () => {
    const user = userEvent.setup();
    mockedFetchRingfence.mockResolvedValue(
      createDetail(7, "Priority hire", {
        owner: "legacy.owner@example.com",
        warehouse: "OLD1",
      }),
    );
    mockedUpdateRingfence.mockResolvedValue(
      createDetail(7, "Updated priority hire", {
        owner: "legacy.owner@example.com",
        warehouse: "OLD1",
      }).ringfence,
    );

    renderRingfencePage();
    await screen.findByRole("heading", { name: "Priority hire" });
    await user.click(screen.getByRole("button", { name: "Edit details" }));

    await waitFor(() => expect(screen.getByLabelText("Owner")).toHaveValue("legacy.owner@example.com"));
    expect(screen.getByLabelText("Warehouse")).toHaveValue("OLD1");

    const name = screen.getByLabelText("Ringfence name");
    await user.clear(name);
    await user.type(name, "Updated priority hire");
    await user.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => {
      expect(mockedUpdateRingfence).toHaveBeenCalledWith(
        7,
        expect.objectContaining({
          title: "Updated priority hire",
          divisions: "01",
          owner: "legacy.owner@example.com",
          warehouse: "OLD1",
        }),
      );
    });
  });

  it("normalizes a catalogue warehouse match without presenting it as a legacy value", async () => {
    const user = userEvent.setup();
    mockedFetchRingfence.mockResolvedValue(createDetail(7, "Priority hire", { warehouse: "ed0" }));
    mockedFetchWarehouses.mockResolvedValue([
      {
        warehouseCode: "ED0",
        warehouse: "Edinburgh depot",
        facility: "EDC",
        divisionCode: "01",
        divisionName: "United Kingdom",
      },
    ]);

    renderRingfencePage();
    await screen.findByRole("heading", { name: "Priority hire" });
    await user.click(screen.getByRole("button", { name: "Edit details" }));

    await waitFor(() => expect(screen.getByLabelText("Warehouse")).toHaveValue("ED0"));
    expect(screen.getByLabelText("Warehouse")).not.toHaveTextContent("Current legacy warehouse");
  });

  it("revalidates legacy owner and warehouse values after the division scope changes", async () => {
    const user = userEvent.setup();
    authMock.value.user.division = "01,02";
    mockedFetchDivisions.mockResolvedValue([
      { code: "01", name: "United Kingdom" },
      { code: "02", name: "Ireland" },
    ]);
    mockedFetchRingfence.mockResolvedValue(
      createDetail(7, "Priority hire", {
        owner: "legacy.owner@example.com",
        warehouse: "OLD1",
      }),
    );

    renderRingfencePage();
    await screen.findByRole("heading", { name: "Priority hire" });
    await user.click(screen.getByRole("button", { name: "Edit details" }));
    await waitFor(() => expect(screen.getByLabelText("Owner")).toHaveValue("legacy.owner@example.com"));

    const divisions = screen.getByLabelText("Divisions");
    await user.selectOptions(divisions, "02");
    await user.deselectOptions(divisions, "01");

    expect(screen.getByLabelText("Owner")).toHaveValue("");
    expect(screen.getByLabelText("Warehouse")).toHaveValue("");
  });

  it("keeps a shared division scope visible and locked for a partial collaborator", async () => {
    const user = userEvent.setup();
    mockedFetchDivisions.mockResolvedValue([
      { code: "01", name: "United Kingdom" },
      { code: "02", name: "Ireland" },
    ]);
    mockedFetchRingfence.mockResolvedValue(createDetail(7, "Priority hire", { divisions: "01,02" }));
    mockedUpdateRingfence.mockResolvedValue(createDetail(7, "Updated priority hire", { divisions: "01,02" }).ringfence);

    renderRingfencePage();
    await screen.findByRole("heading", { name: "Priority hire" });
    await user.click(screen.getByRole("button", { name: "Edit details" }));

    expect(screen.getByRole("textbox", { name: "Divisions" })).toHaveTextContent("01, 02");
    expect(screen.queryByRole("listbox", { name: "Divisions" })).not.toBeInTheDocument();

    const name = screen.getByLabelText("Ringfence name");
    await user.clear(name);
    await user.type(name, "Updated priority hire");
    await user.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => {
      expect(mockedUpdateRingfence).toHaveBeenCalledWith(
        7,
        expect.objectContaining({ divisions: "01,02", title: "Updated priority hire" }),
      );
    });
  });

  it("preflights direct pasted asset IDs, deduplicates them, then applies one batch", async () => {
    const user = userEvent.setup();
    mockedPreflightRingfenceItems.mockResolvedValue(createBatchResult({ readyAssetIds: ["ASSET-A", "ASSET-B"] }));
    renderRingfencePage();
    const input = await openDirectAssetEntry();
    await user.type(input, "asset-a, ASSET-B\nasset-a");

    await user.click(screen.getByRole("button", { name: "Check and add assets" }));

    await waitFor(() => {
      expect(mockedPreflightRingfenceItems).toHaveBeenCalledWith(7, ["ASSET-A", "ASSET-B"]);
    });
    await waitFor(() => {
      expect(mockedAddRingfenceItems).toHaveBeenCalledWith(7, ["ASSET-A", "ASSET-B"], false);
    });
  });

  it("requires an explicit confirmation before applying overlapping direct assignments", async () => {
    const user = userEvent.setup();
    mockedPreflightRingfenceItems.mockResolvedValue(
      createBatchResult({
        readyAssetIds: ["ASSET-A"],
        addedAssetIds: [],
        overlaps: [createOverlap()],
        requiresOverlapAcknowledgement: true,
      }),
    );

    renderRingfencePage();
    const input = await openDirectAssetEntry();
    await user.type(input, "asset-a");
    await user.click(screen.getByRole("button", { name: "Check and add assets" }));

    await screen.findByRole("heading", { name: "Review overlapping assignments" });
    expect(mockedAddRingfenceItems).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Add despite overlap" }));

    await waitFor(() => {
      expect(mockedAddRingfenceItems).toHaveBeenCalledWith(7, ["ASSET-A"], true);
    });
  });
});
