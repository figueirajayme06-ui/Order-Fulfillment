import type { Dispatch, SetStateAction } from "react";
import { useTranslation } from "react-i18next";
import { LuPackagePlus, LuTrash2 } from "react-icons/lu";
import { Button } from "../../components/common";
import type { AgreementDetail } from "../../services/agreementsService";
import type { AgreementLine } from "../../types";
import { canAddEquipmentToLine, isPendingEquipmentLine, type ReservationDisplayGroup } from "./agreementDetailModel";
import styles from "./AgreementDetailPage.module.css";
import { FulfilmentBadge, formatDate } from "./AgreementDetailSummary";

const AVAILABILITY_INSPECTOR_BODY_ID = "agreement-availability-inspector-body";
interface AgreementLineRowsProps {
  lines: AgreementLine[];
  header: AgreementDetail["header"];
  selectedLineId: number | null;
  reservationsByLine: ReadonlyMap<number, ReservationDisplayGroup[]>;
  expandedReservationLineId: number | null;
  canMutate: boolean;
  deletionEnabled: boolean;
  selectedLineIds: ReadonlySet<number>;
  isAvailabilityCollapsed: boolean;
  actionsBusy: boolean;
  deletingLineId: number | null;
  handleLineAvailabilityToggle: (id: number) => void;
  handleLineAvailabilityButton: (id: number) => void;
  toggleLineSelection: (id: number) => void;
  registerAvailabilityTrigger: (id: number, trigger: HTMLButtonElement | null) => void;
  handleRemoveReservationGroup: (group: ReservationDisplayGroup) => Promise<void>;
  setExpandedReservationLineId: Dispatch<SetStateAction<number | null>>;
  openAssetSelector: (lineId: number, trigger: HTMLElement) => void;
  openEquipmentDialog: (line: AgreementLine, trigger: HTMLElement) => void;
  canDeleteLine: (line: AgreementLine) => boolean;
  handleDeleteLine: (line: AgreementLine) => Promise<void>;
}

export function AgreementLineRows({
  lines,
  header,
  selectedLineId,
  reservationsByLine,
  expandedReservationLineId,
  canMutate,
  deletionEnabled,
  selectedLineIds,
  isAvailabilityCollapsed,
  actionsBusy,
  deletingLineId,
  handleLineAvailabilityToggle,
  handleLineAvailabilityButton,
  toggleLineSelection,
  registerAvailabilityTrigger,
  handleRemoveReservationGroup,
  setExpandedReservationLineId,
  openAssetSelector,
  openEquipmentDialog,
  canDeleteLine,
  handleDeleteLine,
}: AgreementLineRowsProps) {
  const { t } = useTranslation();
  return (
    <>
      {lines.map((line) => {
        const isSelected = selectedLineId === line.id;
        const lineReservations = [...(reservationsByLine.get(line.id) ?? [])].sort(
          (left, right) => left.reservations[0].id - right.reservations[0].id,
        );
        const lineNumber = line.agreementLineNumber ?? line.id;
        const additionalReservationCount = Math.max(0, lineReservations.length - 2);
        const isReservationListExpanded = expandedReservationLineId === line.id;
        const reservationListId = `line-${line.id}-reservations`;
        const displayedReservations = isReservationListExpanded ? lineReservations : lineReservations.slice(0, 2);
        const isPendingLine = isPendingEquipmentLine(line);
        const canAddEquipment = canMutate && canAddEquipmentToLine(header, line);
        const showDeleteLine = deletionEnabled;

        return (
          <tr
            key={line.id}
            className={`${styles.lineRow} ${isSelected ? styles.selected : ""} ${
              isPendingLine ? styles.pendingLineRow : ""
            }`}
            onClick={(event) => {
              const target = event.target;
              if (target instanceof Element && target.closest("button, input, select, textarea, a")) return;
              handleLineAvailabilityToggle(line.id);
            }}
          >
            {canMutate && (
              <td className={styles.selectionCell} data-print-hidden>
                <input
                  type="checkbox"
                  className={styles.selectionCheckbox}
                  checked={selectedLineIds.has(line.id)}
                  onChange={() => toggleLineSelection(line.id)}
                  onClick={(event) => event.stopPropagation()}
                  aria-label={t("fulfilment.selectLine", {
                    lineNumber,
                  })}
                />
              </td>
            )}
            <td className={`${styles.scalarCell} ${isPendingLine ? styles.pendingLineCell : ""}`}>
              <div className={styles.lineIdentity}>
                {isPendingLine && <span className={styles.pendingLineConnector} aria-hidden="true" />}
                <div className={styles.lineIdentityText}>
                  <button
                    type="button"
                    className={styles.lineAvailabilityTrigger}
                    ref={(trigger) => registerAvailabilityTrigger(line.id, trigger)}
                    aria-label={
                      isSelected && !isAvailabilityCollapsed
                        ? t("availability.hideForLine", { lineNumber })
                        : t("availability.viewForLine", { lineNumber })
                    }
                    aria-expanded={isSelected && !isAvailabilityCollapsed}
                    aria-controls={isSelected ? AVAILABILITY_INSPECTOR_BODY_ID : undefined}
                    onClick={(event) => {
                      event.stopPropagation();
                      handleLineAvailabilityButton(line.id);
                    }}
                  >
                    {lineNumber}
                  </button>
                  {isPendingLine && <span className={styles.pendingLineLabel}>{t("equipment.pending")}</span>}
                </div>
              </div>
            </td>
            <td className={`${styles.scalarCell} ${styles.mono}`}>{line.itemNumber ?? "—"}</td>
            <td className={styles.scalarCell}>{line.quantity}</td>
            <td className={styles.scalarCell}>{line.warehouse}</td>
            <td className={styles.scalarCell}>{formatDate(line.validFromDate)}</td>
            <td className={styles.scalarCell}>{formatDate(line.validToDate)}</td>
            <td className={styles.reservationCell}>
              <div
                className={`${styles.reservationCellContent} ${
                  isReservationListExpanded ? styles.reservationCellContentExpanded : ""
                }`}
              >
                {lineReservations.length === 0 ? (
                  <span className={styles.reservationEmpty}>{t("fulfilment.noReservations")}</span>
                ) : (
                  <div
                    className={`${styles.reservationDisclosure} ${
                      isReservationListExpanded ? styles.reservationDisclosureExpanded : ""
                    }`}
                  >
                    <ul
                      id={reservationListId}
                      className={`${styles.reservationList} ${
                        isReservationListExpanded ? styles.reservationListExpanded : styles.reservationListCollapsed
                      }`}
                      aria-label={t("fulfilment.reservationsForLine", { lineNumber })}
                      tabIndex={isReservationListExpanded ? 0 : undefined}
                    >
                      {displayedReservations.map((reservationGroup) => {
                        const identifier = reservationGroup.identifier;
                        return (
                          <li key={reservationGroup.key} className={styles.reservationEntry}>
                            <div className={styles.reservationIdentity}>
                              <span className={`${styles.reservationAsset} ${styles.mono}`} title={identifier}>
                                {identifier}
                              </span>
                              {isReservationListExpanded && (
                                <span className={styles.reservationMeta}>
                                  {t("fulfilment.reservationDetails", {
                                    warehouse: reservationGroup.warehouse || "—",
                                    quantity: reservationGroup.quantity,
                                  })}
                                </span>
                              )}
                            </div>
                            {reservationGroup.isQuantityManaged && (
                              <span
                                className={styles.reservationQuantityBadge}
                                aria-label={t("fulfilment.reservationQuantity", {
                                  count: reservationGroup.quantity,
                                })}
                              >
                                {reservationGroup.quantity}
                              </span>
                            )}
                            {canMutate && isReservationListExpanded && (
                              <button
                                type="button"
                                className={styles.reservationRemove}
                                disabled={actionsBusy}
                                aria-label={t("fulfilment.removeReservationFor", { identifier })}
                                onClick={(event) => {
                                  event.stopPropagation();
                                  void handleRemoveReservationGroup(reservationGroup);
                                }}
                                data-print-hidden
                              >
                                {t("fulfilment.removeReservation")}
                              </button>
                            )}
                          </li>
                        );
                      })}
                    </ul>
                    {lineReservations.length > 0 && (
                      <>
                        <button
                          type="button"
                          className={styles.reservationToggle}
                          aria-expanded={isReservationListExpanded}
                          aria-controls={reservationListId}
                          aria-label={
                            isReservationListExpanded
                              ? t("fulfilment.showFewerReservationsForLine", { lineNumber })
                              : t("fulfilment.manageReservationsForLine", { lineNumber })
                          }
                          onClick={(event) => {
                            event.stopPropagation();
                            setExpandedReservationLineId(isReservationListExpanded ? null : line.id);
                          }}
                          data-print-hidden
                        >
                          {isReservationListExpanded
                            ? t("fulfilment.showFewerReservations")
                            : additionalReservationCount > 0
                              ? t("fulfilment.moreReservations", { count: additionalReservationCount })
                              : t("fulfilment.manageReservations")}
                        </button>
                        {additionalReservationCount > 0 && (
                          <span className={styles.reservationPrintOverflow}>
                            {t("fulfilment.moreReservations", { count: additionalReservationCount })}
                          </span>
                        )}
                      </>
                    )}
                  </div>
                )}
                {canMutate && !isReservationListExpanded && (
                  <span data-print-hidden>
                    <Button
                      disabled={actionsBusy}
                      label={t("fulfilment.reserve")}
                      variant="secondary"
                      size="small"
                      onClick={(event) => {
                        event.stopPropagation();
                        openAssetSelector(line.id, event.currentTarget);
                      }}
                    />
                  </span>
                )}
              </div>
            </td>
            <td className={styles.scalarCell}>
              <FulfilmentBadge status={line.fulfilmentStatus} />
            </td>
            {canMutate && (
              <td className={styles.lineActionsCell} data-print-hidden>
                <div className={styles.lineActions}>
                  {canAddEquipment && (
                    <Button
                      disabled={actionsBusy}
                      label={t("equipment.add")}
                      ariaLabel={t("equipment.addForLine", { line: lineNumber })}
                      variant="secondary"
                      size="small"
                      icon={<LuPackagePlus aria-hidden="true" />}
                      onClick={(event) => {
                        event.stopPropagation();
                        openEquipmentDialog(line, event.currentTarget);
                      }}
                    />
                  )}
                  {showDeleteLine && (
                    <Button
                      label={
                        deletingLineId === line.id ? t("agreementActions.deleting") : t("agreementActions.deleteLine")
                      }
                      ariaLabel={t("agreementActions.deleteLineFor", { line: lineNumber })}
                      variant="dangerSecondary"
                      size="small"
                      icon={<LuTrash2 aria-hidden="true" />}
                      iconOnly
                      disabled={actionsBusy || !canDeleteLine(line)}
                      onClick={(event) => {
                        event.stopPropagation();
                        void handleDeleteLine(line);
                      }}
                    />
                  )}
                </div>
              </td>
            )}
          </tr>
        );
      })}
    </>
  );
}
