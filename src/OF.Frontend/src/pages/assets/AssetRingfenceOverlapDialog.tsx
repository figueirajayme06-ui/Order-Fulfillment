import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../components/common";
import type { RingfenceOverlap } from "../../services/ringfenceService";
import styles from "./AssetsPage.module.css";
import type { RingfenceOverlapConfirmation } from "./useAssetRingfence";

interface AssetRingfenceOverlapDialogProps {
  overlapConfirmation: RingfenceOverlapConfirmation | null;
  isAddingToRingfence: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

export function AssetRingfenceOverlapDialog({
  overlapConfirmation,
  isAddingToRingfence,
  onCancel,
  onConfirm,
}: AssetRingfenceOverlapDialogProps) {
  const { t } = useTranslation();
  const overlapDialogRef = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = overlapDialogRef.current;
    if (!dialog) return;
    if (overlapConfirmation && !dialog.open) dialog.showModal?.();
    if (!overlapConfirmation && dialog.open) dialog.close();
  }, [overlapConfirmation]);

  return (
    <dialog
      ref={overlapDialogRef}
      className={styles.ringfenceDialog}
      aria-labelledby="asset-ringfence-overlap-title"
      onCancel={(event) => {
        event.preventDefault();
        onCancel();
      }}
    >
      <div className={styles.ringfenceDialogContent}>
        <h2 id="asset-ringfence-overlap-title">{t("assets.ringfence.overlapTitle")}</h2>
        <div className={styles.ringfenceDialogBody}>
          <p>
            {t("assets.ringfence.overlapMessage", {
              count: overlapConfirmation?.assetIds.length ?? 0,
              details: overlapConfirmation ? formatRingfenceOverlapDetails(overlapConfirmation.overlaps) : "",
            })}
          </p>
          {overlapConfirmation && (
            <ul className={styles.ringfenceOverlapList}>
              {overlapConfirmation.overlaps.map((overlap) => (
                <li key={overlap.ringfenceId}>
                  <strong>{overlap.title}</strong>
                  <span>{formatRingfenceOverlapPeriod(overlap.fromDate, overlap.toDate)}</span>
                  <span>{overlap.assetIds.join(", ")}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
        <div className={styles.ringfenceDialogActions}>
          <Button label={t("common.cancel")} variant="secondary" onClick={() => onCancel()} />
          <Button
            label={t("assets.ringfence.addDespiteOverlap")}
            onClick={() => onConfirm()}
            disabled={isAddingToRingfence}
          />
        </div>
      </div>
    </dialog>
  );
}

function formatRingfenceOverlapPeriod(fromDate: string, toDate: string): string {
  const format = (value: string) => {
    const date = new Date(`${value.slice(0, 10)}T00:00:00`);
    return Number.isNaN(date.getTime())
      ? value
      : date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  };

  return `${format(fromDate)} – ${format(toDate)}`;
}

function formatRingfenceOverlapDetails(overlaps: readonly RingfenceOverlap[]): string {
  return overlaps
    .map((overlap) => `${overlap.title} (${formatRingfenceOverlapPeriod(overlap.fromDate, overlap.toDate)})`)
    .join("; ");
}
