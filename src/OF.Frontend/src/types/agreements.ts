import { type ApiFulfilmentStatus } from "./fulfilmentStatus";

export enum ActivationStatus {
  TODO = 0,
  Failed = 1,
  Requested = 2,
  Activated = 3,
}

export interface AgreementHeader {
  id: number;
  quotePublicId: string | null;
  agreementNumber: string | null;
  orderNumber: string | null;
  quoteNumber: string | null;
  customerName: string | null;
  customerNumber: string | null;
  division: string;
  facility: string;
  fulfilmentStatus: ApiFulfilmentStatus;
  activationStatus: ActivationStatus;
  activationInstanceId?: string | null;
  onHireDate: string | null;
  offHireDate: string | null;
  changeSequence: number;
  isDeleted: boolean;
  lastUpdatedBy: string | null;
  lastUpdatedDate: string | null;
  orderSource: string;
  opportunityNumber: string | null;
  opportunityName: string | null;
}

export interface AgreementLine {
  id: number;
  isSubline: boolean;
  headerId: number | null;
  itemNumber: string | null;
  genericItemNumber: string | null;
  quantity: number;
  deliveryDate: string | null;
  validFromDate: string;
  validToDate: string;
  terminationDate: string | null;
  collectionDate?: string | null;
  attributes: string | null;
  fulfilmentStatus: ApiFulfilmentStatus;
  activationStatus: ActivationStatus;
  activationInstanceId?: string | null;
  requiresFulfilment: boolean;
  isDeleted: boolean;
  changeSequence: number;
  warehouse: string;
  division: string;
  facility: string;
  orderSource: string;
  orderLineNumber: string | null;
  agreementLineNumber: string | null;
  quantityFulfilled: number;
  lastUpdatedBy: string | null;
  lastUpdatedDate: string | null;
}

export interface Reservation {
  id: number;
  assetId: string;
  lineId: number;
  itemNumber: string;
  quantity: number;
  effectiveQuantity: number;
  warehouse: string;
  isConfirmed: boolean;
  isDepotFulfilled: boolean;
  isRehire: boolean;
  actualAssetId: string | null;
  actualItemNumber: string | null;
  actualQuantity: number | null;
  notes: string | null;
  lastUpdatedBy: string | null;
  lastUpdatedDate: string | null;
}

export interface Asset {
  id: string;
  individualItemNumber: string;
  itemNumber: string | null;
  status: string | null;
  warehouse: string | null;
  division: string | null;
  facility: string | null;
  estimatedReadyDate: string | null;
  telemetryStatus: string | null;
  agreementNumber: string | null;
  customerName: string | null;
  deliveryDate: string | null;
  agreementLineValidFromDate: string | null;
  agreementLineValidToDate: string | null;
  description: string | null;
  warehouseLocation: string | null;
  collectionDate: string | null;
  terminationDate: string | null;
  daysOffHire?: number | null;
  warehouseName?: string | null;
  customerNumber?: string | null;
  productGroup?: string | null;
  productCategory?: string | null;
  runHours?: number | null;
  size?: string | null;
  remark?: string | null;
  noteCount?: number;
}

export interface AssetEvent {
  assetId: string | null;
  eventType: string | null;
  startDate: string | null;
  endDate: string | null;
  title: string | null;
  cssClass: string | null;
}

export interface Ringfence {
  id: number;
  fromDate: string;
  toDate: string;
  divisions: string;
  warehouse: string;
}
