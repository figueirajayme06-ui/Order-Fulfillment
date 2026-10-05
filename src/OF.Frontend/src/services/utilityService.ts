import api from "./api";

export interface PullResponse {
  type: "Quote" | "Agreement";
  number: string;
}

export interface RefreshStatus {
  key: string;
  description: string;
  lastSuccessfulRunUtc: string | null;
}

export interface UserAlert {
  id: number;
  text: string;
  lineId: number;
  headerId: number;
}

export async function requestPull(number: string): Promise<PullResponse> {
  const response = await api.post<PullResponse>(`/api/pull/${encodeURIComponent(number.trim())}`);
  return response.data;
}

export async function fetchRefreshStatuses(): Promise<RefreshStatus[]> {
  const response = await api.get<RefreshStatus[]>("/api/status/refreshes");
  return response.data;
}

export async function fetchAlerts(): Promise<UserAlert[]> {
  const response = await api.get<UserAlert[]>("/api/alerts");
  return response.data;
}
