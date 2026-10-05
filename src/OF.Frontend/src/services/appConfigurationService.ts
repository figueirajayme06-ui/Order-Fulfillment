import api from "./api";
import type { AppConfiguration } from "../types";

export async function fetchAppConfiguration(): Promise<AppConfiguration> {
  const response = await api.get<AppConfiguration>("/api/app-config");
  return response.data;
}
