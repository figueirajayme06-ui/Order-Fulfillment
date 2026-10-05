import { useEffect, useLayoutEffect, useState, type FC } from "react";
import { FiMenu } from "react-icons/fi";
import { Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../../contexts/auth";
import { fetchAppConfiguration } from "../../services/appConfigurationService";
import type { AppConfiguration } from "../../types";
import { installChunkFailureRecovery, reloadForStaleRelease } from "../../lib/staleReleaseRecovery";
import { FrontendPreviewBanner } from "./FrontendPreviewBanner";
import { AuthFailurePage } from "./AuthFailurePage";
import { Sidebar } from "./Sidebar";
import { applyThemePreference, readThemePreference, storeThemePreference, type ThemeMode } from "./themePreference";
import styles from "./AppLayout.module.css";

const SIDEBAR_COLLAPSED_STORAGE_KEY = "of.sidebar.collapsed";

export const AppLayout: FC = () => {
  const { user, isLoading, error } = useAuth();
  const { pathname } = useLocation();
  const isHome = pathname === "/";
  const isViewportContainedPage =
    pathname === "/agreements" || pathname === "/agreements/" || pathname === "/assets" || pathname === "/assets/";
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(readSidebarCollapsed);
  const [isMobileNavigationOpen, setIsMobileNavigationOpen] = useState(false);
  const [appConfiguration, setAppConfiguration] = useState<AppConfiguration | null>(null);
  const [theme, setTheme] = useState<ThemeMode>(readThemePreference);

  useLayoutEffect(() => {
    applyThemePreference(theme);
    storeThemePreference(theme);
  }, [theme]);

  useEffect(() => installChunkFailureRecovery(), []);

  useEffect(() => {
    if (!user) {
      return;
    }

    let cancelled = false;
    fetchAppConfiguration()
      .then((configuration) => {
        if (!cancelled) {
          if (reloadForStaleRelease(configuration)) {
            return;
          }
          setAppConfiguration(configuration);
        }
      })
      .catch(() => {
        // Runtime presentation configuration must never block operational workflows.
      });

    return () => {
      cancelled = true;
    };
  }, [user]);

  useEffect(() => {
    try {
      window.localStorage.setItem(SIDEBAR_COLLAPSED_STORAGE_KEY, String(isSidebarCollapsed));
    } catch {
      // Navigation remains usable when browser storage is unavailable.
    }
  }, [isSidebarCollapsed]);

  useEffect(() => {
    if (!isMobileNavigationOpen) {
      return;
    }

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsMobileNavigationOpen(false);
      }
    };

    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [isMobileNavigationOpen]);

  if (isLoading) {
    return (
      <div className={styles.loadingContainer}>
        <div className={styles.spinner} />
        <p>Loading...</p>
      </div>
    );
  }

  if (error || !user) {
    return <AuthFailurePage reason={error ?? "authenticationRequired"} />;
  }

  return (
    <div
      className={`${styles.layout} ${isHome ? styles.homeLayout : ""} ${isSidebarCollapsed ? styles.collapsedLayout : ""}`}
      data-print-layout
    >
      <Sidebar
        user={user}
        isHome={isHome}
        collapsed={isSidebarCollapsed}
        mobileOpen={isMobileNavigationOpen}
        onToggleCollapsed={() => setIsSidebarCollapsed((collapsed) => !collapsed)}
        onCloseMobile={() => setIsMobileNavigationOpen(false)}
        theme={theme}
        onChangeTheme={setTheme}
      />
      {!isHome && isMobileNavigationOpen && (
        <button
          type="button"
          className={styles.backdrop}
          onClick={() => setIsMobileNavigationOpen(false)}
          aria-label="Close navigation"
          data-print-hidden
        />
      )}
      <main
        className={`${styles.main} ${isHome ? styles.mainHome : ""} ${isViewportContainedPage ? styles.mainViewportContained : ""}`}
        aria-hidden={isHome || undefined}
        data-viewport-contained={isViewportContainedPage || undefined}
      >
        {!isHome && (
          <>
            <div className={styles.mobileHeader} data-print-hidden>
              <button
                type="button"
                className={styles.mobileMenuButton}
                onClick={() => setIsMobileNavigationOpen(true)}
                aria-label="Open navigation"
              >
                <FiMenu aria-hidden />
              </button>
              <span className={styles.mobileAppName}>Order Fulfillment</span>
            </div>
            <FrontendPreviewBanner configuration={appConfiguration} />
            <div
              className={`${styles.pageContent} ${isViewportContainedPage ? styles.pageContentViewportContained : ""}`}
              data-print-content
            >
              <Outlet />
            </div>
          </>
        )}
      </main>
    </div>
  );
};

function readSidebarCollapsed(): boolean {
  try {
    return window.localStorage.getItem(SIDEBAR_COLLAPSED_STORAGE_KEY) === "true";
  } catch {
    return false;
  }
}
