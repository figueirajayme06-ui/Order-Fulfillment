import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "./api";
import {
  createEquipmentLine,
  createReservation,
  deleteEquipmentLine,
  deleteReservation,
  fetchAgreementDetail,
  fetchAgreements,
  fetchEquipmentCatalog,
  fetchEquipmentGenericOptions,
  fetchReservationsForHeader,
  type AgreementDetail,
  type AgreementFilterParams,
  type AgreementListItem,
} from "./agreementsService";
import type { Reservation } from "../types";

vi.mock("./api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

const mockedApi = vi.mocked(api);

const agreementList: AgreementListItem[] = [
  {
    id: 17,
    agreementNumber: "A-10017",
    customerName: "Northwind",
    customerNumber: "C-42",
    division: "01",
    warehouse: "GLA",
    fulfilmentStatus: 1,
    onHireDate: "2026-08-01",
    offHireDate: "2026-08-31",
    isDeleted: false,
    orderSource: "D365",
    lineCount: 2,
    deliveryDate: "2026-08-01",
    validFromDate: "2026-07-28",
    validToDate: "2026-09-01",
    terminationDate: null,
    collectionDate: "2026-09-01",
    customerAddress: "Glasgow",
    lastUpdatedByName: "Planner One",
    opportunityName: "Summer works",
  },
];

const allFilterParams: AgreementFilterParams = {
  hideFulfilled: false,
  showHistorical: true,
  division: "01,02",
  search: "northwind",
  customerName: "North",
  agreementNumber: "A-10017",
  warehouse: "GLA",
  onHireDateFrom: "2026-08-01",
  onHireDateTo: "2026-08-02",
  offHireDateFrom: "2026-08-30",
  offHireDateTo: "2026-08-31",
  deliveryDateFrom: "2026-07-31",
  deliveryDateTo: "2026-08-01",
  validFromDate: "2026-07-28",
  validToDate: "2026-09-01",
  terminationDateFrom: "2026-08-20",
  terminationDateTo: "2026-08-21",
  collectionDateFrom: "2026-09-01",
  collectionDateTo: "2026-09-02",
  customerAddress: "Glasgow",
  lastUpdatedByName: "Planner",
  orderType: "agreement",
  status: 3,
  take: 250,
};

function axiosResponseError(status: number): unknown {
  return {
    isAxiosError: true,
    response: { status },
  };
}

beforeEach(() => {
  vi.resetAllMocks();
});

describe("agreementsService detail and reservations", () => {
  describe("fetchAgreementDetail", () => {
    const detail = { header: { id: 17 }, lines: [] } as unknown as AgreementDetail;

    it("uses the canonical detail route", async () => {
      mockedApi.get.mockResolvedValueOnce({ data: detail });

      const result = await fetchAgreementDetail(17);

      expect(result).toBe(detail);
      expect(mockedApi.get).toHaveBeenCalledOnce();
      expect(mockedApi.get).toHaveBeenCalledWith("/api/agreements/17");
    });

    it("retries the matching legacy detail route only after a 404", async () => {
      mockedApi.get.mockRejectedValueOnce(axiosResponseError(404)).mockResolvedValueOnce({ data: detail });

      const result = await fetchAgreementDetail(17);

      expect(result).toBe(detail);
      expect(mockedApi.get).toHaveBeenCalledTimes(2);
      expect(mockedApi.get).toHaveBeenNthCalledWith(1, "/api/agreements/17");
      expect(mockedApi.get).toHaveBeenNthCalledWith(2, "/api/orders/17");
    });

    it("preserves non-404 detail failures", async () => {
      const error = axiosResponseError(500);
      mockedApi.get.mockRejectedValueOnce(error);

      await expect(fetchAgreementDetail(17)).rejects.toBe(error);

      expect(mockedApi.get).toHaveBeenCalledOnce();
      expect(mockedApi.get).toHaveBeenCalledWith("/api/agreements/17");
    });
  });

  it("uses the header reservation route", async () => {
    const reservations = [{ id: 31 }] as unknown as Reservation[];
    mockedApi.get.mockResolvedValueOnce({ data: reservations });

    const result = await fetchReservationsForHeader(17);

    expect(result).toBe(reservations);
    expect(mockedApi.get).toHaveBeenCalledWith("/api/reservations/header/17");
  });

  it("posts the reservation payload without changing optional flags", async () => {
    const payload = {
      assetId: "ASSET/42",
      lineId: 22,
      itemNumber: "ITEM-7",
      quantity: 1,
      warehouse: "GLA",
      notes: "Keep with line",
      isConfirmed: false,
      isDepotFulfilled: true,
      isRehire: false,
    };
    const reservation = { id: 31, ...payload } as unknown as Reservation;
    mockedApi.post.mockResolvedValueOnce({ data: reservation });

    const result = await createReservation(payload);

    expect(result).toBe(reservation);
    expect(mockedApi.post).toHaveBeenCalledWith("/api/reservations", payload);
  });

  it("deletes a reservation by its numeric id", async () => {
    mockedApi.delete.mockResolvedValueOnce({});

    await deleteReservation(31);

    expect(mockedApi.delete).toHaveBeenCalledWith("/api/reservations/31");
  });

  it("loads the equipment catalogue for an agreement", async () => {
    const catalog = {
      productLines: [{ id: 4, description: "Generators", familyDescription: "Power" }],
      generics: [{ id: 9, productLineId: 4, code: "XGGN0060", description: "Diesel generator" }],
    };
    mockedApi.get.mockResolvedValueOnce({ data: catalog });

    await expect(fetchEquipmentCatalog(17)).resolves.toBe(catalog);
    expect(mockedApi.get).toHaveBeenCalledWith("/api/agreements/17/equipment/catalog");
  });

  it("loads filtered generic options using the semicolon attribute contract", async () => {
    const options = {
      attributes: [{ name: "Voltage", values: ["240V"] }],
      items: [{ itemNumber: "GEN-60", description: "Generator 60" }],
    };
    mockedApi.get.mockResolvedValueOnce({ data: options });

    await expect(fetchEquipmentGenericOptions(17, 9, ["Fuel:Diesel", "Voltage:240V"])).resolves.toBe(options);
    expect(mockedApi.get).toHaveBeenCalledWith("/api/agreements/17/equipment/catalog/generics/9", {
      params: { attributes: "Fuel:Diesel;Voltage:240V" },
    });
  });

  it("omits the attributes query when no filters are selected", async () => {
    const options = { attributes: [], items: [] };
    mockedApi.get.mockResolvedValueOnce({ data: options });

    await expect(fetchEquipmentGenericOptions(17, 9, [])).resolves.toBe(options);
    expect(mockedApi.get).toHaveBeenCalledWith("/api/agreements/17/equipment/catalog/generics/9");
  });

  it("posts a new equipment line without rewriting its selections", async () => {
    const payload = {
      parentLineId: 22,
      genericId: 9,
      itemNumber: "GEN-60",
      attributes: ["Fuel:Diesel"],
      quantity: 2,
    };
    mockedApi.post.mockResolvedValueOnce({});

    await createEquipmentLine(17, payload);

    expect(mockedApi.post).toHaveBeenCalledWith("/api/agreements/17/equipment", payload);
  });

  it("deletes an equipment line using its agreement-scoped route", async () => {
    mockedApi.delete.mockResolvedValueOnce({});

    await deleteEquipmentLine(17, 23);

    expect(mockedApi.delete).toHaveBeenCalledWith("/api/agreements/17/equipment/23");
  });
});

describe("fetchAgreements", () => {
  it("uses the canonical route and passes every filter without rewriting it", async () => {
    mockedApi.get.mockResolvedValueOnce({ data: agreementList });

    const result = await fetchAgreements(allFilterParams);

    expect(result).toBe(agreementList);
    expect(mockedApi.get).toHaveBeenCalledOnce();
    expect(mockedApi.get).toHaveBeenCalledWith("/api/agreements", { params: allFilterParams });
  });

  it("retries the legacy route with the same filters when the canonical route returns 404", async () => {
    mockedApi.get.mockRejectedValueOnce(axiosResponseError(404)).mockResolvedValueOnce({ data: agreementList });

    const result = await fetchAgreements(allFilterParams);

    expect(result).toBe(agreementList);
    expect(mockedApi.get).toHaveBeenCalledTimes(2);
    expect(mockedApi.get).toHaveBeenNthCalledWith(1, "/api/agreements", { params: allFilterParams });
    expect(mockedApi.get).toHaveBeenNthCalledWith(2, "/api/orders", { params: allFilterParams });
  });

  it.each([
    ["a non-404 API response", axiosResponseError(403)],
    ["a non-Axios failure", new Error("request setup failed")],
  ])("does not use the legacy route for %s", async (_description, error) => {
    mockedApi.get.mockRejectedValueOnce(error);

    await expect(fetchAgreements(allFilterParams)).rejects.toBe(error);

    expect(mockedApi.get).toHaveBeenCalledOnce();
    expect(mockedApi.get).toHaveBeenCalledWith("/api/agreements", { params: allFilterParams });
  });
});
