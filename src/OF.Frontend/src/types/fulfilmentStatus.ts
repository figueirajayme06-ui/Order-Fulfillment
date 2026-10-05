export enum ApiFulfilmentStatus {
  Unfulfilled = 0,
  PartiallyFulfilled = 1,
  Overfulfilled = 2,
  FullyFulfilled = 3,
}

export type FulfilmentStatusKind = "unfulfilled" | "partial" | "fulfilled" | "unknown";

export const fulfilmentStatusCodes = [
  ApiFulfilmentStatus.Unfulfilled,
  ApiFulfilmentStatus.PartiallyFulfilled,
  ApiFulfilmentStatus.FullyFulfilled,
] as const;

export function mapFulfilmentStatus(status: number | null | undefined): FulfilmentStatusKind {
  switch (status) {
    case ApiFulfilmentStatus.Unfulfilled:
      return "unfulfilled";
    case ApiFulfilmentStatus.PartiallyFulfilled:
      return "partial";
    case ApiFulfilmentStatus.FullyFulfilled:
      return "fulfilled";
    default:
      return "unknown";
  }
}

export function isFulfilmentStatusFilterValue(value: string): boolean {
  return fulfilmentStatusCodes.some((status) => String(status) === value);
}
