import type { AppConfiguration } from "../types";

const ACTIVE_VERSION_KEY = "of.release.active";
const RELOAD_GUARD_KEY = "of.release.reload";
const CHUNK_LOAD_FAILURE =
  /(?:Failed to fetch dynamically imported module|Importing a module script failed|Loading chunk .+ failed)/i;

export function reloadForStaleRelease(
  configuration: AppConfiguration,
  reload: () => void = () => window.location.reload(),
): boolean {
  const nextVersion = configuration.deploymentInfo?.version?.trim();
  if (!nextVersion) {
    return false;
  }

  try {
    const currentVersion = window.sessionStorage.getItem(ACTIVE_VERSION_KEY);
    window.sessionStorage.setItem(ACTIVE_VERSION_KEY, nextVersion);
    if (!currentVersion || currentVersion === nextVersion) {
      return false;
    }

    return guardedReload(`${currentVersion}->${nextVersion}`, reload);
  } catch {
    return false;
  }
}

export function installChunkFailureRecovery(): () => void {
  const onError = (event: ErrorEvent) => {
    if (CHUNK_LOAD_FAILURE.test(event.message)) {
      guardedReload("chunk-load", () => window.location.reload());
    }
  };
  const onUnhandledRejection = (event: PromiseRejectionEvent) => {
    const message = event.reason instanceof Error ? event.reason.message : String(event.reason ?? "");
    if (CHUNK_LOAD_FAILURE.test(message)) {
      guardedReload("chunk-load", () => window.location.reload());
    }
  };

  window.addEventListener("error", onError);
  window.addEventListener("unhandledrejection", onUnhandledRejection);
  return () => {
    window.removeEventListener("error", onError);
    window.removeEventListener("unhandledrejection", onUnhandledRejection);
  };
}

function guardedReload(reason: string, reload: () => void): boolean {
  try {
    if (window.sessionStorage.getItem(RELOAD_GUARD_KEY) === reason) {
      return false;
    }
    window.sessionStorage.setItem(RELOAD_GUARD_KEY, reason);
    reload();
    return true;
  } catch {
    return false;
  }
}
