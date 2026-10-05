import { type FC } from "react";
import { FiArrowLeft } from "react-icons/fi";
import { useTranslation } from "react-i18next";
import type { AppConfiguration } from "../../types";
import styles from "./FrontendPreviewBanner.module.css";

interface FrontendPreviewBannerProps {
  configuration: AppConfiguration | null;
}

export const FrontendPreviewBanner: FC<FrontendPreviewBannerProps> = ({ configuration }) => {
  const { t } = useTranslation();

  if (!configuration?.showPreviewBanner) {
    return null;
  }

  const badge = configuration.environmentLabel
    ? t("preview.environmentBadge", { environment: configuration.environmentLabel })
    : t("preview.badge");

  return (
    <aside className={styles.banner} aria-label={t("preview.label")} data-print-hidden>
      <span className={styles.badge}>{badge}</span>
      <span className={styles.message}>{t("preview.message")}</span>
      {configuration.deploymentInfo && (
        <DeploymentIdentity
          environment={configuration.environmentLabel}
          deploymentInfo={configuration.deploymentInfo}
        />
      )}
      {configuration.legacyFrontendUrl && (
        <a className={styles.returnLink} href={configuration.legacyFrontendUrl}>
          <FiArrowLeft aria-hidden />
          {t("preview.returnToCurrent")}
        </a>
      )}
    </aside>
  );
};

const COMMIT_URL_BASE = "https://github.com/AggrekoTechnologyServices/Order-Fulfillment/commit/";
const COMMIT_SHA_PATTERN = /^[a-f\d]{7,64}$/i;

interface DeploymentIdentityProps {
  environment: string;
  deploymentInfo: NonNullable<AppConfiguration["deploymentInfo"]>;
}

const DeploymentIdentity: FC<DeploymentIdentityProps> = ({ environment, deploymentInfo }) => {
  const { t } = useTranslation();
  const version = deploymentInfo.version || t("preview.deploymentUnavailable");
  const validCommit =
    deploymentInfo.commitSha && COMMIT_SHA_PATTERN.test(deploymentInfo.commitSha) ? deploymentInfo.commitSha : null;

  return (
    <details className={styles.deploymentDetails}>
      <summary>
        {t("preview.deploymentSummary", {
          environment: environment || t("preview.environmentUnknown"),
          version,
          commit: validCommit?.slice(0, 7) ?? t("preview.commitUnknown"),
        })}
      </summary>
      <dl className={styles.deploymentMetadata}>
        <div>
          <dt>{t("preview.release")}</dt>
          <dd>{version}</dd>
        </div>
        <div>
          <dt>{t("preview.commit")}</dt>
          <dd>
            {validCommit ? (
              <a href={`${COMMIT_URL_BASE}${validCommit}`} target="_blank" rel="noopener noreferrer">
                {validCommit}
              </a>
            ) : (
              t("preview.commitUnknown")
            )}
          </dd>
        </div>
        {deploymentInfo.sourceRef && (
          <div>
            <dt>{t("preview.sourceRef")}</dt>
            <dd>{deploymentInfo.sourceRef}</dd>
          </div>
        )}
        {deploymentInfo.buildTimestamp && (
          <div>
            <dt>{t("preview.builtAt")}</dt>
            <dd>{deploymentInfo.buildTimestamp}</dd>
          </div>
        )}
      </dl>
    </details>
  );
};
