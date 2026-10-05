import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type FC, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Alert, Badge, Button, Card, Spinner, TableColumnHeader } from "../../components/common";
import { useAuth } from "../../contexts/auth";
import { getRingfenceItemBatchResult } from "../../lib/ringfenceBatchResult";
import { clearSessionPageState, readSessionPageState, writeSessionPageState } from "../../lib/sessionPageState";
import { removeAssetFromRingfence } from "../../services/assetsService";
import {
  fetchDivisions,
  fetchUsers,
  fetchWarehouses,
  type DivisionLookup,
  type UserLookup,
  type WarehouseLookup,
} from "../../services/lookupsService";
import {
  addRingfenceItems,
  createRingfence,
  deleteRingfence,
  fetchRingfence,
  fetchRingfences,
  preflightRingfenceItems,
  updateRingfence,
  type RingfenceAsset,
  type RingfenceDetail,
  type RingfenceInput,
  type RingfenceListItem,
} from "../../services/ringfenceService";
import { RingfenceAssets } from "./RingfenceAssets";
import {
  ConfirmationDialog,
  getConfirmationButtonLabel,
  getConfirmationTitle,
  type DialogState,
} from "./RingfenceConfirmationDialog";
import { RingfenceEditor, focusFirstInvalidField } from "./RingfenceEditor";
import {
  EMPTY_FORM,
  cleanForm,
  compareText,
  filterRingfenceAssets,
  filterRingfences,
  filterWarehousesByDivision,
  formatDate,
  formatDateRange,
  getErrorMessage,
  getRingfenceStatus,
  getServerErrors,
  isEligibleRingfenceOwner,
  isRingfenceWarehouse,
  isValidRingfenceWarehouse,
  parseAssetIds,
  parseRingfenceId,
  parseRingfencePageState,
  ringfenceStatusVariant,
  sameDivisionSelection,
  sameText,
  splitDivisions,
  toDateInput,
  todayInputValue,
  validateForm,
  type AssetSortField,
  type FormErrors,
  type ListSortField,
  type RingfencePageState,
  type SortDirection,
} from "./ringfenceModel";
import styles from "./RingfencePage.module.css";

type FormMode = "create" | "edit" | null;
interface RingfenceEditBaseline {
  divisions: string;
}
const RINGFENCE_PAGE_SIZE = 30;
const ASSET_PAGE_SIZE = 25;
const MAX_BATCH_ASSET_IDS = 250;

export const RingfencePage: FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const canMutate = !user?.isReadOnly;
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedIdFromUrl = parseRingfenceId(searchParams.get("ringfenceId"));
  const restoredWorkingState = useMemo(
    () => readSessionPageState(user?.loginName, "ringfence", parseRingfencePageState),
    [user?.loginName],
  );
  const selectedIdRef = useRef<number | null>(null);
  const pendingUrlSelectionRef = useRef<number | null>(null);
  const latestDetailRequestRef = useRef(0);
  const editBaselineRef = useRef<RingfenceEditBaseline | null>(null);

  const [ringfences, setRingfences] = useState<RingfenceListItem[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detail, setDetail] = useState<RingfenceDetail | null>(null);
  const [isListLoading, setIsListLoading] = useState(true);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [listError, setListError] = useState(false);
  const [detailError, setDetailError] = useState(false);

  const [formMode, setFormMode] = useState<FormMode>(null);
  const [editingRingfenceId, setEditingRingfenceId] = useState<number | null>(null);
  const [form, setForm] = useState<RingfenceInput>(EMPTY_FORM);
  const [isFormSubmitted, setIsFormSubmitted] = useState(false);
  const [serverErrors, setServerErrors] = useState<FormErrors>({});
  const [isSaving, setIsSaving] = useState(false);

  const [divisionOptions, setDivisionOptions] = useState<DivisionLookup[]>([]);
  const [warehouseOptions, setWarehouseOptions] = useState<WarehouseLookup[]>([]);
  const [ownerOptions, setOwnerOptions] = useState<UserLookup[]>([]);
  const [areDependentLookupsLoading, setAreDependentLookupsLoading] = useState(false);
  const [dependentLookupsError, setDependentLookupsError] = useState(false);

  const [searchTerm, setSearchTerm] = useState(() => restoredWorkingState?.searchTerm ?? "");
  const [statusFilter, setStatusFilter] = useState(() => restoredWorkingState?.statusFilter ?? "");
  const [divisionFilter, setDivisionFilter] = useState(() => restoredWorkingState?.divisionFilter ?? "");
  const [warehouseFilter, setWarehouseFilter] = useState(() => restoredWorkingState?.warehouseFilter ?? "");
  const [listSortField, setListSortField] = useState<ListSortField>(
    () => restoredWorkingState?.listSortField ?? "period",
  );
  const [listSortDirection, setListSortDirection] = useState<SortDirection>(
    () => restoredWorkingState?.listSortDirection ?? "asc",
  );
  const [listPage, setListPage] = useState(() => restoredWorkingState?.listPage ?? 1);

  const [assetSearch, setAssetSearch] = useState(() => restoredWorkingState?.assetSearch ?? "");
  const [assetStatusFilter, setAssetStatusFilter] = useState(() => restoredWorkingState?.assetStatusFilter ?? "");
  const [assetSortField, setAssetSortField] = useState<AssetSortField>(
    () => restoredWorkingState?.assetSortField ?? "id",
  );
  const [assetSortDirection, setAssetSortDirection] = useState<SortDirection>(
    () => restoredWorkingState?.assetSortDirection ?? "asc",
  );
  const [assetPage, setAssetPage] = useState(() => restoredWorkingState?.assetPage ?? 1);
  const [isAssetEntryOpen, setIsAssetEntryOpen] = useState(false);
  const [assetIds, setAssetIds] = useState("");
  const [isAddingAssets, setIsAddingAssets] = useState(false);

  const [confirmation, setConfirmation] = useState<DialogState | null>(null);
  const [notice, setNotice] = useState<{ variant: "success" | "error" | "info"; message: string } | null>(null);

  useEffect(() => {
    selectedIdRef.current = selectedId;
  }, [selectedId]);

  const allowedDivisionCodes = useMemo(
    () =>
      user?.division
        .split(",")
        .map((division) => division.trim())
        .filter(Boolean) ?? [],
    [user?.division],
  );

  const availableDivisionOptions = useMemo(() => {
    if (user?.isSuperAdmin) return divisionOptions;
    const allowed = new Set(allowedDivisionCodes.map((division) => division.toLocaleLowerCase()));
    return divisionOptions.filter((division) => allowed.has(division.code.toLocaleLowerCase()));
  }, [allowedDivisionCodes, divisionOptions, user?.isSuperAdmin]);

  const formDivisionCodes = useMemo(() => splitDivisions(form.divisions), [form.divisions]);
  const formDivisionQuery = formDivisionCodes.join(",");
  const editableDivisionCodes = useMemo(
    () =>
      formMode === "edit" && editBaselineRef.current
        ? splitDivisions(editBaselineRef.current.divisions)
        : formDivisionCodes,
    [formDivisionCodes, formMode],
  );
  const scopedWarehouseOptions = useMemo(
    () => filterWarehousesByDivision(warehouseOptions, formDivisionCodes),
    [formDivisionCodes, warehouseOptions],
  );
  const selectableWarehouseOptions = useMemo(
    () => scopedWarehouseOptions.filter(isRingfenceWarehouse),
    [scopedWarehouseOptions],
  );
  const canEditFormDivisions = useMemo(
    () =>
      Boolean(user?.isSuperAdmin) ||
      editableDivisionCodes.every((division) =>
        allowedDivisionCodes.some((allowedDivision) => sameText(allowedDivision, division)),
      ),
    [allowedDivisionCodes, editableDivisionCodes, user?.isSuperAdmin],
  );

  const loadRingfences = useCallback(async (requestedSelection?: number | null) => {
    setIsListLoading(true);
    setListError(false);

    try {
      const data = await fetchRingfences();
      setRingfences(data);
      setSelectedId((currentSelection) => {
        const requested = requestedSelection ?? currentSelection;
        return requested !== null && requested !== undefined && data.some((ringfence) => ringfence.id === requested)
          ? requested
          : (data[0]?.id ?? null);
      });
    } catch {
      setListError(true);
    } finally {
      setIsListLoading(false);
    }
  }, []);

  const loadDetail = useCallback(async (id: number) => {
    const requestId = ++latestDetailRequestRef.current;
    setIsDetailLoading(true);
    setDetailError(false);

    try {
      const loadedDetail = await fetchRingfence(id);
      if (requestId === latestDetailRequestRef.current) {
        setDetail(loadedDetail);
      }
    } catch {
      if (requestId === latestDetailRequestRef.current) {
        setDetailError(true);
      }
    } finally {
      if (requestId === latestDetailRequestRef.current) {
        setIsDetailLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void loadRingfences(selectedIdFromUrl);
  }, [loadRingfences]);

  useEffect(() => {
    const pendingSelection = pendingUrlSelectionRef.current;
    if (pendingSelection !== null) {
      if (selectedIdFromUrl === pendingSelection) {
        pendingUrlSelectionRef.current = null;
      } else {
        return;
      }
    }

    if (isAddingAssets || isSaving || (confirmation !== null && confirmation.kind !== "discard")) return;
    if (selectedIdFromUrl === null || selectedIdFromUrl === selectedId) return;
    if (!ringfences.some((ringfence) => ringfence.id === selectedIdFromUrl)) return;

    if (formMode) {
      setConfirmation({ kind: "discard", nextRingfenceId: selectedIdFromUrl });
      return;
    }

    setSelectedId(selectedIdFromUrl);
  }, [confirmation, formMode, isAddingAssets, isSaving, ringfences, selectedId, selectedIdFromUrl]);

  useEffect(() => {
    if (selectedId === null) {
      setDetail(null);
      setDetailError(false);
      return;
    }
    void loadDetail(selectedId);
  }, [loadDetail, selectedId]);

  useEffect(() => {
    let active = true;
    fetchDivisions()
      .then((divisions) => {
        if (active) setDivisionOptions(divisions);
      })
      .catch(() => {
        if (active) setDivisionOptions([]);
      });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!formMode || !formDivisionQuery) {
      setWarehouseOptions([]);
      setOwnerOptions([]);
      setDependentLookupsError(false);
      return;
    }

    let active = true;
    setAreDependentLookupsLoading(true);
    setDependentLookupsError(false);

    Promise.all([fetchWarehouses(formDivisionQuery), fetchUsers(formDivisionQuery)])
      .then(([warehouses, users]) => {
        if (!active) return;
        const scopedWarehouses = filterWarehousesByDivision(warehouses, formDivisionCodes);
        setWarehouseOptions(scopedWarehouses);
        setOwnerOptions(users);
        setForm((current) => {
          if (!sameDivisionSelection(current.divisions, formDivisionQuery)) return current;

          // Historic values can disappear from an up-to-date lookup. Preserve them
          // while an existing record keeps the same division scope; the API applies
          // the same compatibility rule until a replacement is chosen.
          const preservesExistingScope =
            formMode === "edit" &&
            editBaselineRef.current !== null &&
            sameDivisionSelection(current.divisions, editBaselineRef.current.divisions);
          const matchedWarehouse = scopedWarehouses.find((warehouse) =>
            sameText(warehouse.warehouseCode, current.warehouse ?? ""),
          );
          const warehouse =
            matchedWarehouse && isRingfenceWarehouse(matchedWarehouse)
              ? matchedWarehouse.warehouseCode
              : preservesExistingScope ||
                  isValidRingfenceWarehouse(current.warehouse, scopedWarehouses, formDivisionCodes)
                ? current.warehouse
                : "";
          const owner =
            preservesExistingScope || isEligibleRingfenceOwner(current.owner, users, user?.loginName)
              ? current.owner
              : "";

          return warehouse === current.warehouse && owner === current.owner
            ? current
            : { ...current, warehouse, owner };
        });
      })
      .catch(() => {
        if (!active) return;
        setWarehouseOptions([]);
        setOwnerOptions([]);
        setDependentLookupsError(true);
      })
      .finally(() => {
        if (active) setAreDependentLookupsLoading(false);
      });

    return () => {
      active = false;
    };
  }, [formDivisionCodes, formDivisionQuery, formMode, user?.loginName]);

  useEffect(() => {
    if (formMode !== "create" || !user?.loginName) return;
    setForm((current) => (current.owner ? current : { ...current, owner: user.loginName }));
  }, [formMode, user?.loginName]);

  const hasMountedListFiltersRef = useRef(false);
  useEffect(() => {
    if (!hasMountedListFiltersRef.current) {
      hasMountedListFiltersRef.current = true;
      return;
    }
    setListPage(1);
  }, [divisionFilter, searchTerm, statusFilter, warehouseFilter]);

  const hasMountedAssetFiltersRef = useRef(false);
  useEffect(() => {
    if (!hasMountedAssetFiltersRef.current) {
      hasMountedAssetFiltersRef.current = true;
      return;
    }
    setAssetPage(1);
  }, [assetSearch, assetSortDirection, assetSortField, assetStatusFilter]);

  const hasResolvedInitialSelectionRef = useRef(false);
  useEffect(() => {
    if (!hasResolvedInitialSelectionRef.current && selectedId !== null) {
      hasResolvedInitialSelectionRef.current = true;
      return;
    }
    if (hasResolvedInitialSelectionRef.current) setAssetPage(1);
  }, [selectedId]);

  const captureWorkingState = useCallback(
    (): RingfencePageState => ({
      stateVersion: 1,
      searchTerm,
      statusFilter,
      divisionFilter,
      warehouseFilter,
      listSortField,
      listSortDirection,
      listPage,
      assetSearch,
      assetStatusFilter,
      assetSortField,
      assetSortDirection,
      assetPage,
    }),
    [
      assetPage,
      assetSearch,
      assetSortDirection,
      assetSortField,
      assetStatusFilter,
      divisionFilter,
      listPage,
      listSortDirection,
      listSortField,
      searchTerm,
      statusFilter,
      warehouseFilter,
    ],
  );

  const mountedUserIdentifierRef = useRef(user?.loginName);
  const workingStateRef = useRef({ userIdentifier: mountedUserIdentifierRef.current, state: captureWorkingState() });
  useLayoutEffect(() => {
    workingStateRef.current = { userIdentifier: mountedUserIdentifierRef.current, state: captureWorkingState() };
  }, [captureWorkingState]);

  useEffect(
    () => () => {
      writeSessionPageState(workingStateRef.current.userIdentifier, "ringfence", workingStateRef.current.state);
    },
    [],
  );

  useEffect(() => {
    const timeout = window.setTimeout(
      () => writeSessionPageState(mountedUserIdentifierRef.current, "ringfence", captureWorkingState()),
      250,
    );
    return () => window.clearTimeout(timeout);
  }, [captureWorkingState]);

  const selectedListItem = ringfences.find((ringfence) => ringfence.id === selectedId) ?? null;
  const selectedDetail = detail?.ringfence.id === selectedId ? detail : null;
  const isEditing = formMode === "edit";
  const isCreateMode = formMode === "create";
  const allowBlankWarehouse = formMode === "edit" && !selectedDetail?.ringfence.warehouse?.trim();
  const isSelectionLocked = isAddingAssets || isSaving || (confirmation !== null && confirmation.kind !== "discard");

  const filteredRingfences = useMemo(
    () =>
      filterRingfences(ringfences, {
        searchTerm,
        statusFilter,
        divisionFilter,
        warehouseFilter,
        listSortField,
        listSortDirection,
      }),
    [divisionFilter, listSortDirection, listSortField, ringfences, searchTerm, statusFilter, warehouseFilter],
  );

  const totalListPages = Math.max(1, Math.ceil(filteredRingfences.length / RINGFENCE_PAGE_SIZE));
  useEffect(() => {
    if (!isListLoading) setListPage((page) => Math.min(page, totalListPages));
  }, [isListLoading, totalListPages]);
  const visibleRingfences = filteredRingfences.slice(
    (listPage - 1) * RINGFENCE_PAGE_SIZE,
    listPage * RINGFENCE_PAGE_SIZE,
  );

  const availableWarehouseFilters = useMemo(
    () =>
      Array.from(
        new Set(
          ringfences
            .map((ringfence) => ringfence.warehouse)
            .filter((warehouse): warehouse is string => Boolean(warehouse)),
        ),
      ).sort(compareText),
    [ringfences],
  );

  const filteredAssets = useMemo(
    () =>
      filterRingfenceAssets(selectedDetail?.assets ?? [], {
        assetSearch,
        assetStatusFilter,
        assetSortField,
        assetSortDirection,
      }),
    [assetSearch, assetSortDirection, assetSortField, assetStatusFilter, selectedDetail],
  );

  const totalAssetPages = Math.max(1, Math.ceil(filteredAssets.length / ASSET_PAGE_SIZE));
  useEffect(() => {
    if (!isDetailLoading && selectedDetail) setAssetPage((page) => Math.min(page, totalAssetPages));
  }, [isDetailLoading, selectedDetail, totalAssetPages]);
  const visibleAssets = filteredAssets.slice((assetPage - 1) * ASSET_PAGE_SIZE, assetPage * ASSET_PAGE_SIZE);
  const assetStatusOptions = useMemo(
    () =>
      Array.from(
        new Set(
          (selectedDetail?.assets ?? [])
            .map((asset) => asset.status)
            .filter((status): status is string => Boolean(status)),
        ),
      ).sort(compareText),
    [selectedDetail?.assets],
  );

  const formErrors = isFormSubmitted ? validateForm(form, t, allowBlankWarehouse) : {};

  const updateUrlSelection = useCallback(
    (id: number) => {
      pendingUrlSelectionRef.current = id;
      const next = new URLSearchParams(searchParams);
      next.set("ringfenceId", String(id));
      setSearchParams(next, { replace: true });
    },
    [searchParams, setSearchParams],
  );

  useEffect(() => {
    if (selectedId === null || selectedIdFromUrl === null || selectedIdFromUrl === selectedId) return;
    if (ringfences.some((ringfence) => ringfence.id === selectedIdFromUrl)) return;
    updateUrlSelection(selectedId);
  }, [ringfences, selectedId, selectedIdFromUrl, updateUrlSelection]);

  const selectRingfence = (id: number) => {
    if (id === selectedId) return;
    if (isSelectionLocked) return;
    if (formMode) {
      setConfirmation({ kind: "discard", nextRingfenceId: id });
      return;
    }
    setSelectedId(id);
    updateUrlSelection(id);
  };

  const beginCreate = () => {
    editBaselineRef.current = null;
    setForm({ ...EMPTY_FORM, owner: user?.loginName ?? "" });
    setFormMode("create");
    setEditingRingfenceId(null);
    setIsFormSubmitted(false);
    setServerErrors({});
    setNotice(null);
  };

  const beginEdit = () => {
    if (!selectedDetail) return;
    editBaselineRef.current = { divisions: selectedDetail.ringfence.divisions };
    setForm({
      title: selectedDetail.ringfence.title,
      fromDate: toDateInput(selectedDetail.ringfence.fromDate),
      toDate: toDateInput(selectedDetail.ringfence.toDate),
      divisions: selectedDetail.ringfence.divisions,
      warehouse: selectedDetail.ringfence.warehouse ?? "",
      owner: selectedDetail.ringfence.owner?.trim() || user?.loginName || "",
    });
    setEditingRingfenceId(selectedDetail.ringfence.id);
    setFormMode("edit");
    setIsFormSubmitted(false);
    setServerErrors({});
    setNotice(null);
  };

  const cancelEditor = () => {
    editBaselineRef.current = null;
    setFormMode(null);
    setEditingRingfenceId(null);
    setIsFormSubmitted(false);
    setServerErrors({});
  };

  const updateField = <K extends keyof RingfenceInput>(key: K, value: RingfenceInput[K]) => {
    setForm((current) => ({ ...current, [key]: value }));
    setServerErrors((current) => {
      const next = { ...current };
      delete next[key];
      return next;
    });
  };

  const updateDivisions = (codes: string[]) => {
    if (!canEditFormDivisions) return;
    const divisions = splitDivisions(codes.join(","));
    const nextDivisionSelection = divisions.join(",");
    setForm((current) => {
      const scopeChanged = !sameDivisionSelection(current.divisions, nextDivisionSelection);
      const ownerIsEligible = isEligibleRingfenceOwner(current.owner, ownerOptions, user?.loginName);

      return {
        ...current,
        divisions: nextDivisionSelection,
        owner: !scopeChanged || ownerIsEligible ? current.owner : formMode === "create" ? (user?.loginName ?? "") : "",
        warehouse:
          !scopeChanged || isValidRingfenceWarehouse(current.warehouse, warehouseOptions, divisions)
            ? current.warehouse
            : "",
      };
    });
    setServerErrors((current) => {
      const next = { ...current };
      delete next.divisions;
      delete next.owner;
      delete next.warehouse;
      return next;
    });
  };

  const saveRingfence = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsFormSubmitted(true);
    const clientErrors = validateForm(form, t, allowBlankWarehouse, isCreateMode);
    if (Object.keys(clientErrors).length > 0) {
      focusFirstInvalidField(clientErrors);
      return;
    }

    setIsSaving(true);
    setServerErrors({});
    try {
      const payload = cleanForm(form);
      const saved =
        formMode === "edit" && editingRingfenceId !== null
          ? await updateRingfence(editingRingfenceId, payload)
          : await createRingfence(payload);

      setSelectedId(saved.id);
      updateUrlSelection(saved.id);
      await Promise.all([loadRingfences(saved.id), loadDetail(saved.id)]);
      setNotice({ variant: "success", message: formMode === "edit" ? t("ringfence.updated") : t("ringfence.created") });
      cancelEditor();
    } catch (error) {
      const nextServerErrors = getServerErrors(error);
      setServerErrors(nextServerErrors);
      setNotice({ variant: "error", message: getErrorMessage(error, t("ringfence.saveError")) });
      if (Object.keys(nextServerErrors).length > 0) focusFirstInvalidField(nextServerErrors);
    } finally {
      setIsSaving(false);
    }
  };

  const deleteSelectedRingfence = async (ringfence: RingfenceDetail) => {
    setIsSaving(true);
    try {
      await deleteRingfence(ringfence.ringfence.id);
      setDetail(null);
      setSelectedId(null);
      await loadRingfences(null);
      setNotice({ variant: "success", message: t("ringfence.deleted") });
    } catch (error) {
      setNotice({ variant: "error", message: getErrorMessage(error, t("ringfence.deleteError")) });
    } finally {
      setIsSaving(false);
    }
  };

  const removeSelectedAsset = async (ringfenceId: number, asset: RingfenceAsset) => {
    setIsAddingAssets(true);
    try {
      await removeAssetFromRingfence(ringfenceId, asset.id);
      await Promise.all([loadDetail(ringfenceId), loadRingfences(ringfenceId)]);
      setNotice({ variant: "success", message: t("ringfence.assetRemoved", { assetId: asset.id }) });
    } catch (error) {
      setNotice({ variant: "error", message: getErrorMessage(error, t("ringfence.removeAssetError")) });
    } finally {
      setIsAddingAssets(false);
    }
  };

  const applyAssetBatch = async (ringfenceId: number, ids: string[], acknowledgeOverlaps = false) => {
    const result = await addRingfenceItems(ringfenceId, ids, acknowledgeOverlaps);
    await Promise.all([loadDetail(ringfenceId), loadRingfences(ringfenceId)]);

    if (result.addedAssetIds.length > 0) {
      setAssetIds("");
      setIsAssetEntryOpen(false);
      setNotice({
        variant: "success",
        message:
          result.alreadyAssignedAssetIds.length > 0
            ? t("ringfence.assetsAddedWithExisting", {
                count: result.addedAssetIds.length,
                existing: result.alreadyAssignedAssetIds.length,
              })
            : t("ringfence.assetsAdded", { count: result.addedAssetIds.length }),
      });
      return;
    }

    if (result.alreadyAssignedAssetIds.length > 0) {
      setNotice({
        variant: "info",
        message: t("ringfence.assetsAlreadyAssigned", { count: result.alreadyAssignedAssetIds.length }),
      });
      return;
    }

    setNotice({ variant: "info", message: t("ringfence.noAssetsAdded") });
  };

  const addAssetIds = async (ringfenceId: number, ids: string[], acknowledgeOverlaps = false) => {
    setIsAddingAssets(true);
    try {
      await applyAssetBatch(ringfenceId, ids, acknowledgeOverlaps);
    } catch (error) {
      const batchResult = getRingfenceItemBatchResult(error);
      if (batchResult?.requiresOverlapAcknowledgement && batchResult.overlaps.length > 0) {
        setConfirmation({ kind: "overlap", ringfenceId, assetIds: ids, overlaps: batchResult.overlaps });
      } else if (batchResult?.unavailableAssetIds.length) {
        setNotice({
          variant: "error",
          message: t("ringfence.unavailableAssets", { assets: batchResult.unavailableAssetIds.join(", ") }),
        });
      } else {
        setNotice({ variant: "error", message: getErrorMessage(error, t("ringfence.addAssetError")) });
      }
    } finally {
      setIsAddingAssets(false);
    }
  };

  const requestAddAssets = async () => {
    if (!selectedDetail || isAddingAssets) return;
    const ids = parseAssetIds(assetIds);
    if (ids.length === 0) return;
    if (ids.length > MAX_BATCH_ASSET_IDS) {
      setNotice({ variant: "error", message: t("ringfence.maxAssetIds", { count: MAX_BATCH_ASSET_IDS }) });
      return;
    }

    setIsAddingAssets(true);
    try {
      const result = await preflightRingfenceItems(selectedDetail.ringfence.id, ids);
      if (result.unavailableAssetIds.length > 0) {
        setNotice({
          variant: "error",
          message: t("ringfence.unavailableAssets", { assets: result.unavailableAssetIds.join(", ") }),
        });
        return;
      }
      if (result.readyAssetIds.length === 0) {
        setNotice({
          variant: "info",
          message: t("ringfence.assetsAlreadyAssigned", { count: result.alreadyAssignedAssetIds.length }),
        });
        return;
      }
      if (result.requiresOverlapAcknowledgement) {
        setConfirmation({
          kind: "overlap",
          ringfenceId: selectedDetail.ringfence.id,
          assetIds: result.readyAssetIds,
          overlaps: result.overlaps,
        });
        return;
      }

      await applyAssetBatch(selectedDetail.ringfence.id, result.readyAssetIds);
    } catch (error) {
      setNotice({ variant: "error", message: getErrorMessage(error, t("ringfence.addAssetError")) });
    } finally {
      setIsAddingAssets(false);
    }
  };

  const chooseFromAssets = () => {
    if (!selectedDetail) return;
    const ringfenceId = String(selectedDetail.ringfence.id);
    const target = `/ringfence?${new URLSearchParams({ ringfenceId })}`;
    navigate(`/assets?${new URLSearchParams({ ringfenceId, returnTo: target })}`);
  };

  const confirmAction = () => {
    const action = confirmation;
    setConfirmation(null);
    if (!action) return;
    if (action.kind === "discard") {
      cancelEditor();
      setSelectedId(action.nextRingfenceId);
      updateUrlSelection(action.nextRingfenceId);
      return;
    }
    if (action.kind === "delete") {
      void deleteSelectedRingfence(action.ringfence);
      return;
    }
    if (action.kind === "remove") {
      void removeSelectedAsset(action.ringfenceId, action.asset);
      return;
    }
    void addAssetIds(action.ringfenceId, action.assetIds, true);
  };

  const toggleListSort = (field: ListSortField) => {
    if (field === listSortField) {
      setListSortDirection((direction) => (direction === "asc" ? "desc" : "asc"));
      return;
    }
    setListSortField(field);
    setListSortDirection("asc");
  };

  const toggleAssetSort = (field: AssetSortField) => {
    if (field === assetSortField) {
      setAssetSortDirection((direction) => (direction === "asc" ? "desc" : "asc"));
      return;
    }
    setAssetSortField(field);
    setAssetSortDirection("asc");
  };

  const clearFilters = () => {
    const clearedState: RingfencePageState = {
      ...captureWorkingState(),
      searchTerm: "",
      statusFilter: "",
      divisionFilter: "",
      warehouseFilter: "",
      listPage: 1,
      assetSearch: "",
      assetStatusFilter: "",
      assetPage: 1,
    };
    workingStateRef.current = { userIdentifier: mountedUserIdentifierRef.current, state: clearedState };
    clearSessionPageState(mountedUserIdentifierRef.current, "ringfence");
    setSearchTerm("");
    setStatusFilter("");
    setDivisionFilter("");
    setWarehouseFilter("");
    setAssetSearch("");
    setAssetStatusFilter("");
    setListPage(1);
    setAssetPage(1);
  };

  const listFilterActive = Boolean(searchTerm || statusFilter || divisionFilter || warehouseFilter);
  const currentAssetCount = selectedDetail?.assets.length ?? selectedListItem?.assetCount ?? 0;

  return (
    <div className={styles.page}>
      <header className={styles.pageHeader}>
        <div>
          <h1>{t("ringfence.titlePlural")}</h1>
        </div>
      </header>

      {notice && (
        <Alert variant={notice.variant} onDismiss={() => setNotice(null)}>
          {notice.message}
        </Alert>
      )}

      <div className={styles.workspace}>
        <Card className={styles.masterCard} bodyClassName={styles.panelBody}>
          <section className={styles.masterPanel} aria-labelledby="ringfence-register-heading">
            <header className={styles.panelHeader}>
              <div>
                <h2 id="ringfence-register-heading">{t("ringfence.register")}</h2>
                <p>{t("ringfence.resultCount", { count: filteredRingfences.length })}</p>
              </div>
              <div className={styles.panelActions} data-print-hidden>
                <Button
                  label={t("common.refresh")}
                  variant="secondary"
                  size="small"
                  onClick={() => void loadRingfences(selectedIdRef.current)}
                  disabled={isListLoading}
                />
                {canMutate && (
                  <Button
                    label={t("ringfence.create")}
                    size="small"
                    onClick={beginCreate}
                    disabled={Boolean(formMode)}
                  />
                )}
              </div>
            </header>
            <div className={styles.masterFilters} data-print-hidden>
              <label className={styles.searchField}>
                <span className={styles.srOnly}>{t("ringfence.search")}</span>
                <input
                  type="search"
                  value={searchTerm}
                  onChange={(event) => setSearchTerm(event.target.value)}
                  placeholder={t("ringfence.searchPlaceholder")}
                />
              </label>
              <select
                value={statusFilter}
                onChange={(event) => setStatusFilter(event.target.value)}
                aria-label={t("ringfence.filterStatus")}
              >
                <option value="">{t("ringfence.allStatuses")}</option>
                <option value="active">{t("ringfence.active")}</option>
                <option value="upcoming">{t("ringfence.upcoming")}</option>
                <option value="expired">{t("ringfence.expired")}</option>
              </select>
              <select
                value={divisionFilter}
                onChange={(event) => setDivisionFilter(event.target.value)}
                aria-label={t("ringfence.filterDivision")}
              >
                <option value="">{t("ringfence.allDivisions")}</option>
                {availableDivisionOptions.map((division) => (
                  <option key={division.code} value={division.code}>
                    {division.code} — {division.name}
                  </option>
                ))}
              </select>
              <select
                value={warehouseFilter}
                onChange={(event) => setWarehouseFilter(event.target.value)}
                aria-label={t("ringfence.filterWarehouse")}
              >
                <option value="">{t("ringfence.allWarehouses")}</option>
                {availableWarehouseFilters.map((warehouse) => (
                  <option key={warehouse} value={warehouse}>
                    {warehouse}
                  </option>
                ))}
              </select>
              <Button
                label={t("ringfence.clearFilters")}
                variant="ghost"
                size="small"
                onClick={clearFilters}
                disabled={!listFilterActive}
              />
            </div>

            {isListLoading ? (
              <div className={styles.loadingArea}>
                <Spinner />
              </div>
            ) : listError ? (
              <div className={styles.emptyState}>
                <p>{t("ringfence.loadError")}</p>
                <Button
                  label={t("common.retry")}
                  variant="secondary"
                  size="small"
                  onClick={() => void loadRingfences(selectedIdRef.current)}
                />
              </div>
            ) : filteredRingfences.length === 0 ? (
              <div className={styles.emptyState}>
                <p>{listFilterActive ? t("ringfence.noMatchingRingfences") : t("ringfence.empty")}</p>
                {canMutate && !listFilterActive && (
                  <Button
                    label={t("ringfence.create")}
                    size="small"
                    onClick={beginCreate}
                    disabled={Boolean(formMode)}
                  />
                )}
              </div>
            ) : (
              <>
                <div className={styles.masterTableWrap}>
                  <table className={styles.masterTable}>
                    <thead>
                      <tr>
                        <th>
                          <TableColumnHeader
                            label={t("ringfence.name")}
                            sortDirection={listSortField === "title" ? listSortDirection : undefined}
                            onSort={() => toggleListSort("title")}
                          />
                        </th>
                        <th>
                          <TableColumnHeader
                            label={t("ringfence.period")}
                            sortDirection={listSortField === "period" ? listSortDirection : undefined}
                            onSort={() => toggleListSort("period")}
                          />
                        </th>
                        <th className={styles.divisionColumn}>
                          <ResponsiveColumnLabel
                            fullLabel={t("ringfence.division")}
                            shortLabel={t("ringfence.divisionShort")}
                          />
                        </th>
                        <th className={styles.warehouseColumn}>
                          <ResponsiveColumnLabel
                            fullLabel={t("ringfence.warehouse")}
                            shortLabel={t("ringfence.warehouseShort")}
                          />
                        </th>
                        <th className={styles.assetsColumn}>
                          <TableColumnHeader
                            label={t("ringfence.assets")}
                            sortDirection={listSortField === "assets" ? listSortDirection : undefined}
                            onSort={() => toggleListSort("assets")}
                          />
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {visibleRingfences.map((ringfence) => {
                        const status = getRingfenceStatus(ringfence.fromDate, ringfence.toDate);
                        const isSelected = ringfence.id === selectedId;
                        return (
                          <tr key={ringfence.id} className={isSelected ? styles.selectedRow : undefined}>
                            <td>
                              <button
                                type="button"
                                className={styles.selectRingfenceButton}
                                onClick={() => selectRingfence(ringfence.id)}
                                aria-current={isSelected ? "page" : undefined}
                                disabled={isSelectionLocked}
                              >
                                <span>{ringfence.title}</span>
                                <span className={styles.rowSecondary}>{ringfence.owner ?? t("ringfence.notSet")}</span>
                              </button>
                            </td>
                            <td className={styles.periodColumn}>
                              <div className={styles.periodCell}>
                                <span>{formatDate(ringfence.fromDate)}</span>
                                <span>{formatDate(ringfence.toDate)}</span>
                              </div>
                            </td>
                            <td className={styles.divisionColumn}>
                              <span className={styles.contextValue}>{ringfence.divisions}</span>
                            </td>
                            <td className={styles.warehouseColumn}>
                              <span className={styles.contextValue}>
                                {ringfence.warehouse ?? t("ringfence.noWarehouse")}
                              </span>
                            </td>
                            <td className={styles.assetsColumn}>
                              <div className={styles.countCell}>
                                <strong>{ringfence.assetCount}</strong>
                                <Badge label={t("ringfence." + status)} variant={ringfenceStatusVariant(status)} />
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
                {totalListPages > 1 && (
                  <nav className={styles.pagination} aria-label={t("ringfence.ringfencePagination")} data-print-hidden>
                    <Button
                      label={t("common.previous")}
                      variant="secondary"
                      size="small"
                      onClick={() => setListPage((page) => Math.max(1, page - 1))}
                      disabled={listPage === 1}
                    />
                    <span>{t("ringfence.pageOf", { page: listPage, total: totalListPages })}</span>
                    <Button
                      label={t("common.next")}
                      variant="secondary"
                      size="small"
                      onClick={() => setListPage((page) => Math.min(totalListPages, page + 1))}
                      disabled={listPage === totalListPages}
                    />
                  </nav>
                )}
              </>
            )}
          </section>
        </Card>

        <Card className={styles.inspectorCard} bodyClassName={styles.panelBody}>
          <section className={styles.inspectorPanel} aria-labelledby="ringfence-inspector-heading">
            {canMutate && (isCreateMode || isEditing) ? (
              <RingfenceEditor
                form={form}
                errors={{ ...formErrors, ...serverErrors }}
                divisionOptions={availableDivisionOptions}
                formDivisionCodes={formDivisionCodes}
                canEditDivisions={canEditFormDivisions}
                warehouseOptions={selectableWarehouseOptions}
                ownerOptions={ownerOptions}
                isLoadingOptions={areDependentLookupsLoading}
                lookupsError={dependentLookupsError}
                isSaving={isSaving}
                isCreateMode={isCreateMode}
                allowBlankWarehouse={allowBlankWarehouse}
                minDate={isCreateMode ? todayInputValue() : undefined}
                onCancel={cancelEditor}
                onDivisionsChange={updateDivisions}
                onFieldChange={updateField}
                onSubmit={saveRingfence}
                t={t}
              />
            ) : selectedId === null ? (
              <div className={styles.emptyInspector}>
                <h2 id="ringfence-inspector-heading">{t("ringfence.selectTitle")}</h2>
                <p>{t("ringfence.selectHint")}</p>
              </div>
            ) : (
              <>
                <header className={styles.inspectorHeader}>
                  <div>
                    <div className={styles.inspectorTitleRow}>
                      <h2 id="ringfence-inspector-heading">{selectedListItem?.title ?? t("ringfence.selectTitle")}</h2>
                      {selectedListItem && (
                        <Badge
                          label={t(
                            "ringfence." + getRingfenceStatus(selectedListItem.fromDate, selectedListItem.toDate),
                          )}
                          variant={ringfenceStatusVariant(
                            getRingfenceStatus(selectedListItem.fromDate, selectedListItem.toDate),
                          )}
                        />
                      )}
                    </div>
                  </div>
                  {canMutate && (
                    <div className={styles.inspectorActions} data-print-hidden>
                      <Button
                        label={t("ringfence.editDetails")}
                        variant="secondary"
                        size="small"
                        onClick={beginEdit}
                        disabled={!selectedDetail || isDetailLoading}
                      />
                      <details className={styles.moreActions}>
                        <summary>{t("ringfence.moreActions")}</summary>
                        <Button
                          label={t("common.delete")}
                          variant="danger"
                          size="small"
                          disabled={!selectedDetail || isSaving}
                          onClick={() =>
                            selectedDetail && setConfirmation({ kind: "delete", ringfence: selectedDetail })
                          }
                        />
                      </details>
                    </div>
                  )}
                </header>

                <div className={styles.factStrip}>
                  <Fact
                    label={t("ringfence.period")}
                    value={selectedListItem ? formatDateRange(selectedListItem.fromDate, selectedListItem.toDate) : "—"}
                  />
                  <Fact
                    label={t("ringfence.divisions")}
                    value={selectedDetail?.ringfence.divisions ?? selectedListItem?.divisions ?? "—"}
                  />
                  <Fact
                    label={t("ringfence.owner")}
                    value={selectedDetail?.ringfence.owner ?? selectedListItem?.owner ?? t("ringfence.notSet")}
                  />
                  <Fact
                    label={t("ringfence.warehouse")}
                    value={selectedDetail?.ringfence.warehouse ?? selectedListItem?.warehouse ?? t("ringfence.notSet")}
                  />
                  <Fact label={t("ringfence.assets")} value={String(currentAssetCount)} />
                </div>

                <RingfenceAssets
                  currentAssetCount={currentAssetCount}
                  canMutate={canMutate}
                  isAssetEntryOpen={isAssetEntryOpen}
                  setIsAssetEntryOpen={setIsAssetEntryOpen}
                  selectedDetail={selectedDetail}
                  isDetailLoading={isDetailLoading}
                  assetIds={assetIds}
                  setAssetIds={setAssetIds}
                  isAddingAssets={isAddingAssets}
                  requestAddAssets={requestAddAssets}
                  chooseFromAssets={chooseFromAssets}
                  detailError={detailError}
                  assetSearch={assetSearch}
                  setAssetSearch={setAssetSearch}
                  assetStatusFilter={assetStatusFilter}
                  setAssetStatusFilter={setAssetStatusFilter}
                  assetStatusOptions={assetStatusOptions}
                  assetSortField={assetSortField}
                  assetSortDirection={assetSortDirection}
                  toggleAssetSort={toggleAssetSort}
                  visibleAssets={visibleAssets}
                  totalAssetPages={totalAssetPages}
                  assetPage={assetPage}
                  setAssetPage={setAssetPage}
                  onRetry={() => {
                    if (selectedId) void loadDetail(selectedId);
                  }}
                  onRemoveAsset={(asset) => {
                    if (selectedDetail)
                      setConfirmation({ kind: "remove", ringfenceId: selectedDetail.ringfence.id, asset });
                  }}
                />
              </>
            )}
          </section>
        </Card>
      </div>

      <ConfirmationDialog
        open={confirmation !== null}
        title={confirmation ? getConfirmationTitle(confirmation, t) : ""}
        confirmLabel={confirmation ? getConfirmationButtonLabel(confirmation, t) : ""}
        cancelLabel={t("common.cancel")}
        confirmVariant={confirmation?.kind === "delete" || confirmation?.kind === "remove" ? "danger" : "primary"}
        onCancel={() => {
          if (confirmation?.kind === "discard" && selectedIdRef.current !== null) {
            updateUrlSelection(selectedIdRef.current);
          }
          setConfirmation(null);
        }}
        onConfirm={confirmAction}
      >
        {confirmation?.kind === "discard" && <p>{t("ringfence.discardChangesMessage")}</p>}
        {confirmation?.kind === "delete" && (
          <p>
            {t("ringfence.confirmDeleteDetailed", {
              title: confirmation.ringfence.ringfence.title,
              count: confirmation.ringfence.assets.length,
            })}
          </p>
        )}
        {confirmation?.kind === "remove" && (
          <p>{t("ringfence.confirmRemoveAsset", { assetId: confirmation.asset.id })}</p>
        )}
        {confirmation?.kind === "overlap" && (
          <>
            <p>{t("ringfence.confirmOverlapDetailed", { count: confirmation.assetIds.length })}</p>
            <div className={styles.overlapList}>
              {confirmation.overlaps.map((overlap) => (
                <div key={overlap.ringfenceId}>
                  <strong>{overlap.title}</strong>
                  <span>{formatDateRange(overlap.fromDate, overlap.toDate)}</span>
                  <span>{overlap.owner ?? t("ringfence.notSet")}</span>
                  <span>{overlap.assetIds.join(", ")}</span>
                </div>
              ))}
            </div>
          </>
        )}
      </ConfirmationDialog>
    </div>
  );
};

const ResponsiveColumnLabel: FC<{ fullLabel: string; shortLabel: string }> = ({ fullLabel, shortLabel }) => (
  <>
    <span className={styles.columnLabelFull}>{fullLabel}</span>
    <span className={styles.columnLabelShort} title={fullLabel}>
      {shortLabel}
    </span>
  </>
);

const Fact: FC<{ label: string; value: string }> = ({ label, value }) => (
  <div>
    <span>{label}</span>
    <strong>{value}</strong>
  </div>
);
