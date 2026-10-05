import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import { AuthContext } from "../../contexts/auth";
import {
  createEquipmentLine,
  deleteReservation,
  fetchAgreementDetail,
  fetchEquipmentCatalog,
  fetchEquipmentGenericOptions,
  fetchReservationsForHeader,
  type AgreementDetail,
} from "../../services/agreementsService";
import { unfulfilAgreement } from "../../services/agreementResetService";
import { agreementLineDeletionService } from "../../services/agreementLineDeletionService";
import { fetchAppConfiguration } from "../../services/appConfigurationService";
import { activateAgreement } from "../../services/activationService";
import { bulkDepotFulfil, bulkRehire } from "../../services/bulkActionsService";
import { ActivationStatus, ApiFulfilmentStatus, type Reservation } from "../../types";
import { AgreementDetailPage } from "./AgreementDetailPage";

vi.mock("../../services/agreementsService", () => ({
  createEquipmentLine: vi.fn(),
  createReservation: vi.fn(),
  deleteReservation: vi.fn(),
  fetchAgreementDetail: vi.fn(),
  fetchEquipmentCatalog: vi.fn(),
  fetchEquipmentGenericOptions: vi.fn(),
  fetchReservationsForHeader: vi.fn(),
}));

vi.mock("../../services/agreementResetService", () => ({ unfulfilAgreement: vi.fn() }));
vi.mock("../../services/agreementLineDeletionService", () => ({
  agreementLineDeletionService: { deleteLine: vi.fn() },
}));
vi.mock("../../services/appConfigurationService", () => ({ fetchAppConfiguration: vi.fn() }));
vi.mock("../../services/activationService", () => ({ activateAgreement: vi.fn() }));

beforeEach(() => {
  vi.mocked(fetchAppConfiguration).mockImplementation(async () => ({
    environmentLabel: "Test",
    showPreviewBanner: false,
    legacyFrontendUrl: null,
  }));
});

vi.mock("../../services/bulkActionsService", () => ({
  bulkDepotFulfil: vi.fn(),
  bulkRehire: vi.fn(),
}));

vi.mock("../../components/fulfilment/AvailabilityPanel", () => ({
  AvailabilityPanel: ({
    genericCode,
    warehouse,
    division,
    startDate,
    endDate,
  }: {
    genericCode: string;
    warehouse?: string;
    division?: string;
    startDate?: string | null;
    endDate?: string | null;
  }) => (
    <div
      data-testid="availability-panel"
      data-generic-code={genericCode}
      data-warehouse={warehouse}
      data-division={division}
      data-start-date={startDate}
      data-end-date={endDate}
    >
      Availability results for {genericCode}
    </div>
  ),
}));

vi.mock("../../components/notes/RecordNotesEditor", () => ({
  RecordNotesEditor: () => <section aria-label="Notes" />,
}));

const mockedFetchAgreementDetail = vi.mocked(fetchAgreementDetail);
const mockedFetchReservationsForHeader = vi.mocked(fetchReservationsForHeader);
const mockedFetchEquipmentCatalog = vi.mocked(fetchEquipmentCatalog);
const mockedFetchEquipmentGenericOptions = vi.mocked(fetchEquipmentGenericOptions);
const mockedCreateEquipmentLine = vi.mocked(createEquipmentLine);
const mockedDeleteReservation = vi.mocked(deleteReservation);
const mockedBulkDepotFulfil = vi.mocked(bulkDepotFulfil);
const mockedBulkRehire = vi.mocked(bulkRehire);

function createDetail(fulfilmentStatus: ApiFulfilmentStatus): AgreementDetail {
  return {
    header: {
      id: 42,
      quotePublicId: null,
      agreementNumber: "A-10042",
      orderNumber: null,
      quoteNumber: null,
      customerName: "Northwind",
      customerNumber: "C-42",
      division: "01",
      facility: "GLA",
      fulfilmentStatus: ApiFulfilmentStatus.FullyFulfilled,
      activationStatus: ActivationStatus.TODO,
      onHireDate: "2026-08-01",
      offHireDate: "2026-08-31",
      changeSequence: 1,
      isDeleted: false,
      lastUpdatedBy: null,
      lastUpdatedDate: null,
      orderSource: "D365",
      opportunityNumber: null,
      opportunityName: null,
    },
    lines: [
      {
        id: 7,
        isSubline: false,
        headerId: 42,
        itemNumber: "ITEM-STATUS",
        genericItemNumber: null,
        quantity: 2,
        deliveryDate: null,
        validFromDate: "2026-08-01",
        validToDate: "2026-08-31",
        terminationDate: null,
        attributes: null,
        fulfilmentStatus,
        activationStatus: ActivationStatus.TODO,
        requiresFulfilment: true,
        isDeleted: false,
        changeSequence: 1,
        warehouse: "GLA",
        division: "01",
        facility: "GLA",
        orderSource: "D365",
        orderLineNumber: "10",
        agreementLineNumber: "1",
        quantityFulfilled: 0,
        lastUpdatedBy: null,
        lastUpdatedDate: null,
      },
    ],
  };
}

function createFullyCoveringReservation(): Reservation {
  return {
    id: 31,
    assetId: "ASSET-31",
    lineId: 7,
    itemNumber: "ITEM-STATUS",
    quantity: 2,
    effectiveQuantity: 2,
    warehouse: "GLA",
    isConfirmed: true,
    isDepotFulfilled: false,
    isRehire: false,
    actualAssetId: null,
    actualItemNumber: null,
    actualQuantity: null,
    notes: null,
    lastUpdatedBy: null,
    lastUpdatedDate: null,
  };
}

function createWorkbenchDetail(): AgreementDetail {
  const detail = createDetail(ApiFulfilmentStatus.PartiallyFulfilled);
  detail.lines[0] = {
    ...detail.lines[0],
    genericItemNumber: "GEN-ONE",
    quantity: 4,
    quantityFulfilled: 1,
  };
  detail.lines.push({
    ...detail.lines[0],
    id: 8,
    itemNumber: "ITEM-SECOND",
    genericItemNumber: "GEN-TWO",
    quantity: 8,
    quantityFulfilled: 3,
    agreementLineNumber: "2",
    warehouse: "MAN",
    division: "02",
    validFromDate: "2026-09-01",
    validToDate: "2026-09-30",
  });
  return detail;
}

function fireHorizontalPointerEvent(
  element: Element,
  type: "pointerdown" | "pointermove" | "pointerup",
  values: { button?: number; clientX: number; pointerId: number },
): void {
  const event = new Event(type, { bubbles: true, cancelable: true });
  Object.defineProperties(event, {
    button: { value: values.button ?? 0 },
    clientX: { value: values.clientX },
    pointerId: { value: values.pointerId },
  });
  fireEvent(element, event);
}

async function renderLineStatus(fulfilmentStatus: ApiFulfilmentStatus, reservations: Reservation[]) {
  mockedFetchAgreementDetail.mockResolvedValue(createDetail(fulfilmentStatus));
  mockedFetchReservationsForHeader.mockResolvedValue(reservations);

  render(
    <MemoryRouter initialEntries={["/agreements/42"]}>
      <Routes>
        <Route path="/agreements/:headerId" element={<AgreementDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );

  const itemCell = await screen.findByText("ITEM-STATUS");
  const lineRow = itemCell.closest("tr");
  expect(lineRow).not.toBeNull();
  return within(lineRow!);
}

function renderReadOnlyDetail() {
  return render(
    <AuthContext
      value={{
        user: {
          loginName: "auditor@example.com",
          displayName: "Audit User",
          division: "01",
          isAdmin: false,
          isSuperAdmin: false,
          isReadOnly: true,
          language: "en",
        },
        isLoading: false,
        error: null,
      }}
    >
      <MemoryRouter initialEntries={["/agreements/42"]}>
        <Routes>
          <Route path="/agreements/:headerId" element={<AgreementDetailPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("AgreementDetailPage line fulfilment", () => {
  beforeEach(async () => {
    vi.resetAllMocks();
    vi.mocked(fetchAppConfiguration).mockResolvedValue({
      environmentLabel: "Test",
      showPreviewBanner: false,
      legacyFrontendUrl: null,
    });
    await i18n.changeLanguage("en");
  });

  afterEach(cleanup);

  it("keeps agreement availability visible without fulfilment mutations for read-only users", async () => {
    mockedFetchAgreementDetail.mockResolvedValue(createWorkbenchDetail());
    mockedFetchReservationsForHeader.mockResolvedValue([]);

    renderReadOnlyDetail();

    await screen.findByText("ITEM-STATUS");
    expect(screen.getByRole("button", { name: "View availability for agreement line 1" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Activate" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reserve" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Depot fulfil" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Rehire" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Add equipment/ })).not.toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Notes" })).toBeInTheDocument();
  });

  it.each([
    ["without reservations", []],
    ["with reservations covering the full line quantity", [createFullyCoveringReservation()]],
  ] as const)("keeps raw status 2 neutral %s", async (_scenario, reservations) => {
    const line = await renderLineStatus(ApiFulfilmentStatus.Overfulfilled, [...reservations]);

    expect(line.getByText("Unknown")).toBeInTheDocument();
    expect(line.queryByText("Unfulfilled")).not.toBeInTheDocument();
    expect(line.queryByText("Partially Fulfilled")).not.toBeInTheDocument();
    expect(line.queryByText("Fully Fulfilled")).not.toBeInTheDocument();
  });

  it.each([
    [ApiFulfilmentStatus.Unfulfilled, "Unfulfilled"],
    [ApiFulfilmentStatus.PartiallyFulfilled, "Partially Fulfilled"],
    [ApiFulfilmentStatus.FullyFulfilled, "Fully Fulfilled"],
  ] as const)("renders authoritative raw status %s as %s", async (status, expectedLabel) => {
    const line = await renderLineStatus(status, [createFullyCoveringReservation()]);

    expect(line.getByText(expectedLabel)).toBeInTheDocument();
  });

  it("displays agreement lines in their numeric line-number order", async () => {
    const detail = createDetail(ApiFulfilmentStatus.Unfulfilled);
    detail.lines = [
      { ...detail.lines[0], id: 29, agreementLineNumber: "A711618-29" },
      { ...detail.lines[0], id: 1, agreementLineNumber: "A711618-1" },
      { ...detail.lines[0], id: 20, agreementLineNumber: "A711618-20" },
      { ...detail.lines[0], id: 4, agreementLineNumber: "A711618-4" },
    ];
    mockedFetchAgreementDetail.mockResolvedValue(detail);
    mockedFetchReservationsForHeader.mockResolvedValue([]);

    render(
      <MemoryRouter initialEntries={["/agreements/42"]}>
        <Routes>
          <Route path="/agreements/:headerId" element={<AgreementDetailPage />} />
        </Routes>
      </MemoryRouter>,
    );

    await screen.findByRole("button", { name: "View availability for agreement line A711618-1" });
    expect(
      screen
        .getAllByRole("button", { name: /View availability for agreement line A711618-/ })
        .map((button) => button.textContent),
    ).toEqual(["A711618-1", "A711618-4", "A711618-20", "A711618-29"]);
  });
});

describe("AgreementDetailPage reservations column", () => {
  beforeEach(async () => {
    vi.resetAllMocks();
    vi.mocked(fetchAppConfiguration).mockResolvedValue({
      environmentLabel: "Test",
      showPreviewBanner: false,
      legacyFrontendUrl: null,
    });
    await i18n.changeLanguage("en");
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  function createReservationDetail() {
    const detail = createDetail(ApiFulfilmentStatus.PartiallyFulfilled);
    detail.lines.push({
      ...detail.lines[0],
      id: 8,
      itemNumber: "ITEM-SECOND",
      agreementLineNumber: "2",
      warehouse: "MAN",
    });
    return detail;
  }

  function createReservations(): Reservation[] {
    return [
      createFullyCoveringReservation(),
      {
        ...createFullyCoveringReservation(),
        id: 32,
        assetId: "ASSET-32",
        quantity: 1,
        effectiveQuantity: 0.5,
        warehouse: "MAN",
      },
      {
        ...createFullyCoveringReservation(),
        id: 33,
        assetId: "ASSET-33",
        quantity: 1,
        effectiveQuantity: 1,
        warehouse: "BHM",
      },
    ];
  }

  function renderReservationDetail(reservationData: Reservation[] = createReservations()) {
    mockedFetchAgreementDetail.mockResolvedValue(createReservationDetail());
    mockedFetchReservationsForHeader.mockResolvedValue(reservationData);

    render(
      <MemoryRouter initialEntries={["/agreements/42"]}>
        <Routes>
          <Route path="/agreements/:headerId" element={<AgreementDetailPage />} />
        </Routes>
      </MemoryRouter>,
    );
  }

  it("keeps each line compact and discloses additional reservations without changing row selection", async () => {
    const user = userEvent.setup();
    renderReservationDetail();

    expect(await screen.findByRole("columnheader", { name: "Reservations" })).toBeInTheDocument();
    const firstLineRow = screen.getByText("ITEM-STATUS").closest("tr");
    const secondLineRow = screen.getByText("ITEM-SECOND").closest("tr");
    expect(firstLineRow).not.toBeNull();
    expect(secondLineRow).not.toBeNull();

    const firstLine = within(firstLineRow!);
    const reservationList = firstLine.getByRole("list", { name: "Reservations for agreement line 1" });
    expect(firstLine.getByText("ASSET-31")).toBeVisible();
    expect(firstLine.getByText("ASSET-32")).toBeVisible();
    expect(firstLine.queryByText("ASSET-33")).not.toBeInTheDocument();
    expect(firstLine.queryByText("GLA · Qty 2")).not.toBeInTheDocument();
    expect(firstLine.queryByRole("button", { name: /^Remove reservation / })).not.toBeInTheDocument();

    const showMore = firstLine.getByRole("button", {
      name: "Manage reservations for agreement line 1",
    });
    expect(showMore).toHaveAttribute("aria-expanded", "false");
    expect(showMore).toHaveAttribute("aria-controls", reservationList.id);

    const secondLine = within(secondLineRow!);
    expect(secondLine.getByText("No reservations")).toBeInTheDocument();
    expect(secondLine.queryByText("ASSET-31")).not.toBeInTheDocument();
    expect(screen.getAllByRole("row")).toHaveLength(3);

    await user.click(showMore);

    expect(showMore).toHaveAttribute("aria-expanded", "true");
    expect(reservationList).toHaveAttribute("tabindex", "0");
    expect(firstLine.getByText("ASSET-33")).toBeVisible();
    expect(firstLine.getByText("GLA · Qty 2")).toBeVisible();
    expect(firstLine.getByRole("button", { name: "Remove reservation ASSET-33" })).toBeVisible();
    expect(firstLine.queryByRole("button", { name: "Reserve" })).not.toBeInTheDocument();

    const showFewer = firstLine.getByRole("button", {
      name: "Show fewer reservations for agreement line 1",
    });
    await user.click(showFewer);

    expect(firstLine.queryByText("ASSET-33")).not.toBeInTheDocument();
    expect(firstLine.queryByRole("button", { name: /^Remove reservation / })).not.toBeInTheDocument();
    expect(firstLine.getByRole("button", { name: "Reserve" })).toBeVisible();
    expect(screen.getAllByRole("row")).toHaveLength(3);
  });

  it("bounds a line with 100 reservation records behind one disclosure", async () => {
    const user = userEvent.setup();
    const manyReservations = Array.from({ length: 100 }, (_, index) => ({
      ...createFullyCoveringReservation(),
      id: 1000 + index,
      assetId: `ASSET-${index + 1}`,
      quantity: 1,
      effectiveQuantity: 1,
    }));
    renderReservationDetail(manyReservations);

    const firstLineRow = (await screen.findByText("ITEM-STATUS")).closest("tr");
    expect(firstLineRow).not.toBeNull();
    const firstLine = within(firstLineRow!);
    const reservationList = firstLine.getByRole("list", { name: "Reservations for agreement line 1" });

    expect(reservationList.querySelectorAll("li")).toHaveLength(2);
    expect(firstLine.getByText("ASSET-1")).toBeVisible();
    expect(firstLine.getByText("ASSET-2")).toBeVisible();
    expect(firstLine.queryByText("ASSET-100")).not.toBeInTheDocument();
    expect(firstLine.queryByRole("button", { name: /^Remove reservation / })).not.toBeInTheDocument();

    const showMore = firstLine.getByRole("button", {
      name: "Manage reservations for agreement line 1",
    });
    expect(showMore).toHaveAttribute("aria-expanded", "false");

    await user.click(showMore);

    expect(showMore).toHaveAttribute("aria-expanded", "true");
    expect(reservationList.querySelectorAll("li")).toHaveLength(100);
    expect(firstLine.getByText("ASSET-100")).toBeVisible();
    expect(firstLine.getByRole("button", { name: "Remove reservation ASSET-100" })).toBeVisible();
  });

  it("consolidates quantity-managed stock by item and warehouse with a quantity badge", async () => {
    const stockReservation = {
      ...createFullyCoveringReservation(),
      id: 41,
      assetId: "CB1201STA005M",
      itemNumber: "CB1201STA005M",
      quantity: 1,
      effectiveQuantity: 1,
      warehouse: "HE0",
    };
    renderReservationDetail([
      stockReservation,
      { ...stockReservation, id: 42 },
      {
        ...stockReservation,
        id: 43,
        assetId: "CB1201STA010M",
        itemNumber: "CB1201STA010M",
      },
    ]);

    const firstLineRow = (await screen.findByText("ITEM-STATUS")).closest("tr");
    expect(firstLineRow).not.toBeNull();
    const firstLine = within(firstLineRow!);
    const reservationList = firstLine.getByRole("list", { name: "Reservations for agreement line 1" });

    expect(reservationList.querySelectorAll("li")).toHaveLength(2);
    expect(firstLine.getAllByText("HE0: CB1201STA005M")).toHaveLength(1);
    expect(firstLine.getByLabelText("Quantity 2")).toHaveTextContent("2");
    expect(firstLine.getByLabelText("Quantity 1")).toHaveTextContent("1");
  });

  it("provides management access when a line has only one reservation", async () => {
    const user = userEvent.setup();
    renderReservationDetail([createFullyCoveringReservation()]);

    const firstLineRow = (await screen.findByText("ITEM-STATUS")).closest("tr");
    expect(firstLineRow).not.toBeNull();
    const firstLine = within(firstLineRow!);
    expect(firstLine.queryByRole("button", { name: "Remove reservation ASSET-31" })).not.toBeInTheDocument();

    await user.click(firstLine.getByRole("button", { name: "Manage reservations for agreement line 1" }));

    expect(firstLine.getByRole("button", { name: "Remove reservation ASSET-31" })).toBeVisible();
    expect(firstLine.queryByRole("button", { name: "Reserve" })).not.toBeInTheDocument();
  });

  it("opens the asset selector from a line reservation action", async () => {
    const user = userEvent.setup();
    renderReservationDetail([]);

    const secondLineRow = (await screen.findByText("ITEM-SECOND")).closest("tr");
    expect(secondLineRow).not.toBeNull();
    await user.click(within(secondLineRow!).getByRole("button", { name: "Reserve" }));

    expect(await screen.findByRole("dialog", { name: "Select Asset" })).toBeInTheDocument();
  });

  it("keeps a cancelled reservation and removes only the confirmed reservation before reloading the row", async () => {
    const user = userEvent.setup();
    const reservationData = createReservations();
    mockedDeleteReservation.mockResolvedValue();
    const confirm = vi.spyOn(window, "confirm").mockReturnValueOnce(false).mockReturnValueOnce(true);

    renderReservationDetail(reservationData);

    const firstLineRow = (await screen.findByText("ITEM-STATUS")).closest("tr");
    expect(firstLineRow).not.toBeNull();
    const firstLine = within(firstLineRow!);
    await user.click(
      firstLine.getByRole("button", {
        name: "Manage reservations for agreement line 1",
      }),
    );
    const remove = within(firstLineRow!).getByRole("button", {
      name: "Remove reservation ASSET-33",
    });

    await user.click(remove);
    expect(mockedDeleteReservation).not.toHaveBeenCalled();

    mockedFetchReservationsForHeader.mockResolvedValueOnce(reservationData.slice(0, 2));
    await user.click(remove);

    expect(confirm).toHaveBeenLastCalledWith('Remove reservation "ASSET-33"? This action cannot be undone.');
    await waitFor(() => expect(mockedDeleteReservation).toHaveBeenCalledWith(33));
    await waitFor(() => expect(mockedFetchAgreementDetail).toHaveBeenCalledTimes(2));
    const updatedLineRow = screen.getByText("ITEM-STATUS").closest("tr");
    const updatedLine = within(updatedLineRow!);
    expect(updatedLine.getByText("ASSET-31")).toBeVisible();
    expect(updatedLine.getByText("ASSET-32")).toBeVisible();
    expect(updatedLine.queryByText("ASSET-33")).not.toBeInTheDocument();
    expect(
      updatedLine.queryByRole("button", { name: "Show 1 more reservation for agreement line 1" }),
    ).not.toBeInTheDocument();
  });
});

describe("AgreementDetailPage availability workbench", () => {
  beforeEach(async () => {
    vi.resetAllMocks();
    vi.mocked(fetchAppConfiguration).mockResolvedValue({
      environmentLabel: "Test",
      showPreviewBanner: false,
      legacyFrontendUrl: null,
    });
    window.localStorage.clear();
    await i18n.changeLanguage("en");
    mockedFetchAgreementDetail.mockResolvedValue(createWorkbenchDetail());
    mockedFetchReservationsForHeader.mockResolvedValue([]);
  });

  afterEach(cleanup);

  function renderWorkbench() {
    render(
      <MemoryRouter initialEntries={["/agreements/42"]}>
        <Routes>
          <Route path="/agreements/:headerId" element={<AgreementDetailPage />} />
        </Routes>
      </MemoryRouter>,
    );
  }

  it("keeps selected-line context and availability in a labelled workbench", async () => {
    const user = userEvent.setup();
    renderWorkbench();

    expect(await screen.findByRole("region", { name: "Lines (2)" })).toBeInTheDocument();
    const firstLine = screen.getByRole("button", { name: "View availability for agreement line 1" });
    firstLine.focus();
    await user.keyboard("{Enter}");

    const firstInspector = screen.getByRole("region", { name: "Availability — 1 · GEN-ONE" });
    await waitFor(() =>
      expect(within(firstInspector).getByRole("heading", { name: "Availability — 1 · GEN-ONE" })).toHaveFocus(),
    );
    const lineQueue = screen.getByRole("list", { name: "Agreement line queue" });
    const firstQueueLine = within(lineQueue).getByRole("button", {
      name: "View availability for agreement line 1, item ITEM-STATUS",
    });
    expect(firstQueueLine).toHaveAttribute("aria-current", "true");
    expect(within(lineQueue).getByText("ID / Item")).toBeInTheDocument();
    expect(within(lineQueue).getByText("Qty")).toBeInTheDocument();
    expect(within(lineQueue).getByText("Reservations")).toBeInTheDocument();
    expect(within(lineQueue).getByText("Whs")).toBeInTheDocument();
    expect(within(firstQueueLine).getByLabelText("Partially Fulfilled")).toBeInTheDocument();
    expect(within(lineQueue).queryByText("GLA")).not.toBeInTheDocument();
    expect(within(firstInspector).queryByText("Required")).not.toBeInTheDocument();
    expect(within(firstInspector).queryByText("1 / 4")).not.toBeInTheDocument();
    expect(within(firstInspector).queryByText("GLA")).not.toBeInTheDocument();
    expect(within(firstInspector).getByTestId("availability-panel")).toHaveAttribute("data-generic-code", "GEN-ONE");

    const secondLine = within(lineQueue).getByRole("button", {
      name: "View availability for agreement line 2, item ITEM-SECOND",
    });
    await user.click(secondLine);

    const secondInspector = screen.getByRole("region", { name: "Availability — 2 · GEN-TWO" });
    expect(screen.queryByRole("region", { name: "Availability — 1 · GEN-ONE" })).not.toBeInTheDocument();
    expect(secondLine).toHaveAttribute("aria-current", "true");
    expect(within(secondInspector).queryByText("3 / 8")).not.toBeInTheDocument();
    expect(within(secondInspector).queryByText("MAN")).not.toBeInTheDocument();
    expect(within(secondInspector).getByTestId("availability-panel")).toHaveAttribute("data-generic-code", "GEN-TWO");
  });

  it("shows legacy-style card columns and manages the selected line from the availability header", async () => {
    const user = userEvent.setup();
    mockedFetchReservationsForHeader.mockResolvedValue([
      { ...createFullyCoveringReservation(), warehouse: "HE0" },
      { ...createFullyCoveringReservation(), id: 32, assetId: "ASSET-32" },
      { ...createFullyCoveringReservation(), id: 33, assetId: "ASSET-33" },
    ]);
    renderWorkbench();

    await user.click(await screen.findByRole("button", { name: "View availability for agreement line 1" }));

    const lineQueue = screen.getByRole("list", { name: "Agreement line queue" });
    const firstQueueLine = within(lineQueue).getByRole("button", {
      name: "View availability for agreement line 1, item ITEM-STATUS",
    });
    const firstCard = firstQueueLine.closest<HTMLElement>('[role="listitem"]');
    expect(firstCard).not.toBeNull();
    expect(within(firstCard!).getByText("ASSET-31")).toBeVisible();
    expect(within(firstCard!).queryByText("ASSET-32")).not.toBeInTheDocument();
    expect(within(firstCard!).getByText("+2")).toBeVisible();
    expect(within(firstCard!).getByText("HE0, GLA")).toBeVisible();
    expect(within(firstCard!).queryByRole("button", { name: "Manage reservations for agreement line 1" })).toBeNull();

    const inspector = screen.getByRole("region", { name: "Availability — 1 · GEN-ONE" });
    const reservationSummary = within(inspector).getByLabelText("Reservations for agreement line 1");
    expect(within(reservationSummary).getByText("ASSET-31")).toBeVisible();
    expect(within(reservationSummary).getByText("ASSET-32")).toBeVisible();
    expect(within(reservationSummary).queryByText("ASSET-33")).not.toBeInTheDocument();
    expect(within(reservationSummary).getByRole("button", { name: "Remove reservation ASSET-31" })).toBeVisible();
    expect(within(reservationSummary).getByRole("button", { name: "Remove reservation ASSET-32" })).toBeVisible();
    expect(within(reservationSummary).queryByText("Manage")).not.toBeInTheDocument();

    await user.click(
      within(reservationSummary).getByRole("button", {
        name: "Show 1 more reservation for agreement line 1",
      }),
    );

    expect(within(reservationSummary).getByText("ASSET-33")).toBeVisible();
    expect(within(reservationSummary).getByRole("button", { name: "Remove reservation ASSET-33" })).toBeVisible();
  });

  it("opens availability without scrolling the agreement header out of view", async () => {
    const user = userEvent.setup();
    renderWorkbench();

    const firstLine = await screen.findByRole("button", { name: "View availability for agreement line 1" });
    const workspace = firstLine.closest("tr")?.closest('[role="region"]')?.parentElement?.parentElement;
    expect(workspace).not.toBeNull();
    const scrollIntoView = vi.fn();
    Object.defineProperty(workspace!, "scrollIntoView", { configurable: true, value: scrollIntoView });

    await user.click(firstLine);

    expect(await screen.findByTestId("availability-panel")).toBeInTheDocument();
    expect(scrollIntoView).not.toHaveBeenCalled();
  });

  it("shows the selected line attributes as read-only availability requirements", async () => {
    const user = userEvent.setup();
    const detail = createWorkbenchDetail();
    detail.lines[0].attributes = "Length (m):10;Category:Cable";
    mockedFetchAgreementDetail.mockResolvedValue(detail);
    renderWorkbench();

    await user.click(await screen.findByRole("button", { name: "View availability for agreement line 1" }));

    const inspector = screen.getByRole("region", { name: "Availability — 1 · GEN-ONE" });
    const attributes = within(inspector).getByLabelText("Selected attributes");
    expect(attributes).toHaveTextContent("Length (m): 10");
    expect(attributes).toHaveTextContent("Category: Cable");
    expect(within(attributes).queryAllByRole("button")).toHaveLength(0);
  });

  it("collapses, reopens for another line, and restores trigger focus when closed", async () => {
    const user = userEvent.setup();
    renderWorkbench();

    const firstLine = await screen.findByRole("button", { name: "View availability for agreement line 1" });
    await user.click(firstLine);
    const firstInspector = screen.getByRole("region", { name: "Availability — 1 · GEN-ONE" });
    const lineQueue = screen.getByRole("list", { name: "Agreement line queue" });
    const firstQueueLine = within(lineQueue).getByRole("button", {
      name: "View availability for agreement line 1, item ITEM-STATUS",
    });
    const collapse = within(firstInspector).getByRole("button", { name: "Collapse" });
    const panel = within(firstInspector).getByTestId("availability-panel");

    await user.click(collapse);
    expect(collapse).toHaveAttribute("aria-expanded", "false");
    expect(panel).not.toBeVisible();
    expect(firstQueueLine).toHaveAttribute("aria-current", "true");

    await user.click(within(firstInspector).getByRole("button", { name: "Expand" }));
    expect(panel).toBeVisible();
    await user.click(within(firstInspector).getByRole("button", { name: "Collapse" }));

    const secondLine = within(lineQueue).getByRole("button", {
      name: "View availability for agreement line 2, item ITEM-SECOND",
    });
    await user.click(secondLine);
    const secondInspector = screen.getByRole("region", { name: "Availability — 2 · GEN-TWO" });
    expect(within(secondInspector).getByTestId("availability-panel")).toBeVisible();

    await user.click(within(secondInspector).getByRole("button", { name: "Close" }));
    expect(screen.queryByRole("region", { name: "Availability — 2 · GEN-TWO" })).not.toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "View availability for agreement line 2" })).toHaveFocus(),
    );
  });

  it("shows a useful availability state when a line has no generic item number", async () => {
    const user = userEvent.setup();
    const detail = createWorkbenchDetail();
    detail.lines[0].genericItemNumber = null;
    mockedFetchAgreementDetail.mockResolvedValue(detail);
    renderWorkbench();

    await user.click(await screen.findByRole("button", { name: "View availability for agreement line 1" }));

    const inspector = screen.getByRole("region", { name: "Availability — 1 · ITEM-STATUS" });
    expect(
      within(inspector).getByText("Availability cannot be shown because this line has no generic item number."),
    ).toBeInTheDocument();
    expect(within(inspector).queryByTestId("availability-panel")).not.toBeInTheDocument();
  });

  it("resizes with keyboard and pointer input, clamps the size, and persists it", async () => {
    const user = userEvent.setup();
    renderWorkbench();
    await user.click(await screen.findByRole("button", { name: "View availability for agreement line 1" }));

    const separator = screen.getByRole("separator", { name: "Resize availability" });
    const linesPane = screen.getByRole("list", { name: "Agreement line queue" }).parentElement;
    expect(separator).toHaveAttribute("aria-orientation", "vertical");
    expect(separator).toHaveAttribute("aria-valuemin", "440");
    expect(separator).toHaveAttribute("aria-valuemax", "560");
    expect(separator).toHaveAttribute("aria-valuenow", "480");

    fireEvent.keyDown(separator, { key: "ArrowLeft" });
    expect(separator).toHaveAttribute("aria-valuenow", "470");
    expect(linesPane).toHaveStyle({ flexBasis: "470px" });
    expect(window.localStorage.getItem("agreementDetail.linesPaneWidth.v2")).toBe("470");

    fireEvent.keyDown(separator, { key: "End" });
    fireEvent.keyDown(separator, { key: "ArrowRight", shiftKey: true });
    expect(separator).toHaveAttribute("aria-valuenow", "560");
    fireEvent.keyDown(separator, { key: "Home" });
    fireEvent.keyDown(separator, { key: "ArrowLeft", shiftKey: true });
    expect(separator).toHaveAttribute("aria-valuenow", "440");

    const workspace = separator.parentElement;
    expect(workspace).not.toBeNull();
    Object.defineProperty(workspace!, "clientWidth", { configurable: true, value: 1000 });
    const setPointerCapture = vi.fn();
    const releasePointerCapture = vi.fn();
    Object.defineProperties(separator, {
      setPointerCapture: { configurable: true, value: setPointerCapture },
      hasPointerCapture: { configurable: true, value: () => true },
      releasePointerCapture: { configurable: true, value: releasePointerCapture },
    });

    fireHorizontalPointerEvent(separator, "pointerdown", { clientX: 500, pointerId: 7 });
    expect(setPointerCapture).toHaveBeenCalledWith(7);
    fireHorizontalPointerEvent(separator, "pointermove", { clientX: 600, pointerId: 7 });
    expect(linesPane).toHaveStyle({ flexBasis: "540px" });
    fireHorizontalPointerEvent(separator, "pointerup", { clientX: 600, pointerId: 7 });
    expect(releasePointerCapture).toHaveBeenCalledWith(7);
    expect(separator).toHaveAttribute("aria-valuenow", "540");
    expect(window.localStorage.getItem("agreementDetail.linesPaneWidth.v2")).toBe("540");
  });
});

describe("AgreementDetailPage bulk fulfilment", () => {
  beforeEach(async () => {
    vi.resetAllMocks();
    vi.mocked(fetchAppConfiguration).mockResolvedValue({
      environmentLabel: "Test",
      showPreviewBanner: false,
      legacyFrontendUrl: null,
    });
    await i18n.changeLanguage("en");
    mockedFetchReservationsForHeader.mockResolvedValue([]);
  });

  afterEach(cleanup);

  function renderBulkDetail() {
    const detail = createDetail(ApiFulfilmentStatus.Unfulfilled);
    detail.lines.push({
      ...detail.lines[0],
      id: 8,
      itemNumber: "ITEM-SECOND",
      agreementLineNumber: "2",
    });
    mockedFetchAgreementDetail.mockResolvedValue(detail);

    render(
      <MemoryRouter initialEntries={["/agreements/42"]}>
        <Routes>
          <Route path="/agreements/:headerId" element={<AgreementDetailPage />} />
        </Routes>
      </MemoryRouter>,
    );
  }

  it("submits depot fulfil for only the checked lines and clears selection after success", async () => {
    const user = userEvent.setup();
    mockedBulkDepotFulfil.mockResolvedValue({ processed: 1 });
    renderBulkDetail();

    const depotFulfil = await screen.findByRole("button", { name: "Depot Fulfil" });
    expect(depotFulfil).toBeDisabled();

    await user.click(screen.getByRole("checkbox", { name: "Select agreement line 1" }));
    expect(depotFulfil).toBeEnabled();
    expect(screen.getByText("1 selected")).toBeInTheDocument();

    await user.click(depotFulfil);

    await waitFor(() =>
      expect(mockedBulkDepotFulfil).toHaveBeenCalledWith({
        headerId: 42,
        lineIds: [7],
        includeAlreadyFulfilled: false,
      }),
    );
    expect(await screen.findByText("1 selected line processed.")).toBeInTheDocument();
    expect(screen.getByText("0 selected")).toBeInTheDocument();
    expect(depotFulfil).toBeDisabled();
    expect(mockedFetchAgreementDetail).toHaveBeenCalledTimes(2);
  });

  it("selects all lines and submits them to rehire", async () => {
    const user = userEvent.setup();
    mockedBulkRehire.mockResolvedValue({ processed: 2 });
    renderBulkDetail();

    await screen.findByText("ITEM-SECOND");
    await user.click(screen.getByRole("checkbox", { name: "Select all agreement lines" }));
    await user.click(screen.getByRole("button", { name: "Rehire" }));

    await waitFor(() =>
      expect(mockedBulkRehire).toHaveBeenCalledWith({
        headerId: 42,
        lineIds: [7, 8],
        includeAlreadyFulfilled: false,
      }),
    );
    expect(await screen.findByText("2 selected lines processed.")).toBeInTheDocument();
  });

  it("retains the checked lines when a bulk action fails so it can be retried", async () => {
    const user = userEvent.setup();
    mockedBulkDepotFulfil.mockRejectedValue(new Error("Request failed"));
    renderBulkDetail();

    const lineCheckbox = await screen.findByRole("checkbox", { name: "Select agreement line 1" });
    await user.click(lineCheckbox);
    await user.click(screen.getByRole("button", { name: "Depot Fulfil" }));

    expect(await screen.findByText("The selected lines could not be updated. Try again.")).toBeInTheDocument();
    expect(lineCheckbox).toBeChecked();
    expect(screen.getByRole("button", { name: "Depot Fulfil" })).toBeEnabled();
  });
});

describe("AgreementDetailPage equipment lines", () => {
  beforeEach(async () => {
    vi.resetAllMocks();
    vi.mocked(fetchAppConfiguration).mockResolvedValue({
      environmentLabel: "Test",
      showPreviewBanner: false,
      legacyFrontendUrl: null,
    });
    await i18n.changeLanguage("en");
    mockedFetchReservationsForHeader.mockResolvedValue([]);
    mockedFetchEquipmentCatalog.mockResolvedValue({
      productLines: [{ id: 4, description: "Generators", familyDescription: "Power" }],
      generics: [{ id: 9, productLineId: 4, code: "XGGN0060", description: "Diesel generator" }],
    });
    mockedFetchEquipmentGenericOptions.mockResolvedValue({ attributes: [], items: [] });
    mockedCreateEquipmentLine.mockResolvedValue(undefined);
    vi.mocked(agreementLineDeletionService.deleteLine).mockResolvedValue({
      lineId: 8,
      removedReservationCount: 0,
      headerStatus: ApiFulfilmentStatus.Unfulfilled,
    });
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  function renderEquipmentDetail(detail: AgreementDetail) {
    mockedFetchAgreementDetail.mockResolvedValue(detail);
    render(
      <MemoryRouter initialEntries={["/agreements/42"]}>
        <Routes>
          <Route path="/agreements/:headerId" element={<AgreementDetailPage />} />
        </Routes>
      </MemoryRouter>,
    );
  }

  it.each([
    ["temporary TODO agreement", "T-10042", ActivationStatus.TODO, ActivationStatus.TODO, true],
    ["active agreement", "A-10042", ActivationStatus.Activated, ActivationStatus.Activated, true],
    ["quote", "Q-10042", ActivationStatus.TODO, ActivationStatus.TODO, false],
    ["unactivated A agreement", "A-10042", ActivationStatus.TODO, ActivationStatus.TODO, false],
    ["activated T agreement", "T-10042", ActivationStatus.Activated, ActivationStatus.Activated, false],
    ["requested agreement", "A-10042", ActivationStatus.Requested, ActivationStatus.Requested, false],
    ["failed agreement", "A-10042", ActivationStatus.Failed, ActivationStatus.Failed, false],
    ["active A agreement with a TODO root", "A-10042", ActivationStatus.Activated, ActivationStatus.TODO, false],
    ["draft T agreement with an activated root", "T-10042", ActivationStatus.TODO, ActivationStatus.Activated, false],
  ])(
    "applies add-equipment eligibility for a %s",
    async (_scenario, agreementNumber, headerActivationStatus, lineActivationStatus, expected) => {
      const detail = createDetail(ApiFulfilmentStatus.Unfulfilled);
      detail.header.agreementNumber = agreementNumber;
      detail.header.activationStatus = headerActivationStatus;
      detail.lines[0].activationStatus = lineActivationStatus;
      renderEquipmentDetail(detail);

      await screen.findByText("ITEM-STATUS");
      const addAction = screen.queryByRole("button", { name: "Add equipment to line 1" });
      if (expected) expect(addAction).toBeInTheDocument();
      else expect(addAction).not.toBeInTheDocument();
    },
  );

  it.each([
    ["deleted", { isDeleted: true }],
    ["not fulfilment-bearing", { requiresFulfilment: false }],
  ])("does not offer equipment for a %s root line", async (_scenario, linePatch) => {
    const detail = createDetail(ApiFulfilmentStatus.Unfulfilled);
    detail.header.activationStatus = ActivationStatus.Activated;
    detail.lines[0] = { ...detail.lines[0], ...linePatch, activationStatus: ActivationStatus.Activated };
    renderEquipmentDetail(detail);

    await screen.findByText("ITEM-STATUS");
    expect(screen.queryByRole("button", { name: "Add equipment to line 1" })).not.toBeInTheDocument();
  });

  it("keeps dotted pending lines with their parent and labels their temporary lineage", async () => {
    const detail = createDetail(ApiFulfilmentStatus.Unfulfilled);
    detail.header.activationStatus = ActivationStatus.Activated;
    const parent = {
      ...detail.lines[0],
      activationStatus: ActivationStatus.Activated,
      agreementLineNumber: "A-10042-1",
      itemNumber: "PARENT-ONE",
    };
    detail.lines = [
      { ...parent, id: 20, agreementLineNumber: "A-10042-2", itemNumber: "PARENT-TWO" },
      {
        ...parent,
        id: 13,
        isSubline: true,
        activationStatus: ActivationStatus.TODO,
        agreementLineNumber: "A-10042-1.10",
        itemNumber: "CHILD-TEN",
      },
      parent,
      {
        ...parent,
        id: 12,
        isSubline: true,
        activationStatus: ActivationStatus.TODO,
        agreementLineNumber: "A-10042-1.2",
        itemNumber: "CHILD-TWO",
      },
      {
        ...parent,
        id: 11,
        isSubline: true,
        activationStatus: ActivationStatus.TODO,
        agreementLineNumber: "A-10042-1.1",
        itemNumber: "CHILD-ONE",
      },
    ];
    renderEquipmentDetail(detail);

    const table = await screen.findByRole("table");
    const itemOrder = within(table)
      .getAllByRole("row")
      .slice(1)
      .map((row) => within(row).getAllByRole("cell")[2].textContent);

    expect(itemOrder).toEqual(["PARENT-ONE", "CHILD-ONE", "CHILD-TWO", "CHILD-TEN", "PARENT-TWO"]);
    expect(screen.getAllByText("Pending new line")).toHaveLength(3);
    expect(screen.getAllByRole("button", { name: /Add equipment to line/ })).toHaveLength(2);
    expect(screen.queryByRole("button", { name: /Delete line/ })).not.toBeInTheDocument();
  });

  it("opens the row-scoped dialog and reloads the agreement after adding equipment", async () => {
    const user = userEvent.setup();
    const detail = createDetail(ApiFulfilmentStatus.Unfulfilled);
    detail.header.activationStatus = ActivationStatus.Activated;
    detail.lines[0].activationStatus = ActivationStatus.Activated;
    detail.lines[0].agreementLineNumber = "A-10042-1";
    renderEquipmentDetail(detail);

    await user.click(await screen.findByRole("button", { name: "Add equipment to line A-10042-1" }));
    const dialog = screen.getByRole("dialog", { name: "Add equipment to A-10042-1" });
    await user.selectOptions(await within(dialog).findByLabelText("Product line"), "4");
    await user.selectOptions(within(dialog).getByLabelText("Generic"), "9");
    await waitFor(() => expect(within(dialog).getByRole("button", { name: "Add equipment" })).toBeEnabled());
    await user.click(within(dialog).getByRole("button", { name: "Add equipment" }));

    await waitFor(() =>
      expect(mockedCreateEquipmentLine).toHaveBeenCalledWith(42, {
        parentLineId: 7,
        genericId: 9,
        itemNumber: null,
        attributes: [],
        quantity: 1,
      }),
    );
    expect(await screen.findByText("Equipment added as a pending new line.")).toBeInTheDocument();
    expect(mockedFetchAgreementDetail).toHaveBeenCalledTimes(2);
  });

  it("confirms and deletes a pending T line, then reloads the agreement", async () => {
    const user = userEvent.setup();
    const detail = createDetail(ApiFulfilmentStatus.Unfulfilled);
    detail.header.agreementNumber = "T-10042";
    detail.header.activationStatus = ActivationStatus.TODO;
    detail.lines[0].activationStatus = ActivationStatus.TODO;
    detail.lines.push({
      ...detail.lines[0],
      id: 8,
      isSubline: true,
      agreementLineNumber: "T-10042-1.1",
      itemNumber: "PENDING-ITEM",
      activationStatus: ActivationStatus.TODO,
    });
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    renderEquipmentDetail(detail);

    await user.click(await screen.findByRole("button", { name: "Delete line T-10042-1.1" }));

    expect(confirmSpy).toHaveBeenCalledWith("Delete line T-10042-1.1 and remove its 0 reservations?");
    await waitFor(() => expect(vi.mocked(agreementLineDeletionService.deleteLine)).toHaveBeenCalledWith(42, 8));
    expect(await screen.findByText("Line T-10042-1.1 deleted.")).toBeInTheDocument();
    expect(mockedFetchAgreementDetail).toHaveBeenCalledTimes(2);
  });

  it("reports a server conflict when T-line deletion is rejected", async () => {
    const user = userEvent.setup();
    const detail = createDetail(ApiFulfilmentStatus.Unfulfilled);
    detail.header.agreementNumber = "T-10042";
    detail.header.activationStatus = ActivationStatus.TODO;
    detail.lines[0].activationStatus = ActivationStatus.TODO;
    detail.lines.push({
      ...detail.lines[0],
      id: 8,
      isSubline: true,
      agreementLineNumber: "T-10042-1.1",
      activationStatus: ActivationStatus.TODO,
    });
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.mocked(agreementLineDeletionService.deleteLine).mockRejectedValue(new Error("Line has reservations"));
    renderEquipmentDetail(detail);

    await user.click(await screen.findByRole("button", { name: "Delete line T-10042-1.1" }));

    expect(await screen.findByText(/The line could not be deleted/)).toBeInTheDocument();
    expect(mockedFetchAgreementDetail).toHaveBeenCalledTimes(2);
  });
});

describe("AgreementDetailPage parity actions", () => {
  beforeEach(async () => {
    vi.resetAllMocks();
    await i18n.changeLanguage("en");
    vi.mocked(fetchAppConfiguration).mockResolvedValue({
      environmentLabel: "Test",
      showPreviewBanner: false,
      legacyFrontendUrl: "https://legacy.example/of/",
    });
    mockedFetchReservationsForHeader.mockResolvedValue([]);
  });
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  function renderActions(detail: AgreementDetail) {
    mockedFetchAgreementDetail.mockResolvedValue(detail);
    return render(
      <MemoryRouter initialEntries={["/agreements/42"]}>
        <Routes>
          <Route path="/agreements/:headerId" element={<AgreementDetailPage />} />
        </Routes>
      </MemoryRouter>,
    );
  }

  it.each([0, 1, 2, 99])("disables activation for header fulfilment %s despite fulfilled lines", async (status) => {
    const detail = createDetail(ApiFulfilmentStatus.FullyFulfilled);
    detail.header.fulfilmentStatus = status as ApiFulfilmentStatus;
    renderActions(detail);
    expect(await screen.findByRole("button", { name: "Activate" })).toBeDisabled();
  });

  it.each([ActivationStatus.Requested, ActivationStatus.Activated, 99])(
    "blocks activation state %s",
    async (status) => {
      const detail = createDetail(ApiFulfilmentStatus.FullyFulfilled);
      detail.header.activationStatus = status;
      renderActions(detail);
      expect(await screen.findByRole("button", { name: "Activate" })).toBeDisabled();
    },
  );

  it("blocks a fulfilled quote and a requested line", async () => {
    const detail = createDetail(ApiFulfilmentStatus.FullyFulfilled);
    detail.header.agreementNumber = "Q-42";
    detail.lines[0].activationStatus = ActivationStatus.Requested;
    renderActions(detail);
    expect(await screen.findByRole("button", { name: "Activate" })).toBeDisabled();
  });

  it("submits activation once and disables the action while awaiting the response", async () => {
    let complete!: () => void;
    vi.mocked(activateAgreement).mockImplementation(
      () =>
        new Promise<{ type: string; headerId: number }>((resolve) => {
          complete = () => resolve({ type: "activation", headerId: 42 });
        }),
    );
    renderActions(createDetail(ApiFulfilmentStatus.FullyFulfilled));
    const button = await screen.findByRole("button", { name: "Activate" });
    expect(button).toBeEnabled();
    fireEvent.click(button);
    fireEvent.click(button);
    expect(activateAgreement).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("button", { name: "Activating…" })).toBeDisabled();
    complete();
    expect(await screen.findByText("Activation requested successfully.")).toBeInTheDocument();
  });

  it("opens the existing report with encoded identifiers and quote context", async () => {
    const detail = createDetail(ApiFulfilmentStatus.FullyFulfilled);
    detail.header.agreementNumber = "A/42";
    detail.header.quotePublicId = "quote & 42";
    renderActions(detail);
    const link = await screen.findByRole("link", { name: "Order summary sheet" });
    expect(link).toHaveAttribute("href", "https://legacy.example/of/report/A%2F42/display?quoteId=quote+%26+42");
    expect(link).toHaveAttribute("target", "_blank");
  });

  it("keeps the summary button disabled without helper text when configuration is missing", async () => {
    vi.mocked(fetchAppConfiguration).mockRejectedValue(new Error("unavailable"));
    renderActions(createDetail(ApiFulfilmentStatus.FullyFulfilled));
    expect(await screen.findByRole("button", { name: "Order summary sheet" })).toBeDisabled();
    expect(screen.queryByText(/legacy UI address is not configured/)).not.toBeInTheDocument();
  });

  it("does not offer an order summary for a T agreement", async () => {
    const detail = createDetail(ApiFulfilmentStatus.FullyFulfilled);
    detail.header.agreementNumber = "T-42";
    renderActions(detail);
    await screen.findByText("ITEM-STATUS");
    expect(screen.queryByRole("link", { name: "Order summary sheet" })).not.toBeInTheDocument();
  });

  it("resets the full header without requiring selected lines and updates activation", async () => {
    const detail = createWorkbenchDetail();
    const reset = structuredClone(detail);
    reset.header.fulfilmentStatus = ApiFulfilmentStatus.Unfulfilled;
    reset.lines.forEach((line) => {
      line.fulfilmentStatus = ApiFulfilmentStatus.Unfulfilled;
      line.quantityFulfilled = 0;
    });
    vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.mocked(unfulfilAgreement).mockResolvedValue({ linesReset: 2, reservationsRemoved: 1, headerStatus: 0 });
    renderActions(detail);
    mockedFetchAgreementDetail.mockResolvedValue(reset);
    mockedFetchReservationsForHeader
      .mockResolvedValueOnce([{ ...createFullyCoveringReservation(), isConfirmed: false }])
      .mockResolvedValue([]);
    await userEvent.click(await screen.findByRole("button", { name: "Unfulfil all lines" }));
    await waitFor(() => expect(unfulfilAgreement).toHaveBeenCalledWith(42));
    expect(await screen.findByText("All lines unfulfilled. Reservations removed: 1.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Activate" })).toBeDisabled();
    expect(window.confirm).toHaveBeenCalledWith(expect.stringContaining("whole agreement"));
  });

  it("cancels reset without calling the server", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(false);
    renderActions(createWorkbenchDetail());
    await userEvent.click(await screen.findByRole("button", { name: "Unfulfil all lines" }));
    expect(unfulfilAgreement).not.toHaveBeenCalled();
  });

  it("disables reset for confirmed reservations", async () => {
    mockedFetchReservationsForHeader.mockResolvedValue([createFullyCoveringReservation()]);
    renderActions(createWorkbenchDetail());
    expect(await screen.findByRole("button", { name: "Unfulfil all lines" })).toBeDisabled();
  });

  it("does not expose destructive actions to read-only users", async () => {
    mockedFetchAgreementDetail.mockResolvedValue(createWorkbenchDetail());
    renderReadOnlyDetail();
    await screen.findByText("ITEM-SECOND");
    expect(screen.queryByRole("button", { name: "Activate" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Unfulfil all lines" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Delete line/ })).not.toBeInTheDocument();
    expect(await screen.findByRole("link", { name: "Order summary sheet" })).toBeInTheDocument();
  });

  it("protects the last live T line", async () => {
    const detail = createDetail(ApiFulfilmentStatus.Unfulfilled);
    detail.header.agreementNumber = "T-42";
    renderActions(detail);
    expect(await screen.findByRole("button", { name: "Delete line 1" })).toBeDisabled();
  });
});
