import type { GanttTask } from "../../components/timeline/FrappeGantt";
import { timelineDate, timelineGeometry } from "../../components/timeline/timelineDates";
import { mapFulfilmentStatus, type AgreementLine, type Reservation } from "../../types";

export function buildAgreementTimelineTasks(
  lines: readonly AgreementLine[],
  reservations: readonly Reservation[],
  fallback: string,
): GanttTask[] {
  const byLine = new Map<number, Reservation[]>();
  for (const reservation of reservations) {
    const group = byLine.get(reservation.lineId) ?? [];
    group.push(reservation);
    byLine.set(reservation.lineId, group);
  }
  return lines
    .filter((line) => !line.isDeleted)
    .flatMap((line) => {
      const lineReservations = (byLine.get(line.id) ?? []).sort((a, b) => a.id - b.id);
      const period = {
        start: timelineDate(line.deliveryDate) ?? timelineDate(line.validFromDate),
        end: timelineDate(line.collectionDate) ?? timelineDate(line.terminationDate) ?? timelineDate(line.validToDate),
      };
      const geometry = timelineGeometry(period, fallback);
      const status = mapFulfilmentStatus(line.fulfilmentStatus);
      const lineTask: GanttTask = {
        id: `line-${line.id}`,
        name: `${line.agreementLineNumber ?? line.id} — ${line.itemNumber ?? "—"} (×${line.quantity})`,
        ...geometry,
        period,
        progress: Math.min(100, Math.round((line.quantityFulfilled / Math.max(line.quantity, 1)) * 100)),
        custom_class:
          status === "fulfilled"
            ? "bar-fulfilled"
            : status === "partial"
              ? "bar-partial"
              : lineReservations.length
                ? "bar-reserved"
                : "bar-unfulfilled",
      };
      return [
        lineTask,
        ...lineReservations.map((reservation): GanttTask => ({
          id: `res-${reservation.id}`,
          name: `↳ ${reservation.assetId} (${reservation.itemNumber})`,
          ...geometry,
          period,
          progress: reservation.isConfirmed ? 100 : 0,
          custom_class: reservation.isDepotFulfilled
            ? "bar-depot"
            : reservation.isRehire
              ? "bar-rehire"
              : "bar-reservation",
        })),
      ];
    });
}
