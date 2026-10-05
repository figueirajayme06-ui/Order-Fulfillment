import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "../../i18n";
import {
  createEquipmentLine,
  fetchEquipmentCatalog,
  fetchEquipmentGenericOptions,
} from "../../services/agreementsService";
import { ActivationStatus, ApiFulfilmentStatus, type AgreementLine } from "../../types";
import { AddEquipmentDialog } from "./AddEquipmentDialog";

vi.mock("../../services/agreementsService", () => ({
  createEquipmentLine: vi.fn(),
  fetchEquipmentCatalog: vi.fn(),
  fetchEquipmentGenericOptions: vi.fn(),
}));

const mockedCreateEquipmentLine = vi.mocked(createEquipmentLine);
const mockedFetchEquipmentCatalog = vi.mocked(fetchEquipmentCatalog);
const mockedFetchEquipmentGenericOptions = vi.mocked(fetchEquipmentGenericOptions);

const parentLine: AgreementLine = {
  id: 7,
  isSubline: false,
  headerId: 42,
  itemNumber: "GEN-BASE",
  genericItemNumber: "XGGN0060",
  quantity: 1,
  deliveryDate: null,
  validFromDate: "2026-08-01",
  validToDate: "2026-08-31",
  terminationDate: null,
  attributes: null,
  fulfilmentStatus: ApiFulfilmentStatus.Unfulfilled,
  activationStatus: ActivationStatus.Activated,
  requiresFulfilment: true,
  isDeleted: false,
  changeSequence: 1,
  warehouse: "GLA",
  division: "01",
  facility: "GLA",
  orderSource: "D365",
  orderLineNumber: "10",
  agreementLineNumber: "A-10042-1",
  quantityFulfilled: 0,
  lastUpdatedBy: null,
  lastUpdatedDate: null,
};

const catalog = {
  productLines: [{ id: 4, description: "Generators", familyDescription: "Power" }],
  generics: [{ id: 9, productLineId: 4, code: "XGGN0060", description: "Diesel generator" }],
};

const genericOptions = {
  attributes: [
    { name: "Fuel", values: ["Diesel", "HVO"] },
    { name: "Voltage", values: ["110V", "240V"] },
  ],
  items: [{ itemNumber: "GEN-60", description: "Generator 60" }],
};

describe("AddEquipmentDialog", () => {
  beforeEach(async () => {
    vi.resetAllMocks();
    await i18n.changeLanguage("en");
    mockedFetchEquipmentCatalog.mockResolvedValue(catalog);
    mockedFetchEquipmentGenericOptions.mockResolvedValue(genericOptions);
    mockedCreateEquipmentLine.mockResolvedValue(undefined);
  });

  afterEach(cleanup);

  it("cascades catalogue choices and submits the selected equipment", async () => {
    const user = userEvent.setup();
    const onAdded = vi.fn().mockResolvedValue(undefined);

    render(<AddEquipmentDialog headerId={42} parentLine={parentLine} onAdded={onAdded} onCancel={vi.fn()} />);

    expect(screen.getByRole("dialog", { name: "Add equipment to A-10042-1" })).toBeInTheDocument();
    expect(screen.getByText("GLA")).toBeInTheDocument();
    await user.selectOptions(await screen.findByLabelText("Product line"), "4");
    await user.selectOptions(screen.getByLabelText("Generic"), "9");

    await waitFor(() => expect(mockedFetchEquipmentGenericOptions).toHaveBeenCalledWith(42, 9, []));
    expect(await screen.findByText("1 matching item")).toBeInTheDocument();
    await user.click(screen.getByText("Attributes", { selector: "summary > span" }));
    await user.selectOptions(await screen.findByLabelText("Voltage"), "240V");
    await waitFor(() => expect(mockedFetchEquipmentGenericOptions).toHaveBeenLastCalledWith(42, 9, ["Voltage:240V"]));
    expect(screen.getByText("1 selected")).toBeInTheDocument();
    expect(screen.getByLabelText("Selected attributes")).toHaveTextContent("Voltage: 240V");
    await waitFor(() => expect(screen.getByLabelText("Exact item (optional)")).toBeEnabled());
    await user.selectOptions(screen.getByLabelText("Exact item (optional)"), "GEN-60");
    await user.clear(screen.getByLabelText("Quantity"));
    await user.type(screen.getByLabelText("Quantity"), "2");
    await user.click(screen.getByRole("button", { name: "Add equipment" }));

    await waitFor(() => {
      expect(mockedCreateEquipmentLine).toHaveBeenCalledWith(42, {
        parentLineId: 7,
        genericId: 9,
        itemNumber: "GEN-60",
        attributes: ["Voltage:240V"],
        quantity: 2,
      });
    });
    expect(onAdded).toHaveBeenCalledOnce();
  });

  it("shows a bounded quantity error and does not submit invalid input", async () => {
    const user = userEvent.setup();

    render(<AddEquipmentDialog headerId={42} parentLine={parentLine} onAdded={vi.fn()} onCancel={vi.fn()} />);

    await user.clear(await screen.findByLabelText("Quantity"));
    await user.type(screen.getByLabelText("Quantity"), "1001");

    expect(screen.getByRole("alert")).toHaveTextContent("Enter a whole quantity from 1 to 1,000.");
    expect(screen.getByRole("button", { name: "Add equipment" })).toBeDisabled();
    expect(mockedCreateEquipmentLine).not.toHaveBeenCalled();
  });

  it("distinguishes an empty catalogue from a catalogue failure", async () => {
    mockedFetchEquipmentCatalog.mockResolvedValueOnce({ productLines: [], generics: [] });
    const { unmount } = render(
      <AddEquipmentDialog headerId={42} parentLine={parentLine} onAdded={vi.fn()} onCancel={vi.fn()} />,
    );

    expect(await screen.findByText("No equipment is available for this agreement.")).toBeInTheDocument();
    unmount();

    mockedFetchEquipmentCatalog.mockRejectedValueOnce(new Error("unavailable"));
    render(<AddEquipmentDialog headerId={42} parentLine={parentLine} onAdded={vi.fn()} onCancel={vi.fn()} />);

    expect(await screen.findByRole("alert")).toHaveTextContent("Equipment options could not be loaded");
  });
});
