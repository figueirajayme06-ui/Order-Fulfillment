import { useCallback, useEffect, useMemo, useRef, useState, type FC } from "react";
import { useTranslation } from "react-i18next";
import { LuArrowLeftRight } from "react-icons/lu";
import { fetchAssets } from "../../services/assetsService";
import { fetchAssetEvents } from "../../services/eventsService";
import type { Asset, AssetEvent } from "../../types";
import { Spinner } from "../common";
import { AssetCommitmentSchedule } from "./AssetCommitmentSchedule";
import type { AvailabilityCellData } from "./availabilityGrid";
import styles from "./AvailabilityPanel.module.css";
import {
  buildAvailabilityScheduleRange,
  formatAvailabilityDate,
  getAssetLocationLabel,
  getAvailabilityCommitments,
  normalizeAvailabilityEventsByAssetId,
  type AvailabilityScheduleRange,
} from "./availabilitySchedule";
import type { AvailabilityPanelProps, ExpandedCell } from "./availabilityTypes";
import { StockAllocator } from "./StockAllocator";

function isAssetAvailableForPeriod(
  asset: Asset,
  eventsByAssetId: Readonly<Record<string, readonly AssetEvent[] | undefined>>,
  range: AvailabilityScheduleRange | null,
): boolean {
  return (
    asset.status?.trim().toUpperCase() === "AVAILABLE" &&
    getAvailabilityCommitments(asset.id, eventsByAssetId, range).length === 0
  );
}

interface AvailabilityDetailsProps extends Pick<
  AvailabilityPanelProps,
  | "startDate"
  | "endDate"
  | "lineId"
  | "requiredQuantity"
  | "fulfilledQuantity"
  | "readOnly"
  | "onReserveAsset"
  | "onReserveStock"
> {
  expandedCell: ExpandedCell;
  cell: AvailabilityCellData;
  warehouseName?: string;
  displayMode: "summary" | "singleWarehouse";
  description?: string;
  relatedSubstitute?: boolean;
  onStockReserved: () => Promise<void>;
}

export const AvailabilityDetails: FC<AvailabilityDetailsProps> = ({
  expandedCell,
  cell,
  warehouseName,
  displayMode,
  description,
  relatedSubstitute,
  startDate,
  endDate,
  lineId,
  requiredQuantity = 0,
  fulfilledQuantity = 0,
  readOnly,
  onReserveAsset,
  onReserveStock,
  onStockReserved,
}) => {
  const { t, i18n } = useTranslation();
  const isSingleWarehouseView = displayMode === "singleWarehouse";
  const [cellAssets, setCellAssets] = useState<Asset[]>([]);
  const [cellAssetsLoading, setCellAssetsLoading] = useState(false);
  const [cellAssetsError, setCellAssetsError] = useState(false);
  const [cellAssetEvents, setCellAssetEvents] = useState<Record<string, AssetEvent[]>>({});
  const [cellAssetEventsLoading, setCellAssetEventsLoading] = useState(false);
  const [cellAssetEventsLoaded, setCellAssetEventsLoaded] = useState(false);
  const [cellAssetEventsError, setCellAssetEventsError] = useState(false);
  const [stockQuantity, setStockQuantity] = useState("1");
  const [stockSubmitting, setStockSubmitting] = useState(false);
  const [stockError, setStockError] = useState<string | null>(null);
  const [retry, setRetry] = useState(0);
  const assetEventRequestId = useRef(0);
  const { itemNumber, warehouseCode, divisionCode } = expandedCell;
  const scheduleRange = useMemo(() => buildAvailabilityScheduleRange(startDate, endDate), [endDate, startDate]);
  const scheduleRangeLabel = scheduleRange
    ? t("availability.schedulePeriod", {
        start: formatAvailabilityDate(scheduleRange.start, i18n.language),
        end: formatAvailabilityDate(scheduleRange.end, i18n.language),
      })
    : null;
  const loadCellAssetEvents = useCallback(
    async (assets: readonly Asset[], divisionCode: string) => {
      const requestId = ++assetEventRequestId.current;
      setCellAssetEvents({});
      setCellAssetEventsError(false);
      setCellAssetEventsLoaded(false);

      if (assets.length === 0) {
        setCellAssetEventsLoading(false);
        setCellAssetEventsLoaded(true);
        return;
      }

      setCellAssetEventsLoading(true);
      try {
        const events = await fetchAssetEvents({
          startDate: startDate || undefined,
          endDate: endDate || undefined,
          divisions: divisionCode || undefined,
          assetIds: assets.map((asset) => asset.id),
        });
        if (requestId === assetEventRequestId.current) {
          setCellAssetEvents(normalizeAvailabilityEventsByAssetId(events));
          setCellAssetEventsLoaded(true);
        }
      } catch {
        if (requestId === assetEventRequestId.current) {
          setCellAssetEvents({});
          setCellAssetEventsError(true);
        }
      } finally {
        if (requestId === assetEventRequestId.current) setCellAssetEventsLoading(false);
      }
    },
    [endDate, startDate],
  );

  useEffect(() => {
    let active = true;
    setCellAssets([]);
    setCellAssetsError(false);
    setStockError(null);
    if (cell.reservationMode === "quantity") {
      const outstanding = Math.max(0, Math.floor(requiredQuantity - fulfilledQuantity));
      setStockQuantity(String(Math.max(1, Math.min(outstanding || 1, Math.floor(cell.available)))));
      return;
    }
    setCellAssetsLoading(true);
    fetchAssets({
      itemNumber,
      warehouse: warehouseCode,
      division: divisionCode || undefined,
      excludeStatuses: "RemovedStock,Scrap,Sold",
      ...(isSingleWarehouseView ? {} : { take: 20 }),
      exactMatch: true,
    })
      .then((assets) => {
        if (!active) return;
        setCellAssets(assets);
        void loadCellAssetEvents(assets, divisionCode);
      })
      .catch(() => {
        if (active) setCellAssetsError(true);
      })
      .finally(() => {
        if (active) setCellAssetsLoading(false);
      });
    return () => {
      active = false;
      assetEventRequestId.current += 1;
    };
  }, [
    itemNumber,
    warehouseCode,
    divisionCode,
    isSingleWarehouseView,
    cell.reservationMode,
    cell.available,
    requiredQuantity,
    fulfilledQuantity,
    loadCellAssetEvents,
    retry,
  ]);
  const reserveStock = useCallback(
    async (itemNumber: string, warehouseCode: string) => {
      if (!lineId || !onReserveStock || readOnly || stockSubmitting) return;
      const quantity = Number(stockQuantity);
      if (!Number.isInteger(quantity) || quantity <= 0) {
        setStockError(t("availability.quantityPositive"));
        return;
      }

      setStockSubmitting(true);
      setStockError(null);
      try {
        await onReserveStock({ lineId, itemNumber, warehouse: warehouseCode, quantity });
        await onStockReserved();
      } catch (error) {
        setStockError(error instanceof Error && error.message ? error.message : t("availability.stockReserveError"));
      } finally {
        setStockSubmitting(false);
      }
    },
    [lineId, onStockReserved, onReserveStock, readOnly, stockQuantity, stockSubmitting, t],
  );

  const stockAllocator = (
    <StockAllocator
      stockQuantity={stockQuantity}
      maximumQuantity={Math.floor(cell.available)}
      disabled={Boolean(readOnly || !onReserveStock || stockSubmitting || Math.floor(cell.available) < 1)}
      stockSubmitting={stockSubmitting}
      stockError={stockError}
      onQuantityChange={setStockQuantity}
      onReserve={() => void reserveStock(expandedCell.itemNumber, expandedCell.warehouseCode)}
    />
  );

  if (isSingleWarehouseView) {
    const itemCells = (
      <>
        <td className={styles.mono}>
          {itemNumber}
          {relatedSubstitute && (
            <span
              className={styles.relatedSubstitution}
              title={t("availability.relatedSubstitute")}
              aria-label={t("availability.relatedSubstitute")}
            >
              <LuArrowLeftRight aria-hidden="true" />
            </span>
          )}
        </td>
        <td>{description}</td>
      </>
    );
    const feedback = cellAssetsLoading ? (
      <Spinner />
    ) : cellAssetsError ? (
      <span className={styles.assetError} role="alert">
        {t("availability.assetsError")}
        <button type="button" className={styles.retrySchedule} onClick={() => setRetry((value) => value + 1)}>
          {t("common.retry")}
        </button>
      </span>
    ) : (
      t("availability.noIndividualAssets")
    );
    if (cell.reservationMode === "quantity") {
      const isAvailable = cell.available > 0;
      return (
        <tbody>
          <tr
            className={isAvailable ? styles.stockAvailableRow : styles.stockUnavailableRow}
            data-availability={isAvailable ? "available" : "unavailable"}
          >
            <td>
              <span>{t("availability.quantityStock")}</span>
              <span
                className={`${styles.stockAvailabilityBadge} ${
                  isAvailable ? styles.stockAvailableBadge : styles.stockUnavailableBadge
                }`}
                aria-label={t(isAvailable ? "availability.available" : "availability.unavailable")}
                title={t(isAvailable ? "availability.available" : "availability.unavailable")}
              >
                <span aria-hidden="true">{isAvailable ? "✓" : "×"}</span>
              </span>
            </td>
            {itemCells}
            <td colSpan={3}>{stockAllocator}</td>
          </tr>
        </tbody>
      );
    }
    return (
      <tbody>
        {cellAssetEventsError && (
          <tr>
            <td colSpan={6}>
              <div className={styles.scheduleWarning} role="status">
                <span>
                  {itemNumber}: {t("availability.scheduleError")}
                </span>
                <button
                  type="button"
                  className={styles.retrySchedule}
                  onClick={() => void loadCellAssetEvents(cellAssets, divisionCode)}
                >
                  {t("common.retry")}
                </button>
              </div>
            </td>
          </tr>
        )}
        {cellAssetsLoading || cellAssetsError || cellAssets.length === 0 ? (
          <tr>
            <td aria-label={t("availability.assetId")}>—</td>
            {itemCells}
            <td colSpan={3}>{feedback}</td>
          </tr>
        ) : (
          cellAssets.map((asset) => {
            const availabilityKnown = cellAssetEventsLoaded && !cellAssetEventsError;
            const isAvailable = availabilityKnown && isAssetAvailableForPeriod(asset, cellAssetEvents, scheduleRange);
            const availabilityLabel = availabilityKnown
              ? t(isAvailable ? "availability.available" : "availability.unavailable")
              : t("availability.checkingAvailability");

            return (
              <tr
                key={asset.id}
                data-availability={availabilityKnown ? (isAvailable ? "available" : "unavailable") : "checking"}
                className={
                  availabilityKnown
                    ? isAvailable
                      ? styles.stockAvailableRow
                      : styles.stockUnavailableRow
                    : styles.stockAvailabilityPendingRow
                }
              >
                <td>
                  <span className={styles.mono}>{asset.id}</span>
                  <span
                    className={`${styles.stockAvailabilityBadge} ${
                      availabilityKnown
                        ? isAvailable
                          ? styles.stockAvailableBadge
                          : styles.stockUnavailableBadge
                        : styles.stockAvailabilityPendingBadge
                    }`}
                    aria-label={availabilityLabel}
                    title={availabilityLabel}
                  >
                    <span aria-hidden="true">{availabilityKnown ? (isAvailable ? "✓" : "×") : "…"}</span>
                  </span>
                  {asset.status && asset.status.trim().toUpperCase() !== "AVAILABLE" && (
                    <span className={styles.rowStatus}>{asset.status}</span>
                  )}
                </td>
                {itemCells}
                <td>{getAssetLocationLabel(asset, warehouseName, warehouseCode)}</td>
                <td>
                  <AssetCommitmentSchedule
                    assetId={asset.id}
                    eventsByAssetId={cellAssetEvents}
                    range={scheduleRange}
                    loading={cellAssetEventsLoading}
                    loaded={cellAssetEventsLoaded}
                    error={cellAssetEventsError}
                  />
                </td>
                <td>
                  {onReserveAsset && !readOnly && (
                    <button
                      type="button"
                      className={styles.reserveBtn}
                      aria-label={t("availability.reserveAsset", { asset: asset.id })}
                      onClick={() => onReserveAsset(asset)}
                    >
                      {t("fulfilment.reserve")}
                    </button>
                  )}
                </td>
              </tr>
            );
          })
        )}
      </tbody>
    );
  }

  return (
    <div className={styles.assetPanel}>
      <div className={styles.assetHeader}>
        {t(cell?.reservationMode === "quantity" ? "availability.stockAt" : "availability.assetsAt", {
          itemNumber: expandedCell.itemNumber,
          warehouse: warehouseName || expandedCell.warehouseCode,
        })}
      </div>
      {cell.reservationMode === "quantity" ? (
        stockAllocator
      ) : cellAssetsLoading ? (
        <Spinner />
      ) : cellAssetsError ? (
        <p className={styles.assetError} role="alert">
          {t("availability.assetsError")}
          <button type="button" className={styles.retrySchedule} onClick={() => setRetry((value) => value + 1)}>
            {t("common.retry")}
          </button>
        </p>
      ) : cellAssets.length === 0 ? (
        <p className={styles.emptyText}>{t("availability.noIndividualAssets")}</p>
      ) : (
        <>
          {cellAssetEventsError && (
            <div className={styles.scheduleWarning} role="status">
              <span>{t("availability.scheduleError")}</span>
              <button
                type="button"
                className={styles.retrySchedule}
                onClick={() => void loadCellAssetEvents(cellAssets, expandedCell.divisionCode)}
              >
                {t("common.retry")}
              </button>
            </div>
          )}
          <div
            className={styles.assetList}
            role="table"
            aria-label={t("availability.assetsAt", {
              itemNumber: expandedCell.itemNumber,
              warehouse: warehouseName || expandedCell.warehouseCode,
            })}
          >
            <div className={styles.assetListHeader} role="row">
              <span role="columnheader">{t("availability.assetId")}</span>
              <span role="columnheader">{t("availability.assetStatus")}</span>
              <span role="columnheader">{t("availability.assetLocation")}</span>
              <span role="columnheader">
                <span>{t("availability.commitments")}</span>
                {scheduleRangeLabel && <span className={styles.schedulePeriod}>{scheduleRangeLabel}</span>}
              </span>
              <span className={styles.assetActionHeader} aria-hidden="true" />
            </div>
            {cellAssets.map((asset) => (
              <div key={asset.id} className={styles.assetItem} role="row">
                <span className={styles.mono} role="cell">
                  {asset.id}
                </span>
                <span className={styles.assetStatus} role="cell">
                  {asset.status || "—"}
                </span>
                <span className={styles.assetLocation} role="cell">
                  {getAssetLocationLabel(asset, warehouseName, expandedCell.warehouseCode)}
                </span>
                <span className={styles.assetSchedule} role="cell">
                  <AssetCommitmentSchedule
                    assetId={asset.id}
                    eventsByAssetId={cellAssetEvents}
                    range={scheduleRange}
                    loading={cellAssetEventsLoading}
                    loaded={cellAssetEventsLoaded}
                    error={cellAssetEventsError}
                  />
                </span>
                <span className={styles.assetActionCell} role="cell">
                  {onReserveAsset && !readOnly && (
                    <button type="button" className={styles.reserveBtn} onClick={() => onReserveAsset(asset)}>
                      {t("fulfilment.reserve")}
                    </button>
                  )}
                </span>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
};
