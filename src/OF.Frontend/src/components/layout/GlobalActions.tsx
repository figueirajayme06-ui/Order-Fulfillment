import { useCallback, useEffect, useRef, useState, type FC, type FormEvent, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { LuBell, LuChevronDown, LuClock3, LuDownload, LuExternalLink, LuHeadset, LuX } from "react-icons/lu";
import { Link } from "react-router-dom";
import {
  fetchAlerts,
  fetchRefreshStatuses,
  requestPull,
  type RefreshStatus,
  type UserAlert,
} from "../../services/utilityService";
import styles from "./GlobalActions.module.css";

const NOF_SERVICE_REQUEST_URL = "https://aggreko.freshservice.com/support/catalog/items/250";
const NOF_USER_GUIDES_URL =
  "https://aggreko.sharepoint.com/:f:/r/sites/ConImp_CustomerExperienceManagement/Process%20Training%20Sessions/Master%20Training%20Decks/Order%20Fulfilment/New%20OF";

interface Props {
  appearance?: "light" | "dark";
  placement?: "toolbar" | "sidebar";
  showPull?: boolean;
  showUtilities?: boolean;
  showAlerts?: boolean;
  showHelp?: boolean;
  compact?: boolean;
}

interface ToastState {
  kind: "success" | "error";
  message: string;
}

const ALERT_POLL_INTERVAL_MS = 60_000;
const TOAST_DURATION_MS = 6_000;

export const GlobalActions: FC<Props> = ({
  appearance = "light",
  placement = "toolbar",
  showPull = true,
  showUtilities = true,
  compact = false,
  showAlerts = true,
  showHelp = false,
}) => {
  const { t } = useTranslation();
  const [number, setNumber] = useState("");
  const [isPulling, setIsPulling] = useState(false);
  const [refreshes, setRefreshes] = useState<RefreshStatus[]>([]);
  const [alerts, setAlerts] = useState<UserAlert[]>([]);
  const [refreshError, setRefreshError] = useState(false);
  const [alertsError, setAlertsError] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);
  const actionsRef = useRef<HTMLDivElement>(null);
  const toastTimer = useRef<number | undefined>(undefined);

  const loadRefreshes = useCallback(async () => {
    try {
      setRefreshes(await fetchRefreshStatuses());
      setRefreshError(false);
    } catch {
      setRefreshError(true);
    }
  }, []);

  const loadAlerts = useCallback(async () => {
    try {
      setAlerts(await fetchAlerts());
      setAlertsError(false);
    } catch {
      setAlertsError(true);
    }
  }, []);

  useEffect(() => {
    if (!showUtilities && !showAlerts && !showHelp) {
      return;
    }

    if (showUtilities) void loadRefreshes();
    if (showAlerts) void loadAlerts();
    const interval = showAlerts ? window.setInterval(() => void loadAlerts(), ALERT_POLL_INTERVAL_MS) : undefined;
    const closeDropdowns = (event: PointerEvent) => {
      if (actionsRef.current?.contains(event.target as Node)) return;
      actionsRef.current
        ?.querySelectorAll<HTMLDetailsElement>("details[open]")
        .forEach((dropdown) => dropdown.removeAttribute("open"));
    };
    document.addEventListener("pointerdown", closeDropdowns);

    return () => {
      if (interval) window.clearInterval(interval);
      document.removeEventListener("pointerdown", closeDropdowns);
      if (toastTimer.current !== undefined) {
        window.clearTimeout(toastTimer.current);
      }
    };
  }, [loadAlerts, loadRefreshes, showAlerts, showHelp, showUtilities]);

  const showToast = (nextToast: ToastState) => {
    setToast(nextToast);
    if (toastTimer.current !== undefined) {
      window.clearTimeout(toastTimer.current);
    }
    toastTimer.current = window.setTimeout(() => setToast(null), TOAST_DURATION_MS);
  };

  const handlePull = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedNumber = number.trim();
    if (!trimmedNumber) return;

    setIsPulling(true);
    try {
      const result = await requestPull(trimmedNumber);
      setNumber("");
      showToast({
        kind: "success",
        message: `${result.type} ${result.number} was requested. Refresh the agreement list in a few moments.`,
      });
    } catch {
      showToast({
        kind: "error",
        message: `Unable to request ${trimmedNumber}. Use a quote beginning Q or an agreement beginning A or T.`,
      });
    } finally {
      setIsPulling(false);
    }
  };

  const alertCount = alerts.length > 99 ? "99+" : String(alerts.length);

  const handleDropdownToggle = (event: SyntheticEvent<HTMLDetailsElement>, loadItems?: () => Promise<void>) => {
    const currentDropdown = event.currentTarget;
    if (!currentDropdown.open) return;

    currentDropdown.parentElement?.querySelectorAll<HTMLDetailsElement>("details[open]").forEach((dropdown) => {
      if (dropdown !== currentDropdown) dropdown.removeAttribute("open");
    });
    if (loadItems) void loadItems();
  };

  return (
    <div
      ref={actionsRef}
      className={`${styles.actions} ${styles[appearance]} ${styles[placement]} ${compact ? styles.compact : ""}`}
    >
      {showPull && (
        <form className={styles.pullForm} onSubmit={handlePull}>
          <label className={styles.srOnly} htmlFor="global-pull-number">
            Quote or agreement number
          </label>
          <input
            id="global-pull-number"
            value={number}
            onChange={(event) => setNumber(event.target.value)}
            placeholder="Quote or Agreement #"
            autoComplete="off"
          />
          <button
            className={styles.pullButton}
            type="submit"
            disabled={isPulling || !number.trim()}
            aria-label="Pull quote or agreement"
            title="Pull quote or agreement"
          >
            <LuDownload aria-hidden="true" />
            <span>{isPulling ? "Pulling…" : "Pull"}</span>
          </button>
        </form>
      )}

      {showHelp && (
        <details className={styles.dropdown} onToggle={handleDropdownToggle}>
          <summary
            className={styles.iconButton}
            aria-label={t("utilities.needHelp")}
            title={compact ? t("utilities.needHelp") : undefined}
          >
            <LuHeadset aria-hidden="true" />
            <span className={styles.sidebarLabel}>{t("utilities.needHelp")}</span>
            <LuChevronDown className={styles.chevron} aria-hidden="true" />
          </summary>
          <div className={styles.menu}>
            <div className={styles.menuHeader}>
              <strong>{t("utilities.needHelp")}</strong>
              <span className={styles.menuHeaderActions}>
                <button
                  type="button"
                  className={styles.menuClose}
                  onClick={(event) => event.currentTarget.closest("details")?.removeAttribute("open")}
                  aria-label={t("utilities.closeHelp")}
                >
                  <LuX aria-hidden="true" />
                </button>
              </span>
            </div>
            <div className={styles.helpContent}>
              <a
                className={styles.helpRequestLink}
                href={NOF_SERVICE_REQUEST_URL}
                target="_blank"
                rel="noopener noreferrer"
                onClick={(event) => event.currentTarget.closest("details")?.removeAttribute("open")}
              >
                <LuHeadset aria-hidden="true" />
                <span>{t("utilities.raiseNofRequest")}</span>
                <LuExternalLink className={styles.externalLinkIcon} aria-hidden="true" />
              </a>
              <a
                className={styles.helpGuideLink}
                href={NOF_USER_GUIDES_URL}
                target="_blank"
                rel="noopener noreferrer"
                onClick={(event) => event.currentTarget.closest("details")?.removeAttribute("open")}
              >
                <span>{t("utilities.userGuides")}</span>
                <LuExternalLink className={styles.externalLinkIcon} aria-hidden="true" />
              </a>
            </div>
          </div>
        </details>
      )}

      {showAlerts && (
        <details className={styles.dropdown} onToggle={(event) => handleDropdownToggle(event, loadAlerts)}>
          <summary
            className={styles.iconButton}
            aria-label={`${alerts.length} unread alerts`}
            title={compact ? t("utilities.alerts") : undefined}
          >
            <LuBell aria-hidden="true" />
            <span className={styles.sidebarLabel}>{t("utilities.alerts")}</span>
            <span className={styles.badge}>{alertCount}</span>
            <LuChevronDown className={styles.chevron} aria-hidden="true" />
          </summary>
          <div className={`${styles.menu} ${styles.alertMenu}`}>
            <div className={styles.menuHeader}>
              <strong>Alerts</strong>
              <span className={styles.menuHeaderActions}>
                <button type="button" onClick={() => void loadAlerts()}>
                  Refresh
                </button>
                <button
                  type="button"
                  className={styles.menuClose}
                  onClick={(event) => event.currentTarget.closest("details")?.removeAttribute("open")}
                  aria-label="Close alerts"
                >
                  <LuX aria-hidden="true" />
                </button>
              </span>
            </div>
            {alertsError ? (
              <p className={styles.menuMessage}>Alerts are unavailable.</p>
            ) : alerts.length === 0 ? (
              <p className={styles.menuMessage}>You have no alerts.</p>
            ) : (
              <ul className={styles.alertList}>
                {alerts.slice(0, 20).map((alert) => (
                  <li key={alert.id}>
                    <Link
                      to={`/agreements/${alert.headerId}`}
                      onClick={(event) => event.currentTarget.closest("details")?.removeAttribute("open")}
                    >
                      <span className={styles.alertIcon}>
                        <LuBell aria-hidden="true" />
                      </span>
                      <span className={styles.alertCopy}>{alert.text}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </details>
      )}

      {showUtilities && (
        <details className={styles.dropdown} onToggle={(event) => handleDropdownToggle(event, loadRefreshes)}>
          <summary
            className={styles.iconButton}
            aria-label={t("utilities.dataRefreshes")}
            title={compact ? t("utilities.dataRefreshes") : undefined}
          >
            <LuClock3 aria-hidden="true" />
            <span className={styles.sidebarLabel}>{t("utilities.dataRefreshes")}</span>
            <LuChevronDown className={styles.chevron} aria-hidden="true" />
          </summary>
          <div className={styles.menu}>
            <div className={styles.menuHeader}>
              <span>Data refreshes</span>
              <span className={styles.menuHeaderActions}>
                <button type="button" onClick={() => void loadRefreshes()}>
                  Refresh
                </button>
                <button
                  type="button"
                  className={styles.menuClose}
                  onClick={(event) => event.currentTarget.closest("details")?.removeAttribute("open")}
                  aria-label="Close data refreshes"
                >
                  <LuX aria-hidden="true" />
                </button>
              </span>
            </div>
            {refreshError ? (
              <p className={styles.menuMessage}>Refresh times are unavailable.</p>
            ) : refreshes.length === 0 ? (
              <p className={styles.menuMessage}>No refresh information is available.</p>
            ) : (
              <ul className={styles.refreshList}>
                {refreshes.map((refresh) => (
                  <li key={refresh.key} title={refresh.description}>
                    <span>{refresh.key}</span>
                    <time dateTime={refresh.lastSuccessfulRunUtc ?? undefined}>
                      {formatRefreshTime(refresh.lastSuccessfulRunUtc)}
                    </time>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </details>
      )}

      {toast && (
        <div className={`${styles.toast} ${styles[toast.kind]}`} role="status">
          <LuDownload aria-hidden="true" />
          <span>{toast.message}</span>
          <button type="button" onClick={() => setToast(null)} aria-label="Dismiss notification">
            ×
          </button>
        </div>
      )}
    </div>
  );
};

function formatRefreshTime(value: string | null): string {
  if (!value) return "Never";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Unknown";

  return date.toLocaleString([], {
    dateStyle: "medium",
    timeStyle: "short",
  });
}
