export interface AppConfiguration {
  environmentLabel: string;
  showPreviewBanner: boolean;
  legacyFrontendUrl: string | null;
  deploymentInfo?: DeploymentInfo;
}

export interface DeploymentInfo {
  version: string | null;
  commitSha: string | null;
  sourceRef: string | null;
  buildTimestamp: string | null;
}
