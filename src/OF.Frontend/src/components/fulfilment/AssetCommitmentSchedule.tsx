import type { FC } from "react";
import { useTranslation } from "react-i18next";
import type { AssetEvent } from "../../types";
import styles from "./AvailabilityPanel.module.css";
import {
  formatAvailabilityDate,
  getAvailabilityCommitments,
  getCommitmentLabel,
  type AvailabilityCommitmentKind,
  type AvailabilityScheduleRange,
} from "./availabilitySchedule";

function getCommitmentClass(kind: AvailabilityCommitmentKind): string {
  switch (kind) {
    case "ringfence":
      return styles.commitmentRingfence;
    case "onHire":
      return styles.commitmentOnHire;
    case "reserved":
      return styles.commitmentReserved;
    case "service":
      return styles.commitmentService;
    case "repair":
      return styles.commitmentRepair;
    case "collection":
      return styles.commitmentCollection;
    case "transport":
      return styles.commitmentTransport;
    case "onHold":
      return styles.commitmentOnHold;
    default:
      return styles.commitmentOther;
  }
}

interface AssetCommitmentScheduleProps {
  assetId: string;
  eventsByAssetId: Readonly<Record<string, readonly AssetEvent[] | undefined>>;
  range: AvailabilityScheduleRange | null;
  loading: boolean;
  loaded: boolean;
  error: boolean;
}

export const AssetCommitmentSchedule: FC<AssetCommitmentScheduleProps> = ({
  assetId,
  eventsByAssetId,
  range,
  loading,
  loaded,
  error,
}) => {
  const { t, i18n } = useTranslation();

  if (error) {
    return <span aria-label={t("availability.scheduleUnavailable")}>—</span>;
  }

  if (loading || !loaded) {
    return <span className={styles.scheduleLoading}>{t("availability.loadingSchedule")}</span>;
  }

  const commitments = getAvailabilityCommitments(assetId, eventsByAssetId, range);
  if (commitments.length === 0) {
    return <span className={styles.noCommitments}>{t("availability.noCommitments")}</span>;
  }

  return (
    <div className={styles.commitmentList}>
      {commitments.map((commitment, index) => {
        const label = getCommitmentLabel(commitment.event, t("availability.commitment"));
        const dates = t("availability.commitmentDates", {
          start: formatAvailabilityDate(commitment.start, i18n.language),
          end: formatAvailabilityDate(commitment.end, i18n.language),
        });

        return (
          <div
            key={`${commitment.event.eventType ?? "event"}-${commitment.event.startDate ?? index}-${index}`}
            className={styles.commitmentItem}
          >
            <span className={styles.commitmentSummary} title={`${label}, ${dates}`}>
              <span>{label}</span>
              <span className={styles.commitmentDates}>{dates}</span>
            </span>
            {range && (
              <span className={styles.commitmentTrack} aria-hidden="true">
                <span
                  className={`${styles.commitmentBar} ${getCommitmentClass(commitment.kind)}`}
                  style={{ left: `${commitment.leftPercent}%`, width: `${commitment.widthPercent}%` }}
                />
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
};
