import api from "./api";

export interface BulkActionResult {
  processed: number;
}

export async function bulkDepotFulfil(data: {
  headerId: number;
  lineIds: number[];
  warehouse?: string;
  includeAlreadyFulfilled: boolean;
}): Promise<BulkActionResult> {
  const response = await api.post<BulkActionResult>("/api/bulkactions/depot-fulfil", data);
  return response.data;
}

export async function bulkRehire(data: {
  headerId: number;
  lineIds: number[];
  warehouse?: string;
  includeAlreadyFulfilled: boolean;
}): Promise<BulkActionResult> {
  const response = await api.post<BulkActionResult>("/api/bulkactions/rehire", data);
  return response.data;
}
