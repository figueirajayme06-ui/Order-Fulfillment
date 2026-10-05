import { isAxiosError } from "axios";
import { useCallback, useEffect, useMemo, useRef, useState, type FC } from "react";
import { useTranslation } from "react-i18next";
import { LuCalendarPlus, LuPackagePlus, LuTrash2, LuX } from "react-icons/lu";
import { useNavigate, useParams } from "react-router-dom";
import { Alert, Button, Card, Spinner } from "../../components/common";
import buttonStyles from "../../components/common/Button/Button.module.css";
import { AddEquipmentDialog } from "../../components/fulfilment/AddEquipmentDialog";
import { AssetSelector } from "../../components/fulfilment/AssetSelector";
import { AvailabilityPanel } from "../../components/fulfilment/AvailabilityPanel";
import { DepotFulfilWarehouseDialog } from "../../components/fulfilment/DepotFulfilWarehouseDialog";
import { RecordNotesEditor } from "../../components/notes/RecordNotesEditor";
import { useAuth } from "../../contexts/auth";
import { activateAgreement } from "../../services/activationService";
import { agreementLineDeletionService } from "../../services/agreementLineDeletionService";
import { unfulfilAgreement } from "../../services/agreementResetService";
import {
  createReservation,
  deleteReservation,
  fetchAgreementDetail,
  fetchReservationsForHeader,
  type AgreementDetail,
} from "../../services/agreementsService";
import { fetchAppConfiguration } from "../../services/appConfigurationService";
import { bulkDepotFulfil, bulkRehire } from "../../services/bulkActionsService";
import { reserveNonSerializedStock } from "../../services/fulfilmentService";
import { createAgreementNote, fetchAgreementNotes, saveAgreementNote } from "../../services/notesService";
import {
  ActivationStatus,
  ApiFulfilmentStatus,
  mapFulfilmentStatus,
  type AgreementLine,
  type Asset,
  type Reservation,
} from "../../types";
import {
  canAddEquipmentToLine,
  compareAgreementLines,
  groupReservationsForDisplay,
  parseLineAttributes,
  type ReservationDisplayGroup,
} from "./agreementDetailModel";
import styles from "./AgreementDetailPage.module.css";
import { ActivationBadge, formatDate, FulfilmentBadge, InfoItem } from "./AgreementDetailSummary";
import { buildOrderSummaryUrl, canActivateAgreement, canViewOrderSummary } from "./agreementHeaderActions";
import { AgreementLineRows } from "./AgreementLineRows";
import { FulfilmentStatusIcon } from "./AgreementsTable";
import {
  AVAILABILITY_INSPECTOR_MAX_SIZE,
  AVAILABILITY_INSPECTOR_MIN_SIZE,
  useAvailabilityInspectorSize,
} from "./useAvailabilityInspectorSize";

const AVAILABILITY_INSPECTOR_ID = "agreement-availability-inspector";
const AVAILABILITY_INSPECTOR_BODY_ID = "agreement-availability-inspector-body";
const AGREEMENT_LINES_TITLE_ID = "agreement-lines-title";
export const AgreementDetailPage: FC = () => {
  const { headerId } = useParams<{ headerId: string }>();
  const { t } = useTranslation();
  const { user } = useAuth();
  const canMutate = !user?.isReadOnly;
  const navigate = useNavigate();

  const [detail, setDetail] = useState<AgreementDetail | null>(null);
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedLineId, setSelectedLineId] = useState<number | null>(null);
  const [showAssetSelector, setShowAssetSelector] = useState(false);
  const [assetSelectorTrigger, setAssetSelectorTrigger] = useState<HTMLElement | null>(null);
  const [equipmentParentLine, setEquipmentParentLine] = useState<AgreementLine | null>(null);
  const [equipmentDialogTrigger, setEquipmentDialogTrigger] = useState<HTMLElement | null>(null);
  const [equipmentMessage, setEquipmentMessage] = useState<{ variant: "success" | "error"; text: string } | null>(null);
  const [deletingLineId, setDeletingLineId] = useState<number | null>(null);
  const [activationMessage, setActivationMessage] = useState<{ variant: "success" | "error"; text: string } | null>(
    null,
  );
  const [isResetting, setIsResetting] = useState(false);
  const [isActivating, setIsActivating] = useState(false);
  const mutationInFlight = useRef(false);
  const [legacyFrontendUrl, setLegacyFrontendUrl] = useState<string | null>(null);
  const [availabilityRevision, setAvailabilityRevision] = useState(0);
  const [selectedLineIds, setSelectedLineIds] = useState<Set<number>>(() => new Set());
  const [bulkAction, setBulkAction] = useState<"depotFulfil" | "rehire" | null>(null);
  const [bulkMessage, setBulkMessage] = useState<{ variant: "success" | "error"; text: string } | null>(null);
  const [showDepotFulfilWarehouseDialog, setShowDepotFulfilWarehouseDialog] = useState(false);
  const [depotFulfilWarehouseTrigger, setDepotFulfilWarehouseTrigger] = useState<HTMLElement | null>(null);
  const [expandedReservationLineId, setExpandedReservationLineId] = useState<number | null>(null);
  const [isAvailabilityCollapsed, setIsAvailabilityCollapsed] = useState(false);
  const {
    availabilityInspectorSize,
    isResizingAvailability,
    fulfilmentWorkspaceRef,
    linesPaneRef,
    handleAvailabilityResizeStart,
    handleAvailabilityResizeMove,
    handleAvailabilityResizeEnd,
    handleAvailabilityResizeKeyDown,
  } = useAvailabilityInspectorSize();
  const availabilityInspectorTitleRef = useRef<HTMLHeadingElement>(null);
  const lineAvailabilityTriggerRefs = useRef(new Map<number, HTMLButtonElement>());
  const focusInspectorOnOpenRef = useRef(false);
  const agreementId = Number(headerId);
  const loadNotes = useCallback(() => fetchAgreementNotes(agreementId), [agreementId]);
  const createNote = useCallback((notes: string) => createAgreementNote(agreementId, notes), [agreementId]);
  const saveNote = useCallback(
    (noteId: number, notes: string) => saveAgreementNote(agreementId, noteId, notes),
    [agreementId],
  );

  const reservationsByLine = useMemo(() => {
    const reservationsByLineId = new Map<number, Reservation[]>();
    for (const reservation of reservations) {
      const lineReservations = reservationsByLineId.get(reservation.lineId);
      if (lineReservations) {
        lineReservations.push(reservation);
      } else {
        reservationsByLineId.set(reservation.lineId, [reservation]);
      }
    }
    return new Map(
      Array.from(reservationsByLineId, ([lineId, lineReservations]) => [
        lineId,
        groupReservationsForDisplay(lineReservations),
      ]),
    );
  }, [reservations]);

  const loadData = useCallback(
    async (options?: { silent?: boolean }) => {
      if (!headerId) return;
      const silent = options?.silent === true;

      if (!silent) {
        setIsLoading(true);
      }

      setError(null);
      try {
        const agreementData = await fetchAgreementDetail(Number(headerId));
        const reservationData = await fetchReservationsForHeader(Number(headerId));
        setDetail(agreementData);
        setReservations(reservationData);
        if (silent) setAvailabilityRevision((revision) => revision + 1);
      } catch {
        setError(t("common.error"));
      } finally {
        if (!silent) {
          setIsLoading(false);
        }
      }
    },
    [headerId, t],
  );

  useEffect(() => {
    let active = true;
    fetchAppConfiguration()
      .then((configuration) => {
        if (active) setLegacyFrontendUrl(configuration.legacyFrontendUrl);
      })
      .catch(() => {
        if (active) setLegacyFrontendUrl(null);
      });
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  useEffect(() => {
    setExpandedReservationLineId(null);
    setSelectedLineId(null);
    setIsAvailabilityCollapsed(false);
    setEquipmentParentLine(null);
    setEquipmentMessage(null);
  }, [headerId]);

  useEffect(() => {
    if (expandedReservationLineId === null) return;

    const lineStillExists = detail?.lines.some((line) => line.id === expandedReservationLineId) ?? false;
    const reservationCount = reservationsByLine.get(expandedReservationLineId)?.length ?? 0;

    if (!lineStillExists || reservationCount === 0) {
      setExpandedReservationLineId(null);
    }
  }, [detail, expandedReservationLineId, reservationsByLine]);

  useEffect(() => {
    if (selectedLineId === null || !detail) return;
    if (!detail.lines.some((line) => line.id === selectedLineId)) {
      setSelectedLineId(null);
      setIsAvailabilityCollapsed(false);
    }
  }, [detail, selectedLineId]);

  useEffect(() => {
    if (selectedLineId === null) return;

    const frame = window.requestAnimationFrame(() => {
      lineAvailabilityTriggerRefs.current.get(selectedLineId)?.scrollIntoView?.({ block: "nearest" });
      if (focusInspectorOnOpenRef.current) {
        focusInspectorOnOpenRef.current = false;
        availabilityInspectorTitleRef.current?.focus({ preventScroll: true });
      }
    });
    return () => window.cancelAnimationFrame(frame);
  }, [isAvailabilityCollapsed, selectedLineId]);

  const handleLineAvailabilityToggle = (lineId: number) => {
    if (selectedLineId === lineId && isAvailabilityCollapsed) {
      setIsAvailabilityCollapsed(false);
      return;
    }

    const nextLineId = selectedLineId === lineId ? null : lineId;
    if (nextLineId !== selectedLineId) setExpandedReservationLineId(null);
    setSelectedLineId(nextLineId);
    setIsAvailabilityCollapsed(false);
  };

  const handleLineAvailabilityButton = (lineId: number) => {
    focusInspectorOnOpenRef.current = selectedLineId !== lineId || isAvailabilityCollapsed;
    handleLineAvailabilityToggle(lineId);
  };

  const handleAvailabilityClose = () => {
    const lineIdToRefocus = selectedLineId;
    setSelectedLineId(null);
    setIsAvailabilityCollapsed(false);
    if (lineIdToRefocus !== null) {
      window.requestAnimationFrame(() => lineAvailabilityTriggerRefs.current.get(lineIdToRefocus)?.focus());
    }
  };

  if (isLoading) {
    return (
      <div className={styles.loadingContainer}>
        <Spinner />
      </div>
    );
  }

  if (error || !detail) {
    return <div className={styles.errorContainer}>{error ?? "Agreement not found"}</div>;
  }

  const { header, lines: detailLines } = detail;
  const lines = [...detailLines].sort(compareAgreementLines);
  const selectedLine = selectedLineId === null ? undefined : lines.find((line) => line.id === selectedLineId);
  const selectedLineAttributes = parseLineAttributes(selectedLine?.attributes);
  const selectedLineReservations = selectedLine
    ? [...(reservationsByLine.get(selectedLine.id) ?? [])].sort(
        (left, right) => left.reservations[0].id - right.reservations[0].id,
      )
    : [];
  const isSelectedReservationListExpanded = selectedLine !== undefined && expandedReservationLineId === selectedLine.id;
  const displayedSelectedLineReservations = isSelectedReservationListExpanded
    ? selectedLineReservations
    : selectedLineReservations.slice(0, 2);
  const additionalSelectedLineReservationCount = Math.max(0, selectedLineReservations.length - 2);
  const selectedLineReservationListId = selectedLine ? `line-${selectedLine.id}-availability-reservations` : undefined;
  const destructiveBusy = isResetting || deletingLineId !== null;
  const actionsBusy = destructiveBusy || isActivating || bulkAction !== null;
  const resetEnabled =
    canMutate &&
    !header.isDeleted &&
    header.activationStatus === ActivationStatus.TODO &&
    !header.activationInstanceId &&
    lines.every((line) => line.activationStatus === ActivationStatus.TODO && !line.activationInstanceId) &&
    reservations.every(
      (reservation) =>
        !reservation.isConfirmed &&
        !reservation.actualAssetId &&
        !reservation.actualItemNumber &&
        reservation.actualQuantity == null,
    );
  const deletionEnabled =
    canMutate &&
    !header.isDeleted &&
    /^T/i.test(header.agreementNumber?.trim() ?? "") &&
    header.activationStatus === ActivationStatus.TODO &&
    !header.activationInstanceId;
  const canDeleteLine = (line: AgreementLine) =>
    deletionEnabled &&
    !line.isDeleted &&
    line.activationStatus === ActivationStatus.TODO &&
    !line.activationInstanceId &&
    lines.filter((candidate) => !candidate.isDeleted).length > 1 &&
    !lines.some(
      (candidate) =>
        !candidate.isDeleted &&
        candidate.id !== line.id &&
        line.agreementLineNumber &&
        candidate.agreementLineNumber?.toUpperCase().startsWith(`${line.agreementLineNumber.toUpperCase()}.`),
    ) &&
    !reservations.some(
      (reservation) =>
        reservation.lineId === line.id &&
        (reservation.isConfirmed ||
          reservation.actualAssetId ||
          reservation.actualItemNumber ||
          reservation.actualQuantity != null),
    );

  const headerStatusKind = mapFulfilmentStatus(header.fulfilmentStatus);

  const activationEnabled = canMutate && canActivateAgreement(header, lines);
  const summaryEligible = canViewOrderSummary(header);
  const summaryUrl = buildOrderSummaryUrl(legacyFrontendUrl, header.agreementNumber, header.quotePublicId);

  const handleActivate = async () => {
    if (!activationEnabled || mutationInFlight.current || actionsBusy) return;
    mutationInFlight.current = true;
    setIsActivating(true);
    setActivationMessage(null);
    try {
      await activateAgreement(Number(headerId));
      setActivationMessage({ variant: "success", text: t("agreementActions.activationSuccess") });
      await loadData({ silent: true });
    } catch {
      setActivationMessage({ variant: "error", text: t("agreementActions.activationError") });
    } finally {
      mutationInFlight.current = false;
      setIsActivating(false);
    }
  };

  const toggleLineSelection = (lineId: number) => {
    setSelectedLineIds((current) => {
      const next = new Set(current);
      if (next.has(lineId)) {
        next.delete(lineId);
      } else {
        next.add(lineId);
      }
      return next;
    });
    setBulkMessage(null);
  };

  const openAssetSelector = (lineId: number, trigger: HTMLElement) => {
    setExpandedReservationLineId(null);
    setSelectedLineId(lineId);
    setIsAvailabilityCollapsed(false);
    setAssetSelectorTrigger(trigger);
    setShowAssetSelector(true);
  };

  const openEquipmentDialog = (line: AgreementLine, trigger: HTMLElement) => {
    setEquipmentMessage(null);
    setEquipmentDialogTrigger(trigger);
    setEquipmentParentLine(line);
  };

  const allLinesSelected = lines.length > 0 && selectedLineIds.size === lines.length;
  const someLinesSelected = selectedLineIds.size > 0 && !allLinesSelected;
  const selectedUnfulfilledLineIds = lines
    .filter((line) => selectedLineIds.has(line.id) && line.fulfilmentStatus === ApiFulfilmentStatus.Unfulfilled)
    .map((line) => line.id);

  const toggleAllLines = () => {
    setSelectedLineIds(allLinesSelected ? new Set() : new Set(lines.map((line) => line.id)));
    setBulkMessage(null);
  };

  const handleBulkAction = async (action: "depotFulfil" | "rehire", warehouse?: string) => {
    if (!canMutate || selectedLineIds.size === 0 || actionsBusy || mutationInFlight.current) return;

    const lineIds = action === "depotFulfil" ? selectedUnfulfilledLineIds : Array.from(selectedLineIds);
    if (lineIds.length === 0) return;

    setBulkAction(action);
    setBulkMessage(null);
    try {
      const request = {
        headerId: Number(headerId),
        lineIds,
        includeAlreadyFulfilled: false,
        ...(warehouse ? { warehouse } : {}),
      };
      const result = action === "depotFulfil" ? await bulkDepotFulfil(request) : await bulkRehire(request);

      setSelectedLineIds(new Set());
      setBulkMessage({
        variant: "success",
        text: t("fulfilment.bulkActionSuccess", { count: result.processed }),
      });
      await loadData({ silent: true });
    } catch {
      setBulkMessage({ variant: "error", text: t("fulfilment.bulkActionError") });
    } finally {
      setBulkAction(null);
    }
  };

  const openDepotFulfilWarehouseDialog = (trigger: HTMLElement) => {
    if (selectedUnfulfilledLineIds.length === 0 || actionsBusy) return;
    setDepotFulfilWarehouseTrigger(trigger);
    setShowDepotFulfilWarehouseDialog(true);
  };

  const handleRemoveReservationGroup = async (group: ReservationDisplayGroup) => {
    if (!canMutate || actionsBusy || mutationInFlight.current) return;
    const confirmed = window.confirm(t("fulfilment.confirmRemoveReservation", { identifier: group.identifier }));
    if (!confirmed) return;

    for (const reservation of group.reservations) {
      await deleteReservation(reservation.id);
    }
    await loadData({ silent: true });
  };

  const handleReset = async () => {
    if (!resetEnabled || mutationInFlight.current || actionsBusy) return;
    if (
      !window.confirm(
        t("agreementActions.confirmReset", {
          agreement: header.agreementNumber ?? header.id,
          count: reservations.length,
        }),
      )
    )
      return;
    mutationInFlight.current = true;
    setIsResetting(true);
    setBulkMessage(null);
    try {
      const result = await unfulfilAgreement(header.id);
      setSelectedLineIds(new Set());
      setExpandedReservationLineId(null);
      setShowAssetSelector(false);
      setEquipmentParentLine(null);
      await loadData({ silent: true });
      setBulkMessage({
        variant: "success",
        text: t("agreementActions.resetSuccess", { count: result.reservationsRemoved }),
      });
    } catch (error) {
      const code = isAxiosError(error) ? error.response?.data?.code : null;
      const key =
        code === "activation_conflict"
          ? "resetUnavailable"
          : code === "confirmed_reservations"
            ? "externalReservations"
            : code === "agreement_changed"
              ? "refreshConflict"
              : "resetError";
      await loadData({ silent: true });
      setBulkMessage({ variant: "error", text: t(`agreementActions.${key}`) });
    } finally {
      mutationInFlight.current = false;
      setIsResetting(false);
    }
  };

  const handleDeleteLine = async (line: AgreementLine) => {
    if (!canDeleteLine(line) || mutationInFlight.current || actionsBusy) return;
    const lineNumber = line.agreementLineNumber ?? String(line.id);
    const count = reservations.filter((reservation) => reservation.lineId === line.id).length;
    if (!window.confirm(t("agreementActions.confirmDelete", { line: lineNumber, count }))) return;
    mutationInFlight.current = true;
    setDeletingLineId(line.id);
    setEquipmentMessage(null);
    try {
      await agreementLineDeletionService.deleteLine(header.id, line.id);
      if (selectedLineId === line.id) setSelectedLineId(null);
      setSelectedLineIds((current) => {
        const next = new Set(current);
        next.delete(line.id);
        return next;
      });
      await loadData({ silent: true });
      setEquipmentMessage({ variant: "success", text: t("agreementActions.deleteSuccess", { line: lineNumber }) });
      const nextLine = lines.find((candidate) => candidate.id !== line.id && !candidate.isDeleted);
      window.requestAnimationFrame(() => {
        if (nextLine) lineAvailabilityTriggerRefs.current.get(nextLine.id)?.focus();
      });
    } catch (error) {
      const code = isAxiosError(error) ? error.response?.data?.code : null;
      const key =
        code === "last_line" || code === "has_children"
          ? "deleteUnavailable"
          : code === "confirmed_reservations"
            ? "externalReservations"
            : code === "concurrent_change"
              ? "refreshConflict"
              : "deleteError";
      await loadData({ silent: true });
      setEquipmentMessage({ variant: "error", text: t(`agreementActions.${key}`) });
    } finally {
      mutationInFlight.current = false;
      setDeletingLineId(null);
    }
  };

  return (
    <div className={styles.page}>
      <div className={styles.topBar}>
        <div className={styles.topLeft}>
          <span data-print-hidden>
            <Button label="← Back" variant="ghost" onClick={() => navigate("/agreements")} />
          </span>
          <h1 className={styles.title}>{header.agreementNumber ?? `Agreement #${header.id}`}</h1>
        </div>
        <div className={styles.topActions} data-print-hidden>
          <Button label="Timeline" variant="secondary" onClick={() => navigate(`/agreements/${headerId}/timeline`)} />
          <RecordNotesEditor
            key={`agreement-${header.id}`}
            recordKey={`agreement-${header.id}`}
            loadNotes={loadNotes}
            createNote={createNote}
            saveNote={saveNote}
          />
          {summaryEligible &&
            (summaryUrl ? (
              <a
                className={`${buttonStyles.button} ${buttonStyles.secondary} ${buttonStyles.medium}`}
                href={summaryUrl}
                target="_blank"
                rel="noopener noreferrer"
              >
                {t("agreementActions.orderSummary")}
              </a>
            ) : (
              <Button label={t("agreementActions.orderSummary")} variant="secondary" disabled />
            ))}
          {canMutate && (
            <Button
              label={isActivating ? t("agreementActions.activating") : t("fulfilment.activate")}
              onClick={handleActivate}
              disabled={!activationEnabled || actionsBusy}
            />
          )}
        </div>
      </div>

      {activationMessage && (
        <div data-print-hidden>
          <Alert variant={activationMessage.variant} onDismiss={() => setActivationMessage(null)}>
            {activationMessage.text}
          </Alert>
        </div>
      )}

      {/* Header Info */}
      <Card bodyClassName={styles.headerCardBody}>
        <div className={styles.headerGrid}>
          <InfoItem
            label={t("agreements.customerName")}
            value={<span className={styles.primaryInfoValue}>{header.customerName ?? "—"}</span>}
          />
          <InfoItem label="Opportunity" value={header.opportunityName ?? header.opportunityNumber ?? "—"} />
          <InfoItem label={t("agreements.division")} value={header.division} />
          <InfoItem label={t("agreements.facility")} value={header.facility} />
          <InfoItem label={t("agreements.onHireDate")} value={formatDate(header.onHireDate)} />
          <InfoItem label={t("agreements.offHireDate")} value={formatDate(header.offHireDate)} />
          <InfoItem
            label="Agreement state"
            value={
              <div className={styles.agreementStateBadges}>
                <FulfilmentBadge status={header.fulfilmentStatus} />
                <ActivationBadge status={header.activationStatus} readyToActivate={headerStatusKind === "fulfilled"} />
              </div>
            }
          />
        </div>
      </Card>

      <div
        ref={fulfilmentWorkspaceRef}
        className={`${styles.fulfilmentWorkspace} ${selectedLine ? styles.fulfilmentWorkspaceSplit : ""}`}
      >
        {/* Lines Table */}
        <div
          ref={linesPaneRef}
          className={`${styles.section} ${styles.linesPane} ${selectedLine ? styles.linesPaneSplit : ""}`}
          style={selectedLine && !isAvailabilityCollapsed ? { flex: `0 0 ${availabilityInspectorSize}px` } : undefined}
        >
          <div className={styles.sectionHeader}>
            <div className={styles.sectionTitle}>
              <h2 id={AGREEMENT_LINES_TITLE_ID}>
                {t("fulfilment.lines")} ({lines.length})
              </h2>

              {canMutate && (
                <span className={styles.selectionCount} aria-live="polite">
                  {t("fulfilment.linesSelected", { count: selectedLineIds.size })}
                </span>
              )}
            </div>
            {canMutate && (
              <div className={styles.bulkActions} data-print-hidden>
                <Button
                  label={isResetting ? t("agreementActions.resetting") : t("agreementActions.reset")}
                  variant="danger"
                  size="small"
                  disabled={!resetEnabled || actionsBusy}
                  onClick={() => void handleReset()}
                />
                <div className={styles.depotFulfilActions}>
                  <Button
                    label={t("fulfilment.depotFulfil")}
                    variant="secondary"
                    size="small"
                    disabled={selectedUnfulfilledLineIds.length === 0 || actionsBusy}
                    onClick={() => void handleBulkAction("depotFulfil")}
                  />
                  <details className={styles.depotFulfilOptions}>
                    <summary aria-label={t("fulfilment.depotFulfilOptions")}>
                      <span aria-hidden="true" />
                    </summary>
                    <div className={styles.depotFulfilMenu}>
                      <button
                        type="button"
                        disabled={selectedUnfulfilledLineIds.length === 0 || actionsBusy}
                        onClick={(event) => openDepotFulfilWarehouseDialog(event.currentTarget)}
                      >
                        {t("fulfilment.depotFulfilFromWarehouse")}
                      </button>
                    </div>
                  </details>
                </div>
                <Button
                  label={t("fulfilment.rehire")}
                  variant="secondary"
                  size="small"
                  disabled={selectedLineIds.size === 0 || actionsBusy}
                  onClick={() => void handleBulkAction("rehire")}
                />
              </div>
            )}
          </div>
          {bulkAction && (
            <div className={styles.bulkProgress} role="status" data-print-hidden>
              <span aria-hidden="true">
                <Spinner size="small" />
              </span>
              <span>{t("fulfilment.bulkActionInProgress")}</span>
            </div>
          )}
          {bulkMessage && (
            <div data-print-hidden>
              <Alert variant={bulkMessage.variant} onDismiss={() => setBulkMessage(null)}>
                {bulkMessage.text}
              </Alert>
            </div>
          )}
          {equipmentMessage && (
            <div data-print-hidden>
              <Alert variant={equipmentMessage.variant} onDismiss={() => setEquipmentMessage(null)}>
                {equipmentMessage.text}
              </Alert>
            </div>
          )}
          {selectedLine && (
            <div className={styles.lineQueue} role="list" aria-label={t("availability.lineQueue")}>
              {canMutate && (
                <div className={styles.lineQueueSelectionBar} data-print-hidden>
                  <input
                    type="checkbox"
                    className={styles.lineQueueSelectionCheckbox}
                    checked={allLinesSelected}
                    ref={(input) => {
                      if (input) input.indeterminate = someLinesSelected;
                    }}
                    onChange={toggleAllLines}
                    aria-label={t("fulfilment.selectAllLines")}
                  />
                  <span>{t("fulfilment.selectAllLines")}</span>
                  <span className={styles.lineQueueSelectionCount} aria-live="polite">
                    {t("fulfilment.linesSelected", { count: selectedLineIds.size })}
                  </span>
                </div>
              )}
              <div className={styles.lineQueueColumnHeader} aria-hidden="true">
                <span />
                <span />
                <span>{t("availability.queueLineItem")}</span>
                <span>{t("availability.queueQuantityLabel")}</span>
                <span>{t("fulfilment.reservations")}</span>
                <span>{t("availability.queueWarehouseLabel")}</span>
                <span>{t("common.actions")}</span>
              </div>
              {lines.map((line) => {
                const lineNumber = line.agreementLineNumber ?? line.id;
                const itemNumber = line.itemNumber ?? line.genericItemNumber ?? "—";
                const lineReservations = reservationsByLine.get(line.id) ?? [];
                const displayedLineReservations = lineReservations.slice(0, 1);
                const additionalLineReservationCount = Math.max(0, lineReservations.length - 1);
                const reservationWarehouses = Array.from(
                  new Set(lineReservations.map((reservationGroup) => reservationGroup.warehouse).filter(Boolean)),
                ).join(", ");
                const isCurrentLine = selectedLine.id === line.id;
                const canAddEquipment = canMutate && canAddEquipmentToLine(header, line);

                return (
                  <div
                    key={line.id}
                    role="listitem"
                    className={`${styles.lineQueueItem} ${isCurrentLine ? styles.lineQueueItemCurrent : ""}`}
                  >
                    <input
                      type="checkbox"
                      className={styles.lineQueueSelectionCheckbox}
                      checked={selectedLineIds.has(line.id)}
                      onChange={() => toggleLineSelection(line.id)}
                      aria-label={t("fulfilment.selectLine", { lineNumber })}
                    />
                    <button
                      type="button"
                      className={`${styles.lineQueueButton} ${isCurrentLine ? styles.lineQueueButtonCurrent : ""}`}
                      ref={(trigger) => {
                        if (trigger) {
                          lineAvailabilityTriggerRefs.current.set(line.id, trigger);
                        } else {
                          lineAvailabilityTriggerRefs.current.delete(line.id);
                        }
                      }}
                      aria-current={isCurrentLine ? "true" : undefined}
                      aria-label={t("availability.selectLineForAvailability", { lineNumber, itemNumber })}
                      onClick={() => handleLineAvailabilityButton(line.id)}
                    >
                      <span className={styles.lineQueueStatus}>
                        <FulfilmentStatusIcon status={line.fulfilmentStatus} />
                      </span>
                      <span className={styles.lineQueueIdentity}>
                        <strong>{lineNumber}</strong>
                        <span className={`${styles.lineQueueItemNumber} ${styles.mono}`}>{itemNumber}</span>
                      </span>
                      <span className={styles.lineQueueScalar}>{line.quantity}</span>
                      <span
                        className={styles.lineQueueReservationIdentifiers}
                        title={lineReservations
                          .map((reservationGroup) =>
                            reservationGroup.isQuantityManaged
                              ? reservationGroup.reservations[0].itemNumber || reservationGroup.identifier
                              : reservationGroup.identifier,
                          )
                          .join(", ")}
                      >
                        {displayedLineReservations.length === 0 ? (
                          <span>—</span>
                        ) : (
                          displayedLineReservations.map((reservationGroup) => {
                            const reservationIdentifier = reservationGroup.isQuantityManaged
                              ? reservationGroup.reservations[0].itemNumber || reservationGroup.identifier
                              : reservationGroup.identifier;

                            return (
                              <span
                                key={reservationGroup.key}
                                className={`${styles.lineQueueReservationIdentifier} ${styles.mono}`}
                              >
                                {reservationIdentifier}
                              </span>
                            );
                          })
                        )}
                        {additionalLineReservationCount > 0 && (
                          <span className={styles.lineQueueReservationOverflow}>+{additionalLineReservationCount}</span>
                        )}
                      </span>
                      <span className={styles.lineQueueScalar} title={reservationWarehouses || undefined}>
                        {reservationWarehouses || "—"}
                      </span>
                    </button>
                    {canMutate && (
                      <div className={styles.lineQueueActions} data-print-hidden>
                        <Button
                          disabled={actionsBusy}
                          label={t("fulfilment.reserve")}
                          variant="secondary"
                          size="small"
                          icon={<LuCalendarPlus aria-hidden="true" />}
                          className={styles.lineActionReserve}
                          iconOnly
                          onClick={(event) => {
                            openAssetSelector(line.id, event.currentTarget);
                          }}
                        />
                        <Button
                          disabled={actionsBusy || !canAddEquipment}
                          label={t("equipment.add")}
                          ariaLabel={t("equipment.addForLine", { line: lineNumber })}
                          variant="secondary"
                          size="small"
                          icon={<LuPackagePlus aria-hidden="true" />}
                          className={styles.lineActionEquipment}
                          iconOnly
                          onClick={(event) => {
                            openEquipmentDialog(line, event.currentTarget);
                          }}
                        />
                        <Button
                          label={
                            deletingLineId === line.id
                              ? t("agreementActions.deleting")
                              : t("agreementActions.deleteLine")
                          }
                          ariaLabel={t("agreementActions.deleteLineFor", { line: lineNumber })}
                          variant="dangerSecondary"
                          size="small"
                          icon={<LuTrash2 aria-hidden="true" />}
                          iconOnly
                          disabled={actionsBusy || !canDeleteLine(line)}
                          onClick={() => void handleDeleteLine(line)}
                        />
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
          <div
            className={styles.tableContainer}
            role="region"
            aria-labelledby={AGREEMENT_LINES_TITLE_ID}
            aria-hidden={selectedLine ? true : undefined}
            tabIndex={selectedLine ? -1 : 0}
            data-print-table="fulfilment"
          >
            <table className={styles.table}>
              <thead>
                <tr>
                  {canMutate && (
                    <th scope="col" className={styles.selectionCell} data-print-hidden>
                      <input
                        type="checkbox"
                        className={styles.selectionCheckbox}
                        checked={allLinesSelected}
                        ref={(input) => {
                          if (input) input.indeterminate = someLinesSelected;
                        }}
                        onChange={toggleAllLines}
                        aria-label={t("fulfilment.selectAllLines")}
                      />
                    </th>
                  )}
                  <th scope="col">Line #</th>
                  <th scope="col">{t("fulfilment.itemNumber")}</th>
                  <th scope="col">{t("fulfilment.quantity")}</th>
                  <th scope="col">{t("fulfilment.warehouse")}</th>
                  <th scope="col">Valid From</th>
                  <th scope="col">Valid To</th>
                  <th scope="col" className={styles.reservationsHeader}>
                    {t("fulfilment.reservations")}
                  </th>
                  <th scope="col">Line fulfilment</th>
                  {canMutate && (
                    <th scope="col" className={styles.lineActionsHeader} data-print-hidden>
                      {t("common.actions")}
                    </th>
                  )}
                </tr>
              </thead>
              <tbody>
                <AgreementLineRows
                  lines={lines}
                  header={header}
                  selectedLineId={selectedLineId}
                  reservationsByLine={reservationsByLine}
                  expandedReservationLineId={expandedReservationLineId}
                  canMutate={canMutate}
                  deletionEnabled={deletionEnabled}
                  selectedLineIds={selectedLineIds}
                  isAvailabilityCollapsed={isAvailabilityCollapsed}
                  actionsBusy={actionsBusy}
                  deletingLineId={deletingLineId}
                  handleLineAvailabilityToggle={handleLineAvailabilityToggle}
                  handleLineAvailabilityButton={handleLineAvailabilityButton}
                  toggleLineSelection={toggleLineSelection}
                  handleRemoveReservationGroup={handleRemoveReservationGroup}
                  setExpandedReservationLineId={setExpandedReservationLineId}
                  openAssetSelector={openAssetSelector}
                  openEquipmentDialog={openEquipmentDialog}
                  canDeleteLine={canDeleteLine}
                  handleDeleteLine={handleDeleteLine}
                  registerAvailabilityTrigger={(id, trigger) => {
                    if (selectedLine) return;
                    if (trigger) lineAvailabilityTriggerRefs.current.set(id, trigger);
                    else lineAvailabilityTriggerRefs.current.delete(id);
                  }}
                />
              </tbody>
            </table>
          </div>
        </div>

        {/* Availability inspector */}
        {selectedLine && (
          <>
            {!isAvailabilityCollapsed && (
              <div
                className={`${styles.availabilityResizeHandle} ${
                  isResizingAvailability ? styles.availabilityResizeHandleActive : ""
                }`}
                role="separator"
                aria-orientation="vertical"
                aria-label={t("availability.resize")}
                aria-controls={AVAILABILITY_INSPECTOR_BODY_ID}
                aria-valuemin={AVAILABILITY_INSPECTOR_MIN_SIZE}
                aria-valuemax={AVAILABILITY_INSPECTOR_MAX_SIZE}
                aria-valuenow={availabilityInspectorSize}
                aria-valuetext={t("availability.resizeValue", { value: availabilityInspectorSize })}
                tabIndex={0}
                title={t("availability.resizeHelp")}
                onPointerDown={handleAvailabilityResizeStart}
                onPointerMove={handleAvailabilityResizeMove}
                onPointerUp={handleAvailabilityResizeEnd}
                onPointerCancel={handleAvailabilityResizeEnd}
                onLostPointerCapture={handleAvailabilityResizeEnd}
                onKeyDown={handleAvailabilityResizeKeyDown}
                data-print-hidden
              />
            )}
            <section
              id={AVAILABILITY_INSPECTOR_ID}
              className={`${styles.availabilityInspector} ${
                isAvailabilityCollapsed ? styles.availabilityInspectorCollapsed : ""
              }`}
              aria-labelledby="availability-inspector-title"
              data-print-hidden
            >
              <div className={styles.availabilityInspectorHeader}>
                <div className={styles.availabilityInspectorHeading}>
                  <h2 ref={availabilityInspectorTitleRef} id="availability-inspector-title" tabIndex={-1}>
                    {t("availability.titleForLine", {
                      lineNumber: selectedLine.agreementLineNumber ?? selectedLine.id,
                      itemNumber: selectedLine.genericItemNumber ?? selectedLine.itemNumber ?? "—",
                    })}
                  </h2>
                  {selectedLineAttributes.length > 0 && (
                    <div className={styles.availabilityAttributes} aria-label={t("equipment.selectedAttributes")}>
                      <span className={styles.availabilityAttributesLabel}>{t("equipment.selectedAttributes")}</span>
                      <ul className={styles.availabilityAttributeList}>
                        {selectedLineAttributes.map((attribute) => (
                          <li className={styles.availabilityAttribute} key={attribute.name}>
                            <strong>{attribute.name}:</strong> {attribute.value}
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                  <div
                    className={styles.availabilityReservations}
                    aria-label={t("fulfilment.reservationsForLine", {
                      lineNumber: selectedLine.agreementLineNumber ?? selectedLine.id,
                    })}
                  >
                    <span className={styles.availabilityReservationsLabel}>{t("fulfilment.reservations")}</span>
                    {selectedLineReservations.length === 0 ? (
                      <span className={styles.reservationEmpty}>{t("fulfilment.noReservations")}</span>
                    ) : (
                      <div
                        className={`${styles.availabilityReservationDisclosure} ${
                          isSelectedReservationListExpanded ? styles.availabilityReservationDisclosureExpanded : ""
                        }`}
                      >
                        <ul
                          id={selectedLineReservationListId}
                          className={`${styles.availabilityReservationList} ${
                            isSelectedReservationListExpanded ? styles.availabilityReservationListExpanded : ""
                          }`}
                          tabIndex={isSelectedReservationListExpanded ? 0 : undefined}
                        >
                          {displayedSelectedLineReservations.map((reservationGroup) => (
                            <li key={reservationGroup.key} className={styles.availabilityReservationEntry}>
                              <span
                                className={`${styles.reservationAsset} ${styles.mono}`}
                                title={reservationGroup.identifier}
                              >
                                {reservationGroup.identifier}
                              </span>
                              {reservationGroup.isQuantityManaged && (
                                <span
                                  className={styles.reservationQuantityBadge}
                                  aria-label={t("fulfilment.reservationQuantity", { count: reservationGroup.quantity })}
                                >
                                  {reservationGroup.quantity}
                                </span>
                              )}
                              {isSelectedReservationListExpanded && (
                                <span className={styles.reservationMeta}>
                                  {t("fulfilment.reservationDetails", {
                                    warehouse: reservationGroup.warehouse || "—",
                                    quantity: reservationGroup.quantity,
                                  })}
                                </span>
                              )}
                              {canMutate && (
                                <button
                                  type="button"
                                  className={styles.availabilityReservationRemove}
                                  disabled={actionsBusy}
                                  aria-label={t("fulfilment.removeReservationFor", {
                                    identifier: reservationGroup.identifier,
                                  })}
                                  title={t("fulfilment.removeReservationFor", {
                                    identifier: reservationGroup.identifier,
                                  })}
                                  onClick={() => void handleRemoveReservationGroup(reservationGroup)}
                                >
                                  <LuX aria-hidden="true" />
                                </button>
                              )}
                            </li>
                          ))}
                        </ul>
                        {(isSelectedReservationListExpanded || additionalSelectedLineReservationCount > 0) && (
                          <button
                            type="button"
                            className={styles.reservationToggle}
                            aria-expanded={isSelectedReservationListExpanded}
                            aria-controls={selectedLineReservationListId}
                            aria-label={
                              isSelectedReservationListExpanded
                                ? t("fulfilment.showFewerReservationsForLine", {
                                    lineNumber: selectedLine.agreementLineNumber ?? selectedLine.id,
                                  })
                                : t("fulfilment.showMoreReservationsForLine", {
                                    count: additionalSelectedLineReservationCount,
                                    lineNumber: selectedLine.agreementLineNumber ?? selectedLine.id,
                                  })
                            }
                            onClick={() =>
                              setExpandedReservationLineId(isSelectedReservationListExpanded ? null : selectedLine.id)
                            }
                          >
                            {isSelectedReservationListExpanded
                              ? t("fulfilment.showFewerReservations")
                              : t("fulfilment.moreReservations", {
                                  count: additionalSelectedLineReservationCount,
                                })}
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                </div>
                <div className={styles.availabilityInspectorActions}>
                  <button
                    type="button"
                    className={styles.inspectorAction}
                    aria-expanded={!isAvailabilityCollapsed}
                    aria-controls={AVAILABILITY_INSPECTOR_BODY_ID}
                    onClick={() => setIsAvailabilityCollapsed((collapsed) => !collapsed)}
                  >
                    {isAvailabilityCollapsed ? t("availability.expand") : t("availability.collapse")}
                  </button>
                  <button type="button" className={styles.inspectorAction} onClick={handleAvailabilityClose}>
                    {t("availability.close")}
                  </button>
                </div>
              </div>
              <div
                id={AVAILABILITY_INSPECTOR_BODY_ID}
                className={styles.availabilityInspectorBody}
                hidden={isAvailabilityCollapsed}
              >
                {selectedLine.genericItemNumber ? (
                  <AvailabilityPanel
                    key={availabilityRevision}
                    genericCode={selectedLine.genericItemNumber}
                    itemNumber={selectedLine.itemNumber}
                    warehouse={selectedLine.warehouse}
                    division={selectedLine.division ?? header.division}
                    startDate={selectedLine.validFromDate ?? header.onHireDate}
                    endDate={selectedLine.validToDate ?? header.offHireDate}
                    attributes={selectedLine.attributes}
                    lineId={selectedLine.id}
                    requiredQuantity={selectedLine.quantity}
                    fulfilledQuantity={selectedLine.quantityFulfilled}
                    readOnly={!canMutate || actionsBusy}
                    onReserveAsset={
                      canMutate
                        ? async (asset) => {
                            if (mutationInFlight.current) return;
                            await createReservation({
                              assetId: asset.id,
                              lineId: selectedLine.id,
                              itemNumber: asset.itemNumber ?? selectedLine.itemNumber ?? "",
                              quantity: 1,
                              warehouse: asset.warehouse ?? selectedLine.warehouse,
                            });
                            await loadData({ silent: true });
                          }
                        : undefined
                    }
                    onReserveStock={
                      canMutate
                        ? async (request) => {
                            if (mutationInFlight.current) return;
                            await reserveNonSerializedStock(request);
                            await loadData({ silent: true });
                          }
                        : undefined
                    }
                  />
                ) : (
                  <p className={styles.availabilityUnavailable}>{t("availability.unavailableWithoutItem")}</p>
                )}
              </div>
            </section>
          </>
        )}
      </div>

      {showDepotFulfilWarehouseDialog && (
        <DepotFulfilWarehouseDialog
          restoreFocusTo={depotFulfilWarehouseTrigger}
          onCancel={() => setShowDepotFulfilWarehouseDialog(false)}
          onConfirm={(warehouse) => {
            setShowDepotFulfilWarehouseDialog(false);
            void handleBulkAction("depotFulfil", warehouse);
          }}
        />
      )}

      {/* Asset Selector Modal */}
      {canMutate && showAssetSelector && selectedLineId !== null && (
        <div data-print-hidden>
          <AssetSelector
            warehouse={lines.find((l) => l.id === selectedLineId)?.warehouse}
            division={detail?.header?.division}
            itemNumber={lines.find((l) => l.id === selectedLineId)?.genericItemNumber ?? undefined}
            restoreFocusTo={assetSelectorTrigger}
            onCancel={() => setShowAssetSelector(false)}
            onSelect={async (asset: Asset) => {
              const line = lines.find((l) => l.id === selectedLineId);
              if (!line) return;
              await createReservation({
                assetId: asset.id,
                lineId: selectedLineId,
                itemNumber: asset.itemNumber ?? line.itemNumber ?? "",
                quantity: 1,
                warehouse: asset.warehouse ?? line.warehouse,
              });
              await loadData({ silent: true });
              setShowAssetSelector(false);
            }}
          />
        </div>
      )}

      {canMutate && equipmentParentLine && (
        <AddEquipmentDialog
          headerId={header.id}
          parentLine={equipmentParentLine}
          restoreFocusTo={equipmentDialogTrigger}
          onCancel={() => setEquipmentParentLine(null)}
          onAdded={async () => {
            await loadData({ silent: true });
            setEquipmentParentLine(null);
            setEquipmentMessage({ variant: "success", text: t("equipment.added") });
          }}
        />
      )}
    </div>
  );
};
