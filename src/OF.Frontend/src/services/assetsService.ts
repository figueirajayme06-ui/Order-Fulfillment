import api from "./api";
import type { Asset, AssetEnrichment, AssetProfile } from "../types";

export interface AssetFilterParams {
  search?: string;
  warehouse?: string;
  status?: string;
  statuses?: string;
  division?: string;
  facility?: string;
  itemNumber?: string;
  description?: string;
  agreementNumber?: string;
  deliveryDateFrom?: string;
  deliveryDateTo?: string;
  validFromDate?: string;
  validToDate?: string;
  warehouseLocation?: string;
  individualItemNumber?: string;
  terminationDateFrom?: string;
  terminationDateTo?: string;
  collectionDateFrom?: string;
  collectionDateTo?: string;
  estimatedReadyDateFrom?: string;
  estimatedReadyDateTo?: string;
  excludeStatuses?: string;
  take?: number;
  exactMatch?: boolean;
}

export async function fetchAssets(params: AssetFilterParams): Promise<Asset[]> {
  const response = await api.get<Asset[]>("/api/assets", { params });
  return response.data;
}

export async function fetchAsset(id: string): Promise<Asset> {
  const response = await api.get<Asset>(`/api/assets/${id}`);
  return response.data;
}

export async function fetchAssetProfile(id: string, fromDate: string, toDate: string): Promise<AssetProfile> {
  const response = await api.get<AssetProfile>(`/api/assets/${encodeURIComponent(id)}/profile`, {
    params: { fromDate, toDate },
  });
  return response.data;
}

export async function fetchAssetEnrichment(id: string, serviceLimit = 20): Promise<AssetEnrichment> {
  const response = await api.get<AssetEnrichment>(`/api/assets/${encodeURIComponent(id)}/enrichment`, {
    params: { serviceLimit },
  });
  return response.data;
}

export async function addAssetToRingfence(ringfenceId: number, assetId: string): Promise<void> {
  await api.post(`/api/ringfence/${ringfenceId}/items`, { assetId });
}

export async function removeAssetFromRingfence(ringfenceId: number, assetId: string): Promise<void> {
  await api.delete(`/api/ringfence/${ringfenceId}/items/${assetId}`);
}
