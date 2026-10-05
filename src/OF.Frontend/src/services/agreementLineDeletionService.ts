import api from "./api";
import type { ApiFulfilmentStatus } from "../types";

export interface AgreementLineDeletionResponse {
  lineId: number;
  removedReservationCount: number;
  headerStatus: ApiFulfilmentStatus;
}

export const agreementLineDeletionService = {
  async deleteLine(headerId: number, lineId: number): Promise<AgreementLineDeletionResponse> {
    const response = await api.delete<AgreementLineDeletionResponse>(`/api/agreements/${headerId}/lines/${lineId}`);
    return response.data;
  },
};
