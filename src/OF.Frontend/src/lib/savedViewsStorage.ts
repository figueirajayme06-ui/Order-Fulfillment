export type SavedViewPage = "agreements" | "assets";

export interface SavedViewRecord<TState> {
  id: string;
  name: string;
  page: SavedViewPage;
  state: TState;
  createdAt: string;
  updatedAt: string;
}

interface SavedViewEnvelope {
  schemaVersion: number;
  views: SavedViewRecord<unknown>[];
}

type StorageLike = Pick<Storage, "getItem" | "setItem" | "removeItem">;

export type SavedViewDecoder<TState> = (value: unknown) => TState | null;

const STORAGE_KEY = "nof.savedViews.v1";
const SCHEMA_VERSION = 1;
const MAX_VIEW_NAME_LENGTH = 80;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function normalizeSavedViewPage(value: unknown): SavedViewPage | null {
  if (value === "agreements" || value === "orders") {
    return "agreements";
  }

  if (value === "assets") {
    return "assets";
  }

  return null;
}

function normalizeViewName(name: string): string {
  return name.trim().replace(/\s+/g, " ").slice(0, MAX_VIEW_NAME_LENGTH);
}

function createViewId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function getDefaultStorage(): StorageLike | null {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

function parseRawView(value: unknown): SavedViewRecord<unknown> | null {
  if (!isRecord(value)) {
    return null;
  }

  const id = value.id;
  const name = value.name;
  const page = normalizeSavedViewPage(value.page);

  if (typeof id !== "string" || id.trim() === "") {
    return null;
  }

  if (typeof name !== "string" || normalizeViewName(name) === "") {
    return null;
  }

  if (!page) {
    return null;
  }

  const now = new Date().toISOString();

  return {
    id,
    name: normalizeViewName(name),
    page,
    state: Object.prototype.hasOwnProperty.call(value, "state") ? value.state : {},
    createdAt: typeof value.createdAt === "string" ? value.createdAt : now,
    updatedAt: typeof value.updatedAt === "string" ? value.updatedAt : now,
  };
}

function emptyEnvelope(): SavedViewEnvelope {
  return {
    schemaVersion: SCHEMA_VERSION,
    views: [],
  };
}

function readEnvelope(storage: StorageLike | null = getDefaultStorage()): SavedViewEnvelope {
  if (!storage) {
    return emptyEnvelope();
  }

  try {
    const raw = storage.getItem(STORAGE_KEY);

    if (!raw) {
      return emptyEnvelope();
    }

    const parsed = JSON.parse(raw) as unknown;

    let candidates: unknown[] = [];
    if (Array.isArray(parsed)) {
      candidates = parsed;
    } else if (isRecord(parsed) && Array.isArray(parsed.views)) {
      candidates = parsed.views;
    } else {
      return emptyEnvelope();
    }

    const views = candidates.map(parseRawView).filter((value): value is SavedViewRecord<unknown> => value !== null);

    return {
      schemaVersion: SCHEMA_VERSION,
      views,
    };
  } catch {
    return emptyEnvelope();
  }
}

function writeEnvelope(envelope: SavedViewEnvelope, storage: StorageLike | null = getDefaultStorage()): boolean {
  if (!storage) {
    return false;
  }

  try {
    storage.setItem(STORAGE_KEY, JSON.stringify(envelope));
    return true;
  } catch {
    return false;
  }
}

export function listSavedViews<TState>(
  page: SavedViewPage,
  decodeState: SavedViewDecoder<TState>,
  storage: StorageLike | null = getDefaultStorage(),
): SavedViewRecord<TState>[] {
  const views: SavedViewRecord<TState>[] = [];

  readEnvelope(storage).views.forEach((view) => {
    if (view.page !== page) {
      return;
    }

    const decodedState = decodeState(view.state);
    if (!decodedState) {
      return;
    }

    views.push({
      ...view,
      state: decodedState,
    });
  });

  return views.sort((a, b) => a.name.localeCompare(b.name));
}

export function createSavedView<TState>(
  page: SavedViewPage,
  name: string,
  state: TState,
  storage: StorageLike | null = getDefaultStorage(),
): SavedViewRecord<TState> | null {
  const normalizedName = normalizeViewName(name);
  if (!normalizedName) {
    return null;
  }

  const envelope = readEnvelope(storage);
  const now = new Date().toISOString();

  const newView: SavedViewRecord<TState> = {
    id: createViewId(),
    name: normalizedName,
    page,
    state,
    createdAt: now,
    updatedAt: now,
  };

  envelope.views.push(newView as SavedViewRecord<unknown>);

  if (!writeEnvelope(envelope, storage)) {
    return null;
  }

  return newView;
}

export function updateSavedView<TState>(
  page: SavedViewPage,
  id: string,
  name: string,
  state: TState,
  storage: StorageLike | null = getDefaultStorage(),
): SavedViewRecord<TState> | null {
  const normalizedName = normalizeViewName(name);
  if (!normalizedName) {
    return null;
  }

  const envelope = readEnvelope(storage);
  const index = envelope.views.findIndex((view) => view.id === id && view.page === page);

  if (index < 0) {
    return null;
  }

  const existing = envelope.views[index];
  const updated: SavedViewRecord<TState> = {
    ...existing,
    name: normalizedName,
    state,
    updatedAt: new Date().toISOString(),
  };

  envelope.views[index] = updated as SavedViewRecord<unknown>;

  if (!writeEnvelope(envelope, storage)) {
    return null;
  }

  return updated;
}

export function deleteSavedView(
  page: SavedViewPage,
  id: string,
  storage: StorageLike | null = getDefaultStorage(),
): boolean {
  const envelope = readEnvelope(storage);
  const nextViews = envelope.views.filter((view) => !(view.id === id && view.page === page));

  if (nextViews.length === envelope.views.length) {
    return false;
  }

  envelope.views = nextViews;
  return writeEnvelope(envelope, storage);
}

export function clearSavedViews(storage: StorageLike | null = getDefaultStorage()): void {
  if (!storage) {
    return;
  }

  try {
    storage.removeItem(STORAGE_KEY);
  } catch {
    // Ignore storage failures and keep current UI behavior stable.
  }
}
