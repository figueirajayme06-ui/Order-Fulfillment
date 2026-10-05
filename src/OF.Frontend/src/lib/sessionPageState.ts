export type SessionPageStatePage = "admin" | "agreements" | "assets" | "ringfence";

interface SessionPageStateEnvelope {
  version: 1;
  user: string;
  page: SessionPageStatePage;
  state: unknown;
}

const KEY_PREFIX = "nof.pageState.v1";

export function getSessionPageStateKey(userIdentifier: string, page: SessionPageStatePage): string {
  return `${KEY_PREFIX}:${encodeURIComponent(userIdentifier.trim().toLocaleLowerCase())}:${page}`;
}

export function readSessionPageState<State>(
  userIdentifier: string | null | undefined,
  page: SessionPageStatePage,
  decode: (value: unknown) => State | null,
): State | null {
  if (!userIdentifier || typeof window === "undefined") return null;
  try {
    const raw = window.sessionStorage.getItem(getSessionPageStateKey(userIdentifier, page));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<SessionPageStateEnvelope>;
    const normalizedUser = userIdentifier.trim().toLocaleLowerCase();
    if (
      parsed.version !== 1 ||
      parsed.page !== page ||
      typeof parsed.user !== "string" ||
      parsed.user.toLocaleLowerCase() !== normalizedUser
    ) {
      return null;
    }
    return decode(parsed.state);
  } catch {
    return null;
  }
}

export function writeSessionPageState<State>(
  userIdentifier: string | null | undefined,
  page: SessionPageStatePage,
  state: State,
): void {
  if (!userIdentifier || typeof window === "undefined") return;
  try {
    const envelope: SessionPageStateEnvelope = {
      version: 1,
      user: userIdentifier.trim().toLocaleLowerCase(),
      page,
      state,
    };
    window.sessionStorage.setItem(getSessionPageStateKey(userIdentifier, page), JSON.stringify(envelope));
  } catch {
    // Storage can be disabled or full; working pages must remain usable.
  }
}

export function clearSessionPageState(userIdentifier: string | null | undefined, page: SessionPageStatePage): void {
  if (!userIdentifier || typeof window === "undefined") return;
  try {
    window.sessionStorage.removeItem(getSessionPageStateKey(userIdentifier, page));
  } catch {
    // Storage can be unavailable without preventing reset.
  }
}

export function hasStoredSavedViewSelection(storageKeys: readonly string[]): boolean {
  if (typeof window === "undefined") return false;
  try {
    return storageKeys.some((key) => Boolean(window.localStorage.getItem(key)));
  } catch {
    return false;
  }
}
