import axios from "axios";
import api from "./api";

export interface NonSerializedStockReservationRequest {
  lineId: number;
  itemNumber: string;
  warehouse: string;
  quantity: number;
}

export interface NonSerializedStockReservationResponse {
  reservationId: number;
  itemNumber: string;
  warehouse: string;
  quantity: number;
  effectiveQuantity: number;
}

export async function reserveNonSerializedStock(
  request: NonSerializedStockReservationRequest,
): Promise<NonSerializedStockReservationResponse> {
  try {
    const response = await api.post<NonSerializedStockReservationResponse>(
      "/api/fulfilment/stock/nonserialized/reservations",
      request,
    );
    return response.data;
  } catch (error) {
    if (axios.isAxiosError<{ message?: string }>(error) && error.response?.data?.message) {
      throw new Error(error.response.data.message);
    }
    throw error;
  }
}
