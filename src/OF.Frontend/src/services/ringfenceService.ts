import api from "./api";
import type { Ringfence } from "../types";

export interface RingfenceListItem {
  id: number;
  title: string;
  fromDate: string;
  toDate: string;
  divisions: string;
  warehouse: string | null;
  owner: string | null;
  assetCount: number;
  createdBy: string;
  createdAt: string;
}

export interface RingfenceAsset {
  id: string;
  division: string | null;
  warehouse: string | null;
  description: string | null;
  itemNumber: string | null;
  status: string | null;
  warehouseLocation: string | null;
}

export interface RingfenceDetail {
  ringfence: Ringfence & {
    title: string;
    owner: string | null;
    warehouse: string | null;
    createdBy: string;
    createdAt: string;
  };
  items: Array<{ id: number; assetId: string }>;
  assets: RingfenceAsset[];
}

export interface RingfenceOverlap {
  ringfenceId: number;
  assetIds: string[];
  title: string;
  fromDate: string;
  toDate: string;
  owner: string | null;
}

/**
 * The result of validating or applying a group of asset assignments.  The
 * server owns the classification so callers never have to infer access,
 * duplicate, or overlap results from a sequence of individual mutations.
 */
export interface RingfenceItemBatchResult {
  readyAssetIds: string[];
  alreadyAssignedAssetIds: string[];
  unavailableAssetIds: string[];
  addedAssetIds: string[];
  overlaps: RingfenceOverlap[];
  requiresOverlapAcknowledgement: boolean;
}

export interface RingfenceInput {
  title: string;
  fromDate: string;
  toDate: string;
  divisions: string;
  warehouse?: string;
  owner?: string;
}

export async function fetchRingfences(): Promise<RingfenceListItem[]> {
  const response = await api.get<RingfenceListItem[]>("/api/ringfence");
  return response.data;
}

export async function fetchRingfence(id: number): Promise<RingfenceDetail> {
  const response = await api.get<RingfenceDetail>(`/api/ringfence/${id}`);
  return response.data;
}

export async function createRingfence(data: RingfenceInput): Promise<Ringfence> {
  const response = await api.post<Ringfence>("/api/ringfence", data);
  return response.data;
}

export async function updateRingfence(id: number, data: RingfenceInput): Promise<Ringfence> {
  const response = await api.put<Ringfence>(`/api/ringfence/${id}`, data);
  return response.data;
}

export async function deleteRingfence(id: number): Promise<void> {
  await api.delete(`/api/ringfence/${id}`);
}

export async function checkRingfenceOverlaps(id: number, assetIds: string[]): Promise<RingfenceOverlap[]> {
  const response = await api.post<{ overlaps: RingfenceOverlap[] }>(`/api/ringfence/${id}/overlaps`, { assetIds });
  return response.data.overlaps;
}

/**
 * Validates a proposed asset group without changing the ringfence.  Use this
 * before showing an overlap acknowledgement so the user sees the same result
 * that the subsequent batch mutation will use.
 */
export async function preflightRingfenceItems(id: number, assetIds: string[]): Promise<RingfenceItemBatchResult> {
  const response = await api.post<RingfenceItemBatchResult>(`/api/ringfence/${id}/items/preflight`, { assetIds });
  return response.data;
}

/**
 * Adds a bounded group of assets through one server-validated operation. A
 * fresh overlap check happens immediately before the write, so
 * `acknowledgeOverlaps` must only be set after explicit confirmation.
 */
export async function addRingfenceItems(
  id: number,
  assetIds: string[],
  acknowledgeOverlaps = false,
): Promise<RingfenceItemBatchResult> {
  const response = await api.post<RingfenceItemBatchResult>(`/api/ringfence/${id}/items/batch`, {
    assetIds,
    acknowledgeOverlaps,
  });
  return response.data;
}
