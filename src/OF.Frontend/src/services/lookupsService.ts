import api from "./api";

export interface DivisionLookup {
  code: string;
  name: string;
}

export interface WarehouseLookup {
  warehouseCode: string;
  warehouse: string;
  facility: string;
  facilityName?: string;
  divisionCode: string;
  divisionName: string;
}

export interface UserLookup {
  loginName: string;
  fullName: string;
}

export async function fetchDivisions(): Promise<DivisionLookup[]> {
  const response = await api.get<DivisionLookup[]>("/api/lookups/divisions");
  return response.data;
}

export async function fetchWarehouses(division: string): Promise<WarehouseLookup[]> {
  const params = new URLSearchParams({ division });
  const response = await api.get<WarehouseLookup[]>(`/api/lookups/warehouses?${params.toString()}`);
  return response.data;
}

export async function fetchUsers(division: string): Promise<UserLookup[]> {
  const params = new URLSearchParams({ division });
  const response = await api.get<UserLookup[]>(`/api/lookups/users?${params.toString()}`);
  return response.data;
}
