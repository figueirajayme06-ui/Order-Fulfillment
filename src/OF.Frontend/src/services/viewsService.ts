import axios from "axios";
import api from "./api";
import {
  createSavedView as createLocalSavedView,
  deleteSavedView as deleteLocalSavedView,
  listSavedViews as listLocalSavedViews,
  updateSavedView as updateLocalSavedView,
  type SavedViewDecoder,
  type SavedViewPage,
  type SavedViewRecord,
} from "../lib/savedViewsStorage";

export type SavedViewSource = "api" | "local";
export type SavedViewScope = "personal" | "users" | "division" | "global";

export interface SavedViewRecipient {
  loginName: string;
  fullName: string;
}

export interface PersistedSavedView<TState> {
  id: string;
  name: string;
  page: SavedViewPage;
  scope: SavedViewScope;
  owner: string | null;
  isOwner: boolean;
  canEdit: boolean;
  canDelete: boolean;
  isDefault: boolean;
  source: SavedViewSource;
  recipients: SavedViewRecipient[];
  state: TState;
}

export interface SavedViewsListResult<TState> {
  source: SavedViewSource;
  views: PersistedSavedView<TState>[];
}

export interface SavedViewMutationResult<TState> {
  source: SavedViewSource;
  view: PersistedSavedView<TState> | null;
}

export interface SavedViewDeleteResult {
  source: SavedViewSource;
  deleted: boolean;
}

interface SavedViewApiDto {
  id: number;
  name: string;
  page: string;
  scope: SavedViewScope;
  owner: string;
  isOwner?: boolean;
  canEdit: boolean;
  canDelete: boolean;
  isDefault?: boolean;
  recipients?: SavedViewRecipient[];
  state: unknown;
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

function normalizeScope(scope: string | null | undefined): SavedViewScope {
  if (scope === "global") {
    return "global";
  }

  if (scope === "division") {
    return "division";
  }

  if (scope === "users") {
    return "users";
  }

  return "personal";
}

function normalizeName(name: string | null | undefined): string {
  return (name ?? "").trim();
}

function mapLocalView<TState>(view: SavedViewRecord<TState>): PersistedSavedView<TState> {
  return {
    id: view.id,
    name: view.name,
    page: view.page,
    scope: "personal",
    owner: null,
    isOwner: true,
    canEdit: true,
    canDelete: true,
    isDefault: false,
    source: "local",
    recipients: [],
    state: view.state,
  };
}

function mapApiView<TState>(
  dto: SavedViewApiDto,
  page: SavedViewPage,
  decodeState: SavedViewDecoder<TState>,
): PersistedSavedView<TState> | null {
  const normalizedPage = normalizeSavedViewPage(dto.page);
  if (!normalizedPage || normalizedPage !== page) {
    return null;
  }

  const normalized = normalizeName(dto.name);
  if (!normalized) {
    return null;
  }

  const decodedState = decodeState(dto.state);
  if (!decodedState) {
    return null;
  }

  return {
    id: String(dto.id),
    name: normalized,
    page,
    scope: normalizeScope(dto.scope),
    owner: dto.owner ?? null,
    isOwner: dto.isOwner === true,
    canEdit: dto.canEdit,
    canDelete: dto.canDelete,
    isDefault: dto.isDefault === true,
    source: "api",
    recipients: Array.isArray(dto.recipients)
      ? dto.recipients.filter(
          (recipient) =>
            typeof recipient?.loginName === "string" &&
            recipient.loginName.trim() !== "" &&
            typeof recipient?.fullName === "string",
        )
      : [],
    state: decodedState,
  };
}

function shouldFallbackToLocal(error: unknown): boolean {
  if (!axios.isAxiosError(error)) {
    return false;
  }

  return error.response == null;
}

function toApiId(viewId: string): number | null {
  const parsed = Number(viewId);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return null;
  }

  return parsed;
}

function fallbackLocalList<TState>(
  page: SavedViewPage,
  decodeState: SavedViewDecoder<TState>,
): SavedViewsListResult<TState> {
  const localViews = listLocalSavedViews(page, decodeState).map(mapLocalView);
  return {
    source: "local",
    views: localViews,
  };
}

export async function listSavedViews<TState>(
  page: SavedViewPage,
  decodeState: SavedViewDecoder<TState>,
): Promise<SavedViewsListResult<TState>> {
  try {
    const response = await api.get<SavedViewApiDto[]>("/api/views", {
      params: { page },
    });

    const views = response.data
      .map((entry) => mapApiView(entry, page, decodeState))
      .filter((entry): entry is PersistedSavedView<TState> => entry !== null)
      .sort((a, b) => a.name.localeCompare(b.name));

    return {
      source: "api",
      views,
    };
  } catch {
    return fallbackLocalList(page, decodeState);
  }
}

export async function createSavedView<TState>(
  source: SavedViewSource,
  page: SavedViewPage,
  name: string,
  scope: SavedViewScope,
  state: TState,
  decodeState: SavedViewDecoder<TState>,
  recipients: readonly string[] = [],
): Promise<SavedViewMutationResult<TState>> {
  if (source === "local") {
    if (scope !== "personal") {
      throw new Error("Shared views require the server.");
    }
    const created = createLocalSavedView(page, name, state);
    return {
      source: "local",
      view: created ? mapLocalView(created) : null,
    };
  }

  try {
    const response = await api.post<SavedViewApiDto>("/api/views", {
      name,
      page,
      scope,
      recipients,
      state,
    });

    const mapped = mapApiView(response.data, page, decodeState);
    if (mapped) {
      return {
        source: "api",
        view: mapped,
      };
    }

    return {
      source: "api",
      view: null,
    };
  } catch (error) {
    if (!shouldFallbackToLocal(error) || scope !== "personal") {
      throw error;
    }

    const created = createLocalSavedView(page, name, state);
    return {
      source: "local",
      view: created ? mapLocalView(created) : null,
    };
  }
}

export async function updateSavedView<TState>(
  source: SavedViewSource,
  page: SavedViewPage,
  id: string,
  name: string,
  scope: SavedViewScope,
  state: TState,
  decodeState: SavedViewDecoder<TState>,
  recipients: readonly string[] = [],
): Promise<SavedViewMutationResult<TState>> {
  if (source === "local") {
    if (scope !== "personal") {
      throw new Error("Shared views require the server.");
    }
    const updated = updateLocalSavedView(page, id, name, state);
    return {
      source: "local",
      view: updated ? mapLocalView(updated) : null,
    };
  }

  const apiId = toApiId(id);
  if (!apiId) {
    return {
      source,
      view: null,
    };
  }

  const response = await api.put<SavedViewApiDto>(`/api/views/${apiId}`, {
    name,
    page,
    scope,
    recipients,
    state,
  });

  const mapped = mapApiView(response.data, page, decodeState);
  if (!mapped) {
    return {
      source: "api",
      view: null,
    };
  }

  return {
    source: "api",
    view: mapped,
  };
}

export function getSavedViewValidationMessage(error: unknown): string | null {
  if (!axios.isAxiosError<{ message?: unknown }>(error) || error.response?.status !== 400) return null;
  const message = error.response.data?.message;
  if (typeof message !== "string") return null;
  const normalized = message.replace(/\s+/g, " ").trim();
  return normalized && normalized.length <= 300 ? normalized : null;
}

export async function listSavedViewRecipientCandidates(search: string, limit = 10): Promise<SavedViewRecipient[]> {
  const normalizedSearch = search.trim();
  if (!normalizedSearch) return [];

  const response = await api.get<SavedViewRecipient[]>("/api/views/recipient-candidates", {
    params: { search: normalizedSearch, limit },
  });
  if (!Array.isArray(response.data)) return [];
  const seen = new Set<string>();
  return response.data
    .filter((candidate) => {
      if (typeof candidate?.loginName !== "string" || typeof candidate?.fullName !== "string") return false;
      const key = candidate.loginName.trim().toLocaleLowerCase();
      if (!key || seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .map((candidate) => ({ loginName: candidate.loginName.trim(), fullName: candidate.fullName.trim() }));
}

export async function deleteSavedView(
  source: SavedViewSource,
  page: SavedViewPage,
  id: string,
): Promise<SavedViewDeleteResult> {
  if (source === "local") {
    return {
      source: "local",
      deleted: deleteLocalSavedView(page, id),
    };
  }

  const apiId = toApiId(id);
  if (!apiId) {
    return {
      source,
      deleted: false,
    };
  }

  await api.delete(`/api/views/${apiId}`);

  return {
    source: "api",
    deleted: true,
  };
}
