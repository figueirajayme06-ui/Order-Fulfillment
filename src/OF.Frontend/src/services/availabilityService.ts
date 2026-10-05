import api from "./api";
import type { AvailabilityItem } from "../types/availability";

export async function fetchAvailabilitySummary(
  genericCode: string,
  attributes?: string | null,
  startDate?: string | null,
  endDate?: string | null,
  division?: string | null,
  itemNumber?: string | null,
  lineId?: number | null,
): Promise<AvailabilityItem[]> {
  const params = new URLSearchParams({ genericCode });
  if (attributes) params.set("attributes", attributes);
  if (startDate) params.set("startDate", startDate);
  if (endDate) params.set("endDate", endDate);
  if (division) params.set("division", division);
  if (itemNumber) params.set("itemNumber", itemNumber);
  if (lineId != null) params.set("lineId", lineId.toString());

  const response = await api.get<AvailabilityItem[]>(`/api/availability/summary?${params.toString()}`);
  return response.data;
}
