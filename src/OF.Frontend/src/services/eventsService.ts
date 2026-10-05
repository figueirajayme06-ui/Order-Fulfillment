import api from "./api";
import type { AssetEvent } from "../types";

export interface AssetEventsRequest {
  startDate?: string;
  endDate?: string;
  divisions?: string;
  assetIds?: string[];
}

interface AssetEventsResponse {
  events: Record<string, AssetEvent[]>;
}

export async function fetchAssetEvents(request: AssetEventsRequest): Promise<Record<string, AssetEvent[]>> {
  const response = await api.post<AssetEventsResponse>("/api/event/events", request);
  return response.data.events ?? {};
}
