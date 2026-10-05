import type { AgreementDetail } from "../../services/agreementsService";
import { ActivationStatus, type AgreementLine, type Reservation } from "../../types";

export interface ReservationDisplayGroup {
  key: string;
  reservations: Reservation[];
  identifier: string;
  warehouse: string;
  quantity: number;
  isQuantityManaged: boolean;
}

interface LineAttributeDisplay {
  name: string;
  value: string;
}

export function parseLineAttributes(attributes: string | null | undefined): LineAttributeDisplay[] {
  if (!attributes) return [];

  return attributes
    .split(";")
    .map((attribute) => {
      const separator = attribute.indexOf(":");
      if (separator <= 0 || separator === attribute.length - 1) return null;

      const name = attribute.slice(0, separator).trim();
      const value = attribute.slice(separator + 1).trim();
      return name && value ? { name, value } : null;
    })
    .filter((attribute): attribute is LineAttributeDisplay => attribute !== null);
}

export function compareAgreementLines(
  left: AgreementDetail["lines"][number],
  right: AgreementDetail["lines"][number],
): number {
  const leftSegments = getAgreementLineSegments(left.agreementLineNumber);
  const rightSegments = getAgreementLineSegments(right.agreementLineNumber);

  if (leftSegments === null && rightSegments === null) return left.id - right.id;
  if (leftSegments === null) return 1;
  if (rightSegments === null) return -1;

  const segmentCount = Math.max(leftSegments.length, rightSegments.length);
  for (let index = 0; index < segmentCount; index += 1) {
    const leftSegment = leftSegments[index];
    const rightSegment = rightSegments[index];

    if (leftSegment === undefined) return -1;
    if (rightSegment === undefined) return 1;

    const comparison = leftSegment.localeCompare(rightSegment, undefined, {
      numeric: true,
      sensitivity: "base",
    });
    if (comparison !== 0) return comparison;
  }

  return left.id - right.id;
}

function getAgreementLineSegments(lineNumber: string | null): string[] | null {
  if (!lineNumber) return null;
  return lineNumber.split(".");
}

export function isPendingEquipmentLine(line: AgreementLine): boolean {
  const hasChildLineNumber = line.agreementLineNumber?.includes(".") === true;
  return (line.isSubline || hasChildLineNumber) && line.activationStatus === ActivationStatus.TODO && !line.isDeleted;
}

function isRootAgreementLine(line: AgreementLine): boolean {
  return !line.isSubline && line.agreementLineNumber?.includes(".") !== true;
}

function canManageEquipment(header: AgreementDetail["header"]): boolean {
  if (header.isDeleted || !header.agreementNumber) return false;

  if (/^T/i.test(header.agreementNumber)) {
    return header.activationStatus === ActivationStatus.TODO;
  }

  if (/^A/i.test(header.agreementNumber)) {
    return header.activationStatus === ActivationStatus.Activated;
  }

  return false;
}

export function canAddEquipmentToLine(header: AgreementDetail["header"], line: AgreementLine): boolean {
  if (!canManageEquipment(header) || !isRootAgreementLine(line) || line.isDeleted || !line.requiresFulfilment) {
    return false;
  }

  if (/^T/i.test(header.agreementNumber ?? "")) {
    return line.activationStatus === ActivationStatus.TODO;
  }

  return line.activationStatus === ActivationStatus.Activated;
}

export function groupReservationsForDisplay(reservations: Reservation[]): ReservationDisplayGroup[] {
  const groups = new Map<string, ReservationDisplayGroup>();

  for (const reservation of reservations) {
    const isQuantityManaged =
      reservation.assetId === reservation.itemNumber && !reservation.isDepotFulfilled && !reservation.isRehire;
    const key = isQuantityManaged
      ? `stock|${reservation.itemNumber}|${reservation.warehouse}`
      : `reservation|${reservation.id}`;
    const existing = groups.get(key);

    if (existing) {
      existing.reservations.push(reservation);
      existing.quantity += reservation.quantity;
      continue;
    }

    groups.set(key, {
      key,
      reservations: [reservation],
      identifier: isQuantityManaged
        ? `${reservation.warehouse}: ${reservation.itemNumber}`
        : reservation.actualAssetId || reservation.assetId,
      warehouse: reservation.warehouse,
      quantity: reservation.quantity,
      isQuantityManaged,
    });
  }

  return Array.from(groups.values()).sort((left, right) => left.reservations[0].id - right.reservations[0].id);
}
