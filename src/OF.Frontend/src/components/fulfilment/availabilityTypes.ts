import type { Asset } from "../../types";

export interface AvailabilityPanelProps {
  genericCode: string;
  itemNumber?: string | null;
  warehouse?: string;
  division?: string;
  startDate?: string | null;
  endDate?: string | null;
  attributes?: string | null;
  lineId?: number;
  requiredQuantity?: number;
  readOnly?: boolean;
  fulfilledQuantity?: number;
  onReserveAsset?: (asset: Asset) => void;
  onReserveStock?: (request: {
    lineId: number;
    itemNumber: string;
    warehouse: string;
    quantity: number;
  }) => Promise<void>;
}

export interface ExpandedCell {
  itemNumber: string;
  warehouseCode: string;
  divisionCode: string;
}
