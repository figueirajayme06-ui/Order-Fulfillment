import api from "./api";

export async function activateAgreement(headerId: number): Promise<{ type: string; headerId: number }> {
  const response = await api.post<{ type: string; headerId: number }>(`/api/activation/${headerId}`);
  return response.data;
}

export async function cancelActivation(headerId: number): Promise<void> {
  await api.post(`/api/activation/${headerId}/cancel`);
}
