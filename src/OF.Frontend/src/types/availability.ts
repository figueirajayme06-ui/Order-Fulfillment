export interface AvailabilityItem {
  warehouseCode: string;
  warehouse: string;
  facility: string;
  divisionCode: string;
  divisionName: string;
  genericCode: string;
  genericDescription: string;
  itemNumber: string;
  descriptionIntl: string;
  available: number;
  count: number;
  genericOnly: boolean;
  reservationMode: "asset" | "quantity" | "generic";
  substitutionReason?: "RELATED" | null;
}
