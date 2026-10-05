import { useTranslation } from "react-i18next";
import { Badge } from "../../components/common";
import { mapFulfilmentStatus } from "../../types";
import styles from "./AgreementDetailPage.module.css";

export function InfoItem({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className={styles.infoItem}>
      <span className={styles.infoLabel}>{label}</span>
      <div className={styles.infoValue}>{value ?? "—"}</div>
    </div>
  );
}

export function FulfilmentBadge({ status }: { status?: number }) {
  const { t } = useTranslation();

  const resolvedStatusKind = mapFulfilmentStatus(status);
  switch (resolvedStatusKind) {
    case "unfulfilled":
      return <Badge label={t("fulfilmentStatus.unfulfilled")} variant="error" />;
    case "partial":
      return <Badge label={t("fulfilmentStatus.partiallyFulfilled")} variant="warning" />;
    case "fulfilled":
      return <Badge label={t("fulfilmentStatus.fullyFulfilled")} variant="success" />;
    default:
      return <Badge label="Unknown" variant="neutral" />;
  }
}

export function ActivationBadge({ status, readyToActivate }: { status: number; readyToActivate: boolean }) {
  const { t } = useTranslation();
  switch (status) {
    case 0:
      return <Badge label={readyToActivate ? "Ready to activate" : "Not activated"} variant="neutral" />;
    case 1:
      return <Badge label={t("activationStatus.failed")} variant="error" />;
    case 2:
      return <Badge label={t("activationStatus.requested")} variant="warning" />;
    case 3:
      return <Badge label={t("activationStatus.activated")} variant="success" />;
    default:
      return <Badge label="Unknown" variant="neutral" />;
  }
}

export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return "—";
  try {
    return new Date(isoDate).toLocaleDateString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  } catch {
    return isoDate;
  }
}
