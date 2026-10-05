import type { Asset } from "./agreements";

export interface AssetProfileAsset extends Asset {
  manufacturerName: string | null;
  productGroup: string | null;
  productCategory: string | null;
  runHours: number | null;
  serviceCenter: string | null;
}

export interface AssetScheduleSummary {
  nextAvailableDate: string | null;
  availabilityStatus: string;
  nextCommitmentDate: string | null;
  nextCommitmentLabel: string | null;
  conflictCount: number;
}

export interface AssetScheduleEvent {
  id: string;
  type: string;
  status: string;
  startDate: string;
  endDate: string | null;
  title: string;
  agreementId: number | null;
  agreementNumber: string | null;
  customerName: string | null;
  warehouse: string | null;
  source: string;
  hasConflict: boolean;
  conflictReason: string | null;
}

export interface AssetProfile {
  asset: AssetProfileAsset;
  summary: AssetScheduleSummary;
  events: AssetScheduleEvent[];
}

export interface AssetLocationObservation {
  latitude: number;
  longitude: number;
  observedAt: string;
  ingestedAt: string | null;
  validity: number | null;
  satelliteCount: number | null;
  horizontalAccuracy: number | null;
  assignedLocation: string | null;
  telemetryFitment: string;
}

export interface AssetServiceHistoryItem {
  serviceOrderNumber: string;
  serviceOrderJobNumber: number;
  status: string | null;
  createdDate: string | null;
  finishedDate: string | null;
  type: string | null;
  meterReading: number | null;
  meterDate: string | null;
  errorSymptom: string | null;
  errorCause: string | null;
  action: string | null;
  actionText: string | null;
  detailCount: number;
}

export interface AssetRetrofitItem {
  document: string;
  description: string | null;
  status: string | null;
  openedAt: string | null;
  completedAt: string | null;
  serviceOrder: string | null;
}

export interface AssetRentalHistoryItem {
  agreementNumber: string;
  customerNumber: string | null;
  customerName: string | null;
  validFrom: string | null;
  validTo: string | null;
  terminationDate: string | null;
}

export interface AssetTelemetrySummary {
  fitmentStatus: string;
  hasDeviceMapping: boolean;
  deviceStatusUpdatedAt: string | null;
  deviceStatusIngestedAt: string | null;
}

export interface AssetEnrichment {
  location: AssetLocationObservation | null;
  serviceHistory: AssetServiceHistoryItem[];
  retrofits: AssetRetrofitItem[];
  rentalHistory: AssetRentalHistoryItem[];
  telemetry?: AssetTelemetrySummary | null;
}
