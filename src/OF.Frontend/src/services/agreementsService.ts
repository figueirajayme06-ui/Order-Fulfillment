import axios from "axios";
import api from "./api";
import type { AgreementHeader, AgreementLine, ApiFulfilmentStatus, Reservation } from "../types";

export interface AgreementListItem {
  id: number;
  agreementNumber: string | null;
  customerName: string | null;
  customerNumber: string | null;
  division: string;
  warehouse: string | null;
  fulfilmentStatus: ApiFulfilmentStatus;
  onHireDate: string | null;
  offHireDate: string | null;
  isDeleted: boolean;
  orderSource: string;
  lineCount: number | null;
  deliveryDate: string | null;
  validFromDate: string | null;
  validToDate: string | null;
  terminationDate: string | null;
  collectionDate: string | null;
  customerAddress: string | null;
  lastUpdatedByName: string | null;
  opportunityName: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  lastUpdatedDate?: string | null;
  opportunityStage?: string | null;
  probability?: number | null;
  minFulfilmentStatus?: ApiFulfilmentStatus | null;
  maxFulfilmentStatus?: ApiFulfilmentStatus | null;
  noteCount?: number;
}

export interface AgreementDetail {
  header: AgreementHeader;
  lines: AgreementLine[];
}

export interface EquipmentProductLine {
  id: number;
  description: string;
  familyDescription: string;
}

export interface EquipmentGeneric {
  id: number;
  productLineId: number;
  code: string;
  description: string;
}

export interface EquipmentCatalog {
  productLines: EquipmentProductLine[];
  generics: EquipmentGeneric[];
}

export interface EquipmentAttribute {
  name: string;
  values: string[];
}

export interface EquipmentItem {
  itemNumber: string;
  description: string;
}

export interface EquipmentGenericOptions {
  attributes: EquipmentAttribute[];
  items: EquipmentItem[];
}

export interface CreateEquipmentLineRequest {
  parentLineId: number;
  genericId: number;
  itemNumber?: string | null;
  attributes: string[];
  quantity: number;
}

export interface AgreementFilterParams {
  hideFulfilled?: boolean;
  showHistorical?: boolean;
  division?: string;
  search?: string;
  customerName?: string;
  agreementNumber?: string;
  warehouse?: string;
  onHireDateFrom?: string;
  onHireDateTo?: string;
  offHireDateFrom?: string;
  offHireDateTo?: string;
  deliveryDateFrom?: string;
  deliveryDateTo?: string;
  validFromDate?: string;
  validToDate?: string;
  terminationDateFrom?: string;
  terminationDateTo?: string;
  collectionDateFrom?: string;
  collectionDateTo?: string;
  customerAddress?: string;
  lastUpdatedByName?: string;
  orderType?: "quote" | "temporaryAgreement" | "agreement";
  orderTypes?: string;
  status?: number;
  statuses?: string;
  take?: number;
}

function shouldFallbackToLegacyOrdersEndpoint(error: unknown): boolean {
  if (!axios.isAxiosError(error)) {
    return false;
  }

  return error.response?.status === 404;
}

export async function fetchAgreements(params: AgreementFilterParams): Promise<AgreementListItem[]> {
  try {
    const response = await api.get<AgreementListItem[]>("/api/agreements", { params });
    return response.data;
  } catch (error) {
    if (!shouldFallbackToLegacyOrdersEndpoint(error)) {
      throw error;
    }

    const response = await api.get<AgreementListItem[]>("/api/orders", { params });
    return response.data;
  }
}

export async function fetchAgreementDetail(headerId: number): Promise<AgreementDetail> {
  try {
    const response = await api.get<AgreementDetail>(`/api/agreements/${headerId}`);
    return response.data;
  } catch (error) {
    if (!shouldFallbackToLegacyOrdersEndpoint(error)) {
      throw error;
    }

    const response = await api.get<AgreementDetail>(`/api/orders/${headerId}`);
    return response.data;
  }
}

export async function fetchReservationsForHeader(headerId: number): Promise<Reservation[]> {
  const response = await api.get<Reservation[]>(`/api/reservations/header/${headerId}`);
  return response.data;
}

export async function createReservation(data: {
  assetId: string;
  lineId: number;
  itemNumber: string;
  quantity: number;
  warehouse: string;
  notes?: string;
  isConfirmed?: boolean;
  isDepotFulfilled?: boolean;
  isRehire?: boolean;
}): Promise<Reservation> {
  const response = await api.post<Reservation>("/api/reservations", data);
  return response.data;
}

export async function deleteReservation(id: number): Promise<void> {
  await api.delete(`/api/reservations/${id}`);
}

export async function fetchEquipmentCatalog(headerId: number): Promise<EquipmentCatalog> {
  const response = await api.get<EquipmentCatalog>(`/api/agreements/${headerId}/equipment/catalog`);
  return response.data;
}

export async function fetchEquipmentGenericOptions(
  headerId: number,
  genericId: number,
  attributes: string[],
): Promise<EquipmentGenericOptions> {
  const url = `/api/agreements/${headerId}/equipment/catalog/generics/${genericId}`;
  const response = attributes.length
    ? await api.get<EquipmentGenericOptions>(url, { params: { attributes: attributes.join(";") } })
    : await api.get<EquipmentGenericOptions>(url);
  return response.data;
}

export async function createEquipmentLine(headerId: number, data: CreateEquipmentLineRequest): Promise<void> {
  await api.post(`/api/agreements/${headerId}/equipment`, data);
}

export async function deleteEquipmentLine(headerId: number, lineId: number): Promise<void> {
  await api.delete(`/api/agreements/${headerId}/equipment/${lineId}`);
}
