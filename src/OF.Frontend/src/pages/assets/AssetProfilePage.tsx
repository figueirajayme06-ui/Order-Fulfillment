import { useCallback, useEffect, useMemo, useState, type FC, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router-dom";
import { Button, Spinner } from "../../components/common";
import { RecordNotesEditor } from "../../components/notes/RecordNotesEditor";
import { FrappeGantt, type GanttTask } from "../../components/timeline/FrappeGantt";
import { fetchAssetEnrichment, fetchAssetProfile } from "../../services/assetsService";
import { createAssetNote, fetchAssetNotes, saveAssetNote } from "../../services/notesService";
import type { AssetEnrichment, AssetProfile, AssetScheduleEvent } from "../../types";
import { AssetEnrichmentSection, type EnrichmentSection } from "./AssetEnrichmentSection";
import styles from "./AssetProfilePage.module.css";

type AssetProfileSection = "attributes" | "bookings" | EnrichmentSection | "details";

const enrichmentSections = new Set<EnrichmentSection>(["location", "service", "rental", "retrofits"]);
const sectionTabs: ReadonlyArray<{ section: AssetProfileSection; label: string }> = [
  { section: "attributes", label: "Attributes" },
  { section: "bookings", label: "Planning and bookings" },
  { section: "location", label: "Location and telemetry" },
  { section: "service", label: "Service history" },
  { section: "retrofits", label: "Retrofits" },
  { section: "rental", label: "Rental history" },
  { section: "details", label: "Asset details" },
];

const AssetProfilePage: FC = () => {
  const { t } = useTranslation();
  const { assetId = "" } = useParams<{ assetId: string }>();
  const initialRange = useMemo(() => createDefaultRange(), []);
  const [fromDate, setFromDate] = useState(initialRange.fromDate);
  const [toDate, setToDate] = useState(initialRange.toDate);
  const [appliedRange, setAppliedRange] = useState(initialRange);
  const [profile, setProfile] = useState<AssetProfile | null>(null);
  const [enrichment, setEnrichment] = useState<AssetEnrichment | null>(null);
  const [isEnrichmentLoading, setIsEnrichmentLoading] = useState(true);
  const [enrichmentError, setEnrichmentError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeSection, setActiveSection] = useState<AssetProfileSection>("attributes");
  const timelineTasks = useMemo(
    () => buildTimelineTasks(profile?.events ?? [], appliedRange.toDate),
    [appliedRange.toDate, profile?.events],
  );
  const loadNotes = useCallback(() => fetchAssetNotes(assetId), [assetId]);
  const createNote = useCallback((notes: string) => createAssetNote(assetId, notes), [assetId]);
  const saveNote = useCallback((noteId: number, notes: string) => saveAssetNote(assetId, noteId, notes), [assetId]);
  const loadProfile = useCallback(async () => {
    if (!assetId) {
      setError("The asset ID is missing.");
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const result = await fetchAssetProfile(assetId, appliedRange.fromDate, appliedRange.toDate);
      setProfile(result);
    } catch (requestError: unknown) {
      const status = getResponseStatus(requestError);
      setProfile(null);
      setError(
        status === 404
          ? "This asset is unavailable or you do not have access to it."
          : "Unable to load this asset profile.",
      );
    } finally {
      setIsLoading(false);
    }
  }, [appliedRange, assetId]);

  const loadEnrichment = useCallback(async () => {
    if (!assetId) return;

    setIsEnrichmentLoading(true);
    setEnrichmentError(null);
    try {
      setEnrichment(await fetchAssetEnrichment(assetId));
    } catch {
      setEnrichmentError(t("assetProfile.enrichment.error"));
    } finally {
      setIsEnrichmentLoading(false);
    }
  }, [assetId, t]);

  useEffect(() => {
    void loadProfile();
  }, [loadProfile]);

  useEffect(() => {
    setEnrichment(null);
    void loadEnrichment();
  }, [loadEnrichment]);

  useEffect(() => {
    setActiveSection("attributes");
  }, [assetId]);

  const applyDateRange = () => {
    if (toDate < fromDate) {
      setError("The end date must be on or after the start date.");
      return;
    }

    setAppliedRange({ fromDate, toDate });
  };

  if (isLoading && !profile) {
    return (
      <div className={styles.loading}>
        <Spinner />
      </div>
    );
  }

  if (!profile) {
    return (
      <section className={styles.page} aria-labelledby="asset-profile-title">
        <Link className={styles.backLink} to="/assets" data-print-hidden>
          ← Back to assets
        </Link>
        <h1 id="asset-profile-title">Asset profile</h1>
        <div className={styles.errorState} role="alert">
          <p>{error ?? "This asset could not be found."}</p>
          <Button label="Try again" variant="secondary" onClick={() => void loadProfile()} />
        </div>
      </section>
    );
  }

  const { asset, summary, events } = profile;

  return (
    <section className={styles.page} aria-labelledby="asset-profile-title">
      <Link className={styles.backLink} to="/assets" data-print-hidden>
        ← Back to assets
      </Link>

      <header className={styles.equipmentHeader}>
        <div className={styles.assetIdentity}>
          <span className={styles.assetIdentityIcon} aria-hidden="true">
            i
          </span>
          <div>
            <div className={styles.titleRow}>
              <h1 id="asset-profile-title">{asset.id}</h1>
              <span className={styles.itemTag}>{asset.itemNumber ?? asset.individualItemNumber}</span>
              <StatusPill status={asset.status} />
            </div>
            <p className={styles.description}>{asset.description ?? asset.itemNumber ?? "Asset details"}</p>
          </div>
        </div>
        <RecordNotesEditor
          key={`asset-${asset.id}`}
          recordKey={`asset-${asset.id}`}
          loadNotes={loadNotes}
          createNote={createNote}
          saveNote={saveNote}
        />
      </header>

      <div className={styles.profileWorkspace}>
        <nav className={styles.sectionRail} aria-label="Asset profile sections" role="tablist" data-print-hidden>
          {sectionTabs.map((tab) => (
            <SectionTab key={tab.section} activeSection={activeSection} {...tab} onSelect={setActiveSection} />
          ))}
        </nav>

        <div className={styles.profileContent}>
          {activeSection === "attributes" && (
            <section
              className={styles.profilePanel}
              id="asset-attributes"
              role="tabpanel"
              aria-labelledby="asset-tab-attributes"
            >
              <div className={styles.attributesPanel}>
                <h2 id="asset-attributes-title" className={styles.visuallyHidden}>
                  Asset attributes
                </h2>
                <AttributeGroup>
                  <AttributeItem label="Product group" value={asset.productGroup} />
                  <AttributeItem label="Product category" value={asset.productCategory} />
                  <AttributeItem label="Manufacturer" value={asset.manufacturerName} />
                  <AttributeItem
                    label={t("assetProfile.details.individualItem")}
                    value={asset.individualItemNumber}
                    mono
                  />
                </AttributeGroup>
                <AttributeGroup>
                  <AttributeItem label="Warehouse" value={asset.warehouse} />
                  <AttributeItem label="Current location" value={asset.warehouseLocation} />
                  <AttributeItem label="Facility" value={asset.facility} />
                  <AttributeItem label="Service centre" value={asset.serviceCenter} />
                </AttributeGroup>
                <AttributeGroup>
                  <AttributeItem label="Fleet status" value={formatStatus(asset.status)} />
                  <AttributeItem label="Current agreement" value={asset.agreementNumber} />
                  <AttributeItem label="Division" value={asset.division} />
                </AttributeGroup>
              </div>

              <div className={styles.summaryGrid}>
                <SummaryCard
                  label="Availability"
                  value={summary.availabilityStatus}
                  tone={getAvailabilityTone(summary.availabilityStatus)}
                />
                <SummaryCard
                  label="Next available"
                  value={formatDate(summary.nextAvailableDate)}
                  detail={nextAvailableDetail(summary.availabilityStatus)}
                />
                <SummaryCard
                  label="Next commitment"
                  value={formatDate(summary.nextCommitmentDate)}
                  detail={summary.nextCommitmentLabel ?? "No commitment in this period"}
                />
                <SummaryCard
                  label="Conflicts"
                  value={summary.conflictCount === 0 ? "None" : `${summary.conflictCount} to review`}
                  detail={
                    summary.conflictCount === 0
                      ? "No overlapping commitments"
                      : "Overlapping events need planner action"
                  }
                  tone={summary.conflictCount === 0 ? "success" : "danger"}
                />
              </div>
            </section>
          )}

          {activeSection === "bookings" && (
            <section
              className={styles.contentCard}
              id="asset-bookings"
              role="tabpanel"
              aria-labelledby="asset-tab-bookings"
            >
              <div className={styles.sectionHeader}>
                <div>
                  <h2 id="booking-events-title">Bookings and events</h2>
                  <p>All recorded commitments for this asset in the selected period.</p>
                </div>
                <div className={styles.scheduleControls} data-print-hidden>
                  <div className={styles.dateControls}>
                    <label>
                      <span>From</span>
                      <input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
                    </label>
                    <label>
                      <span>To</span>
                      <input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
                    </label>
                    <Button label="Apply period" variant="secondary" size="small" onClick={applyDateRange} />
                  </div>
                </div>
              </div>

              {error && (
                <p className={styles.inlineError} role="alert">
                  {error}
                </p>
              )}
              {isLoading ? (
                <div className={styles.tableLoading}>
                  <Spinner />
                </div>
              ) : events.length === 0 ? (
                <div className={styles.emptyState}>No bookings or operational events are recorded for this period.</div>
              ) : (
                <>
                  <div className={styles.scheduleTimeline} data-print-hidden>
                    <FrappeGantt tasks={timelineTasks} viewMode="Day" readonlyDates readonlyProgress />
                  </div>
                  <div className={styles.tableWrap} data-print-table="standard">
                    <table className={styles.eventsTable}>
                      <thead>
                        <tr>
                          <th>Event</th>
                          <th>Period</th>
                          <th>Agreement</th>
                          <th>Customer</th>
                          <th>Location</th>
                          <th>Source</th>
                          <th>Planner flag</th>
                        </tr>
                      </thead>
                      <tbody>
                        {events.map((event) => (
                          <EventRow event={event} key={event.id} />
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
            </section>
          )}

          {isEnrichmentSection(activeSection) && (
            <div id={`asset-${activeSection}`} role="tabpanel" aria-labelledby={`asset-tab-${activeSection}`}>
              <AssetEnrichmentSection
                enrichment={enrichment}
                isLoading={isEnrichmentLoading}
                error={enrichmentError}
                onRetry={() => void loadEnrichment()}
                section={activeSection}
              />
            </div>
          )}

          {activeSection === "details" && (
            <section
              className={`${styles.contentCard} ${styles.assetDetailsPane}`}
              id="asset-details"
              role="tabpanel"
              aria-labelledby="asset-tab-details"
            >
              <div className={styles.sectionHeader}>
                <div>
                  <h2>{t("assetProfile.details.title")}</h2>
                  <p>{t("assetProfile.details.description")}</p>
                </div>
              </div>
              <div className={styles.detailsGrid}>
                <DetailsGroup title={t("assetProfile.details.assignment")}>
                  <DetailItem label="Item number" value={asset.itemNumber} mono />
                  <DetailItem label="Warehouse location" value={asset.warehouseLocation} />
                  <DetailItem label="Facility" value={asset.facility} />
                  <DetailItem label="Service centre" value={asset.serviceCenter} />
                </DetailsGroup>
                <DetailsGroup title={t("assetProfile.details.agreement")}>
                  <DetailItem label="Agreement" value={asset.agreementNumber} />
                  <DetailItem label="Customer" value={asset.customerName} />
                  <DetailItem label="Delivery" value={formatDate(asset.deliveryDate)} />
                  <DetailItem label="Collection" value={formatDate(asset.collectionDate)} />
                  <DetailItem label="Estimated ready" value={formatDate(asset.estimatedReadyDate)} />
                  <DetailItem label="Valid to" value={formatDate(asset.agreementLineValidToDate)} />
                </DetailsGroup>
                <DetailsGroup title={t("assetProfile.details.equipment")}>
                  <DetailItem label="Manufacturer" value={asset.manufacturerName} />
                  <DetailItem label="Product group" value={asset.productGroup} />
                  <DetailItem label="Product category" value={asset.productCategory} />
                  <DetailItem
                    label="Run hours"
                    value={asset.runHours == null ? null : asset.runHours.toLocaleString()}
                  />
                </DetailsGroup>
              </div>
            </section>
          )}
        </div>
      </div>
    </section>
  );
};

function buildTimelineTasks(events: readonly AssetScheduleEvent[], periodEnd: string): GanttTask[] {
  return events.map((event) => ({
    id: event.id,
    name: `${event.type}: ${event.title}`,
    start: event.startDate.slice(0, 10),
    end: (event.endDate ?? periodEnd).slice(0, 10),
    period: { start: event.startDate, end: event.endDate },
    progress: 100,
    custom_class: getAssetProfileEventClass(event.type),
  }));
}

function EventRow({ event }: { event: AssetScheduleEvent }) {
  const agreement =
    event.agreementId && event.agreementNumber ? (
      <Link className={styles.agreementLink} to={`/agreements/${event.agreementId}`}>
        {event.agreementNumber}
      </Link>
    ) : (
      (event.agreementNumber ?? "—")
    );

  return (
    <tr className={event.hasConflict ? styles.conflictRow : undefined}>
      <td>
        <div className={styles.eventCell}>
          <span className={`${styles.eventType} ${getEventTypeClass(event.type)}`}>{event.type}</span>
          <span className={styles.eventTitle}>{event.title}</span>
        </div>
      </td>
      <td>
        <span className={styles.dateRange}>
          {formatDate(event.startDate)}
          <span aria-hidden="true"> → </span>
          {formatDate(event.endDate)}
        </span>
      </td>
      <td>{agreement}</td>
      <td>{event.customerName ?? "—"}</td>
      <td>{event.warehouse ?? "—"}</td>
      <td>
        <span className={styles.source}>{event.source}</span>
      </td>
      <td>
        {event.hasConflict ? (
          <span className={styles.conflictFlag} title={event.conflictReason ?? undefined}>
            Conflict
          </span>
        ) : (
          <span className={styles.clearFlag}>{event.status}</span>
        )}
      </td>
    </tr>
  );
}

function SummaryCard({
  label,
  value,
  detail,
  tone = "neutral",
}: {
  label: string;
  value: string;
  detail?: string;
  tone?: "neutral" | "success" | "warning" | "danger";
}) {
  return (
    <div className={`${styles.summaryCard} ${styles[`summary${capitalize(tone)}`]}`}>
      <span className={styles.summaryLabel}>{label}</span>
      <strong>{value}</strong>
      {detail && <span className={styles.summaryDetail}>{detail}</span>}
    </div>
  );
}

function StatusPill({ status }: { status: string | null }) {
  return <span className={`${styles.statusPill} ${getStatusClass(status)}`}>{formatStatus(status)}</span>;
}

function DetailsGroup({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className={styles.detailsGroup}>
      <h3>{title}</h3>
      <dl>{children}</dl>
    </section>
  );
}

function AttributeGroup({ children }: { children: ReactNode }) {
  return <dl className={styles.attributeGroup}>{children}</dl>;
}

function AttributeItem({ label, value, mono = false }: { label: string; value: string | null; mono?: boolean }) {
  return (
    <div className={styles.attributeItem}>
      <dt>{label}</dt>
      <dd className={mono ? styles.mono : undefined}>{value || "—"}</dd>
    </div>
  );
}

function SectionTab({
  activeSection,
  section,
  label,
  onSelect,
}: {
  activeSection: AssetProfileSection;
  section: AssetProfileSection;
  label: string;
  onSelect: (section: AssetProfileSection) => void;
}) {
  const isActive = activeSection === section;
  return (
    <button
      aria-controls={`asset-${section}`}
      aria-selected={isActive}
      className={isActive ? styles.activeRailLink : undefined}
      id={`asset-tab-${section}`}
      role="tab"
      tabIndex={isActive ? 0 : -1}
      type="button"
      onClick={() => onSelect(section)}
    >
      {label}
    </button>
  );
}

function isEnrichmentSection(section: AssetProfileSection): section is EnrichmentSection {
  return enrichmentSections.has(section as EnrichmentSection);
}

function DetailItem({ label, value, mono = false }: { label: string; value: string | null; mono?: boolean }) {
  return (
    <div className={styles.detailItem}>
      <dt>{label}</dt>
      <dd className={mono ? styles.mono : undefined}>{value || "—"}</dd>
    </div>
  );
}

function createDefaultRange() {
  const from = new Date();
  const to = new Date(from);
  to.setMonth(to.getMonth() + 12);
  to.setDate(to.getDate() - 1);

  return { fromDate: toDateInput(from), toDate: toDateInput(to) };
}

function toDateInput(value: Date): string {
  return value.toISOString().slice(0, 10);
}

function formatDate(value: string | null): string {
  if (!value) return "—";

  return new Date(`${value.slice(0, 10)}T00:00:00`).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

function formatStatus(status: string | null): string {
  if (!status) return "Unknown";
  return status.replace(/([a-z])([A-Z])/g, "$1 $2");
}

function getStatusClass(status: string | null): string {
  switch (status) {
    case "Available":
      return styles.statusAvailable;
    case "OnHire":
      return styles.statusOnHire;
    case "Service":
    case "Repair":
    case "Assess":
      return styles.statusWarning;
    case "RemovedStock":
    case "Scrap":
    case "Sold":
      return styles.statusDanger;
    default:
      return styles.statusNeutral;
  }
}

function getAvailabilityTone(status: string): "success" | "warning" | "danger" | "neutral" {
  if (status === "Available" || status === "Available before next commitment") return "success";
  if (status === "Committed") return "warning";
  if (status === "Out of fleet" || status === "Ready date required") return "danger";
  return "neutral";
}

function nextAvailableDetail(status: string): string | undefined {
  if (status === "Ready date required") return "Set an estimated ready date to plan this asset.";
  if (status === "Out of fleet") return "This asset is not allocatable.";
  return undefined;
}

function getEventTypeClass(type: string): string {
  switch (type) {
    case "Reservation":
      return styles.eventReservation;
    case "Ringfence":
      return styles.eventRingfence;
    case "On hire":
    case "On hold":
      return styles.eventOnHire;
    case "Service / assessment":
      return styles.eventService;
    case "Repair":
      return styles.eventRepair;
    case "Collection":
      return styles.eventCollection;
    case "In transit":
      return styles.eventMovement;
    default:
      return styles.eventNeutral;
  }
}

function getAssetProfileEventClass(type: string): string {
  switch (type) {
    case "Ringfence":
      return "event-ringfence";
    case "Reservation":
      return "event-reserved";
    case "On hire":
      return "event-onhire";
    case "On hold":
      return "event-onhold";
    case "Service / assessment":
      return "event-service";
    case "Repair":
      return "event-repair";
    case "Collection":
      return "event-collection";
    case "In transit":
      return "event-transport";
    default:
      return "event-unknown";
  }
}

function capitalize(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1);
}

function getResponseStatus(error: unknown): number | undefined {
  if (typeof error !== "object" || error == null || !("response" in error)) return undefined;
  const response = (error as { response?: { status?: number } }).response;
  return response?.status;
}

export { AssetProfilePage };
