import type { FC } from "react";
import type { IconType } from "react-icons";
import { FiLogIn, FiRefreshCw, FiUserX, FiWifiOff } from "react-icons/fi";
import { useTranslation } from "react-i18next";
import type { AuthFailureReason } from "../../contexts/auth/AuthContext";
import { Button } from "../common";
import styles from "./AuthFailurePage.module.css";

interface Props {
  reason: AuthFailureReason;
}

const icons: Record<AuthFailureReason, IconType> = {
  notProvisioned: FiUserX,
  authenticationRequired: FiLogIn,
  serviceUnavailable: FiWifiOff,
};

export const AuthFailurePage: FC<Props> = ({ reason }) => {
  const { t } = useTranslation();
  const Icon = icons[reason];

  return (
    <main className={styles.page}>
      <section className={styles.panel} aria-labelledby="auth-failure-title">
        <div className={styles.brand}>
          <img src="/aggreko-logo.svg" alt="Aggreko" />
          <span>{t("common.appName")}</span>
        </div>

        <div className={styles.content}>
          <span className={styles.icon} aria-hidden>
            <Icon />
          </span>
          <p className={styles.eyebrow}>{t(`auth.${reason}.eyebrow`)}</p>
          <h1 id="auth-failure-title">{t(`auth.${reason}.title`)}</h1>
          <p className={styles.description}>{t(`auth.${reason}.description`)}</p>
          {reason === "notProvisioned" && <p className={styles.nextStep}>{t("auth.notProvisioned.nextStep")}</p>}
          <div className={styles.action}>
            <Button
              label={t("auth.tryAgain")}
              size="large"
              icon={<FiRefreshCw aria-hidden />}
              onClick={() => window.location.reload()}
            />
          </div>
        </div>
      </section>
    </main>
  );
};
