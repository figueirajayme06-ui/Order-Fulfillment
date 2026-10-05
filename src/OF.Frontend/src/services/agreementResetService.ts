import api from "./api";

export interface AgreementResetResult {
  linesReset: number;
  reservationsRemoved: number;
  headerStatus: number;
}

/** Resets the complete agreement, independently of selected or filtered lines. */
export async function unfulfilAgreement(headerId: number): Promise<AgreementResetResult> {
  const response = await api.post<AgreementResetResult>(`/api/agreements/${headerId}/unfulfil`);
  return response.data;
}
