import { type CSSProperties, type FC } from "react";
import type { IconType } from "react-icons";
import { NavLink } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  FiBox,
  FiArrowRight,
  FiChevronLeft,
  FiChevronRight,
  FiFileText,
  FiHome,
  FiPrinter,
  FiSettings,
  FiShield,
  FiX,
} from "react-icons/fi";
import type { UserInfo } from "../../types";
import { GlobalActions } from "./GlobalActions";
import { ThemeToggle } from "./ThemeToggle";
import type { ThemeMode } from "./themePreference";
import styles from "./Sidebar.module.css";

interface Props {
  user: UserInfo;
  isHome?: boolean;
  collapsed: boolean;
  mobileOpen: boolean;
  onToggleCollapsed: () => void;
  onCloseMobile: () => void;
  theme: ThemeMode;
  onChangeTheme: (theme: ThemeMode) => void;
}

interface NavigationItem {
  to: string;
  label: string;
  icon: IconType;
  adminOnly?: boolean;
  description: string;
  eyebrow: string;
}

export const Sidebar: FC<Props> = ({
  user,
  isHome = false,
  collapsed,
  mobileOpen,
  onToggleCollapsed,
  onCloseMobile,
  theme,
  onChangeTheme,
}) => {
  const { t } = useTranslation();
  const navigationItems: NavigationItem[] = [
    {
      to: "/agreements",
      label: t("nav.agreements"),
      icon: FiFileText,
      description: "Review hire agreements, dates and fulfilment progress.",
      eyebrow: "Orders",
    },
    {
      to: "/assets",
      label: "Assets",
      icon: FiBox,
      description: "Find equipment and see where assets are allocated.",
      eyebrow: "Equipment",
    },
    {
      to: "/ringfence",
      label: t("nav.ringfence"),
      icon: FiShield,
      description: "Protect stock for upcoming work and priority demand.",
      eyebrow: "Availability",
    },
    {
      to: "/admin",
      label: t("nav.admin"),
      icon: FiSettings,
      adminOnly: true,
      description: "Manage users, access and application settings.",
      eyebrow: "Management",
    },
  ];

  const visibleItems = navigationItems.filter(
    (item) => !item.adminOnly || (!user.isReadOnly && (user.isAdmin || user.isSuperAdmin)),
  );
  //const workspaceItems = visibleItems.filter((item) => item.showOnHome);
  const primaryItems = visibleItems.filter((item) => !item.adminOnly);
  const adminItems = visibleItems.filter((item) => item.adminOnly);
  const userInitials = getInitials(user.displayName || user.loginName);

  return (
    <aside
      className={`${styles.sidebar} ${isHome ? styles.home : ""} ${collapsed ? styles.collapsed : ""} ${mobileOpen ? styles.mobileOpen : ""}`}
      data-print-hidden
    >
      <div className={styles.brand}>
        <div className={styles.brandIdentity}>
          <img className={styles.brandLogo} src="/aggreko-logo.svg" alt="Aggreko" />
          <span className={styles.appName}>{t("common.appName")}</span>
        </div>
        <span className={styles.compactMark} aria-label="Order Fulfillment">
          OF
        </span>

        {!isHome && (
          <>
            <button
              type="button"
              className={styles.desktopToggle}
              onClick={onToggleCollapsed}
              aria-label={collapsed ? "Expand navigation" : "Collapse navigation"}
              title={collapsed ? "Expand navigation" : "Collapse navigation"}
            >
              {collapsed ? <FiChevronRight aria-hidden /> : <FiChevronLeft aria-hidden />}
            </button>
            <button type="button" className={styles.mobileClose} onClick={onCloseMobile} aria-label="Close navigation">
              <FiX aria-hidden />
            </button>
          </>
        )}
      </div>

      {isHome ? (
        <div className={styles.homeContent}>
          <section className={styles.welcome}>
            <span className={styles.kicker}>Order Fulfillment</span>
            <h1>
              Good to see you,
              <br />
              <span>{user.displayName.split(" ").filter(Boolean)[0] ?? user.displayName}.</span>
            </h1>
            <p>Choose a workspace to get started.</p>
          </section>

          <div className={styles.homeWorkspace}>
            <div className={`${styles.workspaceFlair} ${styles.workspaceFlairTop}`} aria-hidden="true">
              <span />
              <span />
              <span />
            </div>
            <nav className={styles.homeNav} aria-label="Workspaces">
              {visibleItems.map(({ to, label, description, eyebrow, icon: Icon }, index) => (
                <NavLink
                  key={to}
                  to={to}
                  className={styles.homeCard}
                  style={{ "--item-index": index } as CSSProperties}
                >
                  <span className={styles.homeCardIcon}>
                    <Icon aria-hidden />
                  </span>
                  <span className={styles.homeCardCopy}>
                    <span className={styles.homeCardEyebrow}>{eyebrow}</span>
                    <span className={styles.homeCardTitle}>{label}</span>
                    <span className={styles.homeCardDescription}>{description}</span>
                  </span>
                  <FiArrowRight className={styles.homeCardArrow} aria-hidden />
                </NavLink>
              ))}
            </nav>
            <div className={`${styles.workspaceFlair} ${styles.workspaceFlairBottom}`} aria-hidden="true">
              <span />
              <span />
              <span />
            </div>
          </div>
        </div>
      ) : (
        <nav className={styles.nav} aria-label={t("nav.mainNavigation")}>
          {primaryItems.map(({ to, label, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) => `${styles.navLink} ${isActive ? styles.active : ""}`}
              aria-label={label}
              title={collapsed ? label : undefined}
              onClick={onCloseMobile}
            >
              <Icon className={styles.navIcon} aria-hidden />
              <span className={styles.navLabel}>{label}</span>
            </NavLink>
          ))}
          <NavLink to="/" className={styles.navLink} aria-label={t("nav.home")} onClick={onCloseMobile}>
            <FiHome className={styles.navIcon} aria-hidden />
            <span className={styles.navLabel}>{t("Home")}</span>
          </NavLink>
          {adminItems.length > 0 && (
            <div className={styles.secondaryNav}>
              {adminItems.map(({ to, label, icon: Icon }) => (
                <NavLink
                  key={to}
                  to={to}
                  className={({ isActive }) => `${styles.navLink} ${isActive ? styles.active : ""}`}
                  aria-label={label}
                  title={collapsed ? label : undefined}
                  onClick={onCloseMobile}
                >
                  <Icon className={styles.navIcon} aria-hidden />
                  <span className={styles.navLabel}>{label}</span>
                </NavLink>
              ))}
            </div>
          )}
        </nav>
      )}

      {!isHome && (
        <div className={styles.utilitySection}>
          <GlobalActions appearance="dark" placement="sidebar" showPull={false} compact={collapsed} showHelp />
          <ThemeToggle theme={theme} onChange={onChangeTheme} appearance="sidebar" compact={collapsed} />
          <button
            type="button"
            className={styles.printButton}
            onClick={() => window.print()}
            aria-label="Print current page"
            title={collapsed ? "Print current page" : undefined}
          >
            <FiPrinter className={styles.navIcon} aria-hidden />
            <span className={styles.navLabel}>Print</span>
          </button>
        </div>
      )}

      <div className={styles.userInfo} title={collapsed ? user.displayName : undefined}>
        <span className={styles.userAvatar} aria-hidden>
          {userInitials}
        </span>
        <span className={styles.userDetails}>
          <span className={styles.userName}>{user.displayName}</span>
          {user.isReadOnly && <span className={styles.userAccess}>{t("admin.readOnly")}</span>}
        </span>
      </div>
    </aside>
  );
};

function getInitials(name: string): string {
  const initials = name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((part) => part.charAt(0))
    .join("");

  return initials.toUpperCase() || "U";
}
