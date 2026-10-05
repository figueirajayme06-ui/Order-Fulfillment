import type { FC } from "react";
import { useTranslation } from "react-i18next";
import type {
  AssetEnrichment,
  AssetLocationObservation,
  AssetServiceHistoryItem,
  AssetTelemetrySummary,
} from "../../types";
import styles from "./AssetProfilePage.module.css";

export type EnrichmentSection = "location" | "service" | "rental" | "retrofits";

interface AssetEnrichmentSectionProps {
  enrichment: AssetEnrichment | null;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
  section: EnrichmentSection;
}

const AssetEnrichmentSection: FC<AssetEnrichmentSectionProps> = ({
  enrichment,
  isLoading,
  error,
  onRetry,
  section,
}) => {
  const { t } = useTranslation();
  const title = t(sectionTitleKeys[section]);

  return (
    <section className={`${styles.contentCard} ${styles.operationsCard}`} aria-labelledby="asset-operations-title">
      <div className={styles.sectionHeader}>
        <div>
          <h2 id="asset-operations-title">{title}</h2>
          <p>{t("assetProfile.enrichment.description")}</p>
        </div>
        <button className={styles.textButton} type="button" onClick={onRetry} disabled={isLoading} data-print-hidden>
          {t("assetProfile.enrichment.refresh")}
        </button>
      </div>

      {isLoading && !enrichment ? (
        <div className={styles.enrichmentStatus} role="status">
          {t("assetProfile.enrichment.loading")}
        </div>
      ) : error && !enrichment ? (
        <div className={styles.enrichmentStatus} role="alert">
          <p>{error}</p>
          <button className={styles.textButton} type="button" onClick={onRetry}>
            {t("assetProfile.enrichment.retry")}
          </button>
        </div>
      ) : enrichment ? (
        <div className={styles.enrichmentPane}>
          <EnrichmentPane section={section} enrichment={enrichment} />
        </div>
      ) : null}

      {error && enrichment && (
        <p className={styles.inlineError} role="alert">
          {error}
        </p>
      )}
    </section>
  );
};

const sectionTitleKeys: Record<EnrichmentSection, string> = {
  location: "assetProfile.location.title",
  service: "assetProfile.service.title",
  rental: "assetProfile.rentalHistory.title",
  retrofits: "assetProfile.retrofits.title",
};

const EnrichmentPane: FC<{ section: EnrichmentSection; enrichment: AssetEnrichment }> = ({ section, enrichment }) => {
  switch (section) {
    case "location":
      return <LocationAndTelemetryPanel location={enrichment.location} telemetry={enrichment.telemetry ?? null} />;
    case "service":
      return <ServiceHistory items={enrichment.serviceHistory} />;
    case "rental":
      return <RentalHistory items={enrichment.rentalHistory} />;
    case "retrofits":
      return <Retrofits items={enrichment.retrofits} />;
  }
};

const LocationAndTelemetryPanel: FC<{
  location: AssetLocationObservation | null;
  telemetry: AssetTelemetrySummary | null;
}> = ({ location, telemetry }) => {
  const { t } = useTranslation();
  if (!location && !telemetry) {
    return <div className={styles.enrichmentStatus}>{t("assetProfile.location.empty")}</div>;
  }

  const mapUrl = location ? buildMapUrl(location.latitude, location.longitude) : null;
  const mapLink = location
    ? `https://www.openstreetmap.org/?mlat=${location.latitude}&mlon=${location.longitude}#map=14/${location.latitude}/${location.longitude}`
    : null;

  return (
    <div className={styles.locationGrid}>
      <div className={styles.locationDetails}>
        <dl>
          <LocationItem
            label={t("assetProfile.telemetry.fitment")}
            value={telemetry?.fitmentStatus ?? location?.telemetryFitment ?? null}
          />
          <LocationItem
            label={t("assetProfile.telemetry.device")}
            value={
              (telemetry?.hasDeviceMapping ?? Boolean(location))
                ? t("assetProfile.telemetry.mapped")
                : t("assetProfile.telemetry.notMapped")
            }
          />
          <LocationItem
            label={t("assetProfile.telemetry.updated")}
            value={telemetry?.deviceStatusUpdatedAt ? formatDateTime(telemetry.deviceStatusUpdatedAt) : null}
          />
          {location && (
            <>
              <LocationItem
                label={t("assetProfile.location.coordinates")}
                value={`${location.latitude.toFixed(6)}, ${location.longitude.toFixed(6)}`}
                mono
              />
              <LocationItem
                label={t("assetProfile.location.quality")}
                value={formatLocationQuality(
                  location,
                  t("assetProfile.location.valid"),
                  t("assetProfile.location.unverified"),
                  t("assetProfile.location.satellites", { count: location.satelliteCount }),
                  t("assetProfile.location.accuracy", { value: location.horizontalAccuracy }),
                )}
              />
            </>
          )}
        </dl>
        <p className={styles.sourceNote}>{t("assetProfile.telemetry.timestampNote")}</p>
      </div>
      {location && mapUrl && mapLink ? (
        <div className={styles.mapWrap} data-print-hidden>
          <iframe
            className={styles.mapFrame}
            src={mapUrl}
            title={t("assetProfile.location.mapTitle")}
            loading="lazy"
            referrerPolicy="no-referrer"
          />
          <a className={styles.mapLink} href={mapLink} target="_blank" rel="noreferrer">
            {t("assetProfile.location.openMap")}
          </a>
        </div>
      ) : (
        <div className={styles.locationUnavailable}>{t("assetProfile.location.empty")}</div>
      )}
    </div>
  );
};

const ServiceHistory: FC<{ items: AssetServiceHistoryItem[] }> = ({ items }) => {
  const { t } = useTranslation();
  return (
    <div className={styles.disclosureContent}>
      {items.length === 0 ? (
        <div className={styles.enrichmentStatus}>{t("assetProfile.service.empty")}</div>
      ) : (
        <div className={styles.serviceList}>
          {items.map((item) => (
            <details className={styles.serviceItem} key={`${item.serviceOrderNumber}-${item.serviceOrderJobNumber}`}>
              <summary>
                <span>
                  <strong>{item.serviceOrderNumber}</strong>
                  <small>{item.type ?? t("assetProfile.service.unknownType")}</small>
                </span>
                <span className={styles.serviceSummaryMeta}>
                  <span>{formatDate(item.createdDate)}</span>
                  <span className={styles.source}>{item.status ?? t("assetProfile.service.unknownStatus")}</span>
                </span>
              </summary>
              <dl className={styles.serviceDetailGrid}>
                <LocationItem
                  label={t("assetProfile.service.jobNumber")}
                  value={new Intl.NumberFormat().format(item.serviceOrderJobNumber)}
                />
                <LocationItem
                  label={t("assetProfile.service.detailRows")}
                  value={new Intl.NumberFormat().format(item.detailCount)}
                />
                <LocationItem label={t("assetProfile.service.completed")} value={formatDate(item.finishedDate)} />
                <LocationItem label={t("assetProfile.service.meterReading")} value={formatNumber(item.meterReading)} />
                <LocationItem label={t("assetProfile.service.meterDate")} value={formatDate(item.meterDate)} />
                <LocationItem label={t("assetProfile.service.symptom")} value={item.errorSymptom} />
                <LocationItem label={t("assetProfile.service.cause")} value={item.errorCause} />
                <LocationItem label={t("assetProfile.service.actionCode")} value={item.action} mono />
                <LocationItem label={t("assetProfile.service.action")} value={item.actionText} />
              </dl>
            </details>
          ))}
        </div>
      )}
    </div>
  );
};

const RentalHistory: FC<{ items: AssetEnrichment["rentalHistory"] }> = ({ items }) => {
  const { t } = useTranslation();
  if (items.length === 0) return <div className={styles.enrichmentStatus}>{t("assetProfile.rentalHistory.empty")}</div>;

  return (
    <div className={styles.disclosureContent}>
      <div className={styles.tableWrap} data-print-table="standard">
        <table className={styles.eventsTable}>
          <thead>
            <tr>
              <th>{t("assetProfile.rentalHistory.agreement")}</th>
              <th>{t("assetProfile.rentalHistory.customer")}</th>
              <th>{t("assetProfile.rentalHistory.validFrom")}</th>
              <th>{t("assetProfile.rentalHistory.validTo")}</th>
              <th>{t("assetProfile.rentalHistory.terminated")}</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={`${item.agreementNumber}-${item.validFrom ?? "unknown"}`}>
                <td className={styles.mono}>{item.agreementNumber}</td>
                <td>
                  <strong>{item.customerName ?? "—"}</strong>
                  {item.customerNumber && <small className={styles.tableSecondary}>{item.customerNumber}</small>}
                </td>
                <td>{formatDate(item.validFrom)}</td>
                <td>{formatDate(item.validTo)}</td>
                <td>{formatDate(item.terminationDate)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

const Retrofits: FC<{ items: AssetEnrichment["retrofits"] }> = ({ items }) => {
  const { t } = useTranslation();
  if (items.length === 0) return <div className={styles.enrichmentStatus}>{t("assetProfile.retrofits.empty")}</div>;

  return (
    <div className={styles.disclosureContent}>
      <div className={styles.tableWrap} data-print-table="standard">
        <table className={styles.eventsTable}>
          <thead>
            <tr>
              <th>{t("assetProfile.retrofits.document")}</th>
              <th>{t("assetProfile.retrofits.description")}</th>
              <th>{t("assetProfile.retrofits.status")}</th>
              <th>{t("assetProfile.retrofits.opened")}</th>
              <th>{t("assetProfile.retrofits.completed")}</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={`${item.document}-${item.openedAt ?? "unknown"}`}>
                <td className={styles.mono}>{item.document}</td>
                <td>{item.description ?? "—"}</td>
                <td>{item.status ?? "—"}</td>
                <td>{formatDate(item.openedAt)}</td>
                <td>{formatDate(item.completedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

const LocationItem: FC<{ label: string; value: string | null; mono?: boolean }> = ({ label, value, mono }) => (
  <div className={styles.detailItem}>
    <dt>{label}</dt>
    <dd className={mono ? styles.mono : undefined}>{value || "—"}</dd>
  </div>
);

function buildMapUrl(latitude: number, longitude: number): string {
  const latitudeDelta = 0.012;
  const longitudeDelta = 0.018;
  const bbox = [
    longitude - longitudeDelta,
    latitude - latitudeDelta,
    longitude + longitudeDelta,
    latitude + latitudeDelta,
  ];
  const params = new URLSearchParams({
    bbox: bbox.join(","),
    layer: "mapnik",
    marker: `${latitude},${longitude}`,
  });
  return `https://www.openstreetmap.org/export/embed.html?${params.toString()}`;
}

function formatLocationQuality(
  location: AssetLocationObservation,
  valid: string,
  unverified: string,
  satellites: string,
  accuracy: string,
): string {
  const parts = [location.validity === 1 ? valid : unverified];
  if (location.satelliteCount != null) parts.push(satellites);
  if (location.horizontalAccuracy != null) parts.push(accuracy);
  return parts.join(" · ");
}

function formatDate(value: string | null): string {
  if (!value) return "—";
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(value));
}

function formatDateTime(value: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(value);
  if (!match) return value;
  const [, year, month, day, hour, minute] = match;
  const sourceTime = new Date(Number(year), Number(month) - 1, Number(day), Number(hour), Number(minute));
  return new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(sourceTime);
}

function formatNumber(value: number | null): string | null {
  return value == null ? null : new Intl.NumberFormat().format(value);
}

export { AssetEnrichmentSection };
