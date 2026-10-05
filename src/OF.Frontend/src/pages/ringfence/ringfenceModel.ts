import type { UserLookup, WarehouseLookup } from "../../services/lookupsService";
import type { RingfenceAsset, RingfenceInput, RingfenceListItem } from "../../services/ringfenceService";

export interface WarehouseOptionGroup {
  divisionCode: string;
  label: string;
  warehouses: WarehouseLookup[];
}

export type ListSortField = "title" | "period" | "assets";
export type AssetSortField = "id" | "status" | "description";
export type SortDirection = "asc" | "desc";
export type FormErrors = Partial<Record<keyof RingfenceInput, string>>;

export interface RingfencePageState {
  stateVersion: 1;
  searchTerm: string;
  statusFilter: string;
  divisionFilter: string;
  warehouseFilter: string;
  listSortField: ListSortField;
  listSortDirection: SortDirection;
  listPage: number;
  assetSearch: string;
  assetStatusFilter: string;
  assetSortField: AssetSortField;
  assetSortDirection: SortDirection;
  assetPage: number;
}

export const EMPTY_FORM: RingfenceInput = {
  title: "",
  fromDate: "",
  toDate: "",
  divisions: "",
  warehouse: "",
  owner: "",
};

export function parseRingfencePageState(value: unknown): RingfencePageState | null {
  if (!value || typeof value !== "object") return null;
  const state = value as Partial<RingfencePageState>;
  if (
    state.stateVersion !== 1 ||
    typeof state.searchTerm !== "string" ||
    typeof state.statusFilter !== "string" ||
    typeof state.divisionFilter !== "string" ||
    typeof state.warehouseFilter !== "string" ||
    !isListSortField(state.listSortField) ||
    !isSortDirection(state.listSortDirection) ||
    !isPositiveInteger(state.listPage) ||
    typeof state.assetSearch !== "string" ||
    typeof state.assetStatusFilter !== "string" ||
    !isAssetSortField(state.assetSortField) ||
    !isSortDirection(state.assetSortDirection) ||
    !isPositiveInteger(state.assetPage)
  ) {
    return null;
  }
  return state as RingfencePageState;
}

function isListSortField(value: unknown): value is ListSortField {
  return value === "title" || value === "period" || value === "assets";
}

function isAssetSortField(value: unknown): value is AssetSortField {
  return value === "id" || value === "status" || value === "description";
}

function isSortDirection(value: unknown): value is SortDirection {
  return value === "asc" || value === "desc";
}

function isPositiveInteger(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value > 0;
}

export function parseRingfenceId(value: string | null): number | null {
  if (!value) return null;
  const id = Number(value);
  return Number.isSafeInteger(id) && id > 0 ? id : null;
}

function normaliseDivisionCode(value: string): string {
  return value.trim().toLocaleUpperCase();
}

export function splitDivisions(value: string | null | undefined): string[] {
  return (value ?? "")
    .split(",")
    .map((division) => division.trim())
    .filter(Boolean);
}

export function filterWarehousesByDivision(
  warehouses: readonly WarehouseLookup[],
  divisions: readonly string[],
): WarehouseLookup[] {
  const selectedDivisions = new Set(divisions.map(normaliseDivisionCode).filter(Boolean));
  if (selectedDivisions.size === 0) return [];

  const seen = new Set<string>();
  return warehouses.filter((warehouse) => {
    const divisionCode = normaliseDivisionCode(warehouse.divisionCode);
    const identity = divisionCode + "\u0000" + warehouse.warehouseCode.trim().toLocaleUpperCase();
    if (!selectedDivisions.has(divisionCode) || seen.has(identity)) return false;
    seen.add(identity);
    return true;
  });
}

function isWarehouseInDivisions(
  warehouseCode: string | null | undefined,
  warehouses: readonly WarehouseLookup[],
  divisions: readonly string[],
): boolean {
  if (!warehouseCode?.trim()) return true;
  const selectedDivisions = new Set(divisions.map(normaliseDivisionCode).filter(Boolean));
  return warehouses.some(
    (warehouse) =>
      sameText(warehouse.warehouseCode, warehouseCode) &&
      selectedDivisions.has(normaliseDivisionCode(warehouse.divisionCode)),
  );
}

export function isRingfenceWarehouse(warehouse: Pick<WarehouseLookup, "warehouseCode">): boolean {
  return warehouse.warehouseCode.trim().toLocaleUpperCase().endsWith("0");
}

export function isValidRingfenceWarehouse(
  warehouseCode: string | null | undefined,
  warehouses: readonly WarehouseLookup[],
  divisions: readonly string[],
): boolean {
  return (
    isWarehouseInDivisions(warehouseCode, warehouses, divisions) &&
    (!warehouseCode?.trim() || isRingfenceWarehouse({ warehouseCode }))
  );
}

export function isEligibleRingfenceOwner(
  owner: string | null | undefined,
  owners: readonly UserLookup[],
  currentUserLoginName: string | undefined,
): boolean {
  if (!owner?.trim()) return true;
  return (
    Boolean(currentUserLoginName && sameText(owner, currentUserLoginName)) ||
    owners.some((candidate) => sameText(candidate.loginName, owner))
  );
}

export function sameDivisionSelection(first: string | null | undefined, second: string | null | undefined): boolean {
  const normalise = (value: string | null | undefined) =>
    Array.from(new Set(splitDivisions(value).map(normaliseDivisionCode))).sort(compareText);
  const firstCodes = normalise(first);
  const secondCodes = normalise(second);
  return firstCodes.length === secondCodes.length && firstCodes.every((code, index) => code === secondCodes[index]);
}

export function groupWarehousesByDivision(warehouses: readonly WarehouseLookup[]): WarehouseOptionGroup[] {
  const groups = new Map<string, WarehouseOptionGroup>();

  warehouses.forEach((warehouse) => {
    const divisionCode = normaliseDivisionCode(warehouse.divisionCode);
    if (!divisionCode) return;

    const existing = groups.get(divisionCode);
    if (existing) {
      existing.warehouses.push(warehouse);
      return;
    }

    groups.set(divisionCode, {
      divisionCode,
      label: warehouse.divisionName ? divisionCode + " — " + warehouse.divisionName : divisionCode,
      warehouses: [warehouse],
    });
  });

  return Array.from(groups.values()).sort((first, second) => compareText(first.divisionCode, second.divisionCode));
}

export function parseAssetIds(value: string): string[] {
  return Array.from(
    new Set(
      value
        .split(/[\n,\s]+/)
        .map((assetId) => assetId.trim().toUpperCase())
        .filter(Boolean),
    ),
  );
}

export function cleanForm(form: RingfenceInput): RingfenceInput {
  return {
    ...form,
    title: form.title.trim(),
    divisions: splitDivisions(form.divisions).join(","),
    owner: form.owner?.trim(),
    warehouse: form.warehouse?.trim(),
  };
}

export function validateForm(
  form: RingfenceInput,
  t: (key: string) => string,
  allowBlankWarehouse = false,
  requiresSuffixZeroWarehouse = false,
): FormErrors {
  const errors: FormErrors = {};
  if (!form.title.trim()) errors.title = t("ringfence.nameRequired");
  if (!form.fromDate) errors.fromDate = t("ringfence.datesRequired");
  if (!form.toDate) errors.toDate = t("ringfence.datesRequired");
  if (form.fromDate && form.toDate && form.toDate < form.fromDate) errors.toDate = t("ringfence.invalidDates");
  if (!splitDivisions(form.divisions).length) errors.divisions = t("ringfence.divisionsRequired");
  if (!form.owner?.trim()) errors.owner = t("ringfence.ownerRequired");
  if (!allowBlankWarehouse && !form.warehouse?.trim()) errors.warehouse = t("ringfence.warehouseRequired");
  if (
    requiresSuffixZeroWarehouse &&
    form.warehouse?.trim() &&
    !isRingfenceWarehouse({ warehouseCode: form.warehouse })
  ) {
    errors.warehouse = t("ringfence.warehouseMustEndInZero");
  }
  return errors;
}

export function toDateInput(value: string): string {
  return value.slice(0, 10);
}

function calendarDate(value: string): Date {
  const [year, month, day] = value.slice(0, 10).split("-").map(Number);
  return new Date(year, month - 1, day);
}

export function formatDate(value: string): string {
  const date = calendarDate(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

export function formatDateRange(fromDate: string, toDate: string): string {
  return formatDate(fromDate) + " – " + formatDate(toDate);
}

export function todayInputValue(): string {
  const today = new Date();
  return (
    String(today.getFullYear()) +
    "-" +
    String(today.getMonth() + 1).padStart(2, "0") +
    "-" +
    String(today.getDate()).padStart(2, "0")
  );
}

export function getRingfenceStatus(fromDate: string, toDate: string): "active" | "upcoming" | "expired" {
  const today = calendarDate(todayInputValue());
  if (calendarDate(fromDate) > today) return "upcoming";
  return calendarDate(toDate) >= today ? "active" : "expired";
}

function getRingfenceStatusRank(fromDate: string, toDate: string): number {
  const status = getRingfenceStatus(fromDate, toDate);
  if (status === "active") return 0;
  if (status === "upcoming") return 1;
  return 2;
}

export function ringfenceStatusVariant(status: "active" | "upcoming" | "expired"): "success" | "warning" | "neutral" {
  if (status === "active") return "success";
  if (status === "upcoming") return "warning";
  return "neutral";
}

export function assetStatusVariant(status: string | null): "success" | "warning" | "error" | "info" | "neutral" {
  if (status === "Available") return "success";
  if (status === "OnHire" || status === "In Transit") return "info";
  if (status === "Assess" || status === "Service" || status === "Repair") return "warning";
  if (status === "RemovedStock" || status === "Scrap" || status === "Sold") return "error";
  return "neutral";
}

export function compareText(first: string, second: string): number {
  return first.localeCompare(second, undefined, { numeric: true, sensitivity: "base" });
}

export function sameText(first: string, second: string): boolean {
  return first.trim().localeCompare(second.trim(), undefined, { sensitivity: "accent" }) === 0;
}

export function getErrorMessage(error: unknown, fallback: string): string {
  if (typeof error !== "object" || error === null) return fallback;
  const candidate = error as {
    response?: { data?: { detail?: unknown; title?: unknown; message?: unknown } };
    message?: unknown;
  };
  if (typeof candidate.response?.data?.detail === "string") return candidate.response.data.detail;
  if (typeof candidate.response?.data?.message === "string") return candidate.response.data.message;
  if (typeof candidate.response?.data?.title === "string") return candidate.response.data.title;
  if (typeof candidate.message === "string") return candidate.message;
  return fallback;
}

export function getServerErrors(error: unknown): FormErrors {
  if (typeof error !== "object" || error === null) return {};
  const candidate = error as { response?: { data?: { errors?: Record<string, unknown> } } };
  const errors = candidate.response?.data?.errors;
  if (!errors) return {};

  const nextErrors: FormErrors = {};
  (Object.keys(EMPTY_FORM) as Array<keyof RingfenceInput>).forEach((field) => {
    const key = field.charAt(0).toUpperCase() + field.slice(1);
    const value = errors[field] ?? errors[key];
    if (Array.isArray(value) && typeof value[0] === "string") nextErrors[field] = value[0];
  });
  return nextErrors;
}

export function filterRingfences(
  ringfences: readonly RingfenceListItem[],
  {
    searchTerm,
    statusFilter,
    divisionFilter,
    warehouseFilter,
    listSortField,
    listSortDirection,
  }: Pick<
    RingfencePageState,
    "searchTerm" | "statusFilter" | "divisionFilter" | "warehouseFilter" | "listSortField" | "listSortDirection"
  >,
) {
  const query = searchTerm.trim().toLocaleLowerCase();
  const matches = ringfences.filter((ringfence) => {
    const status = getRingfenceStatus(ringfence.fromDate, ringfence.toDate);
    const searchContent = [ringfence.title, ringfence.owner, ringfence.divisions, ringfence.warehouse]
      .filter(Boolean)
      .join(" ")
      .toLocaleLowerCase();

    return (
      (!query || searchContent.includes(query)) &&
      (!statusFilter || status === statusFilter) &&
      (!divisionFilter || splitDivisions(ringfence.divisions).some((division) => sameText(division, divisionFilter))) &&
      (!warehouseFilter || sameText(ringfence.warehouse ?? "", warehouseFilter))
    );
  });

  return matches.sort((first, second) => {
    const direction = listSortDirection === "asc" ? 1 : -1;
    if (listSortField === "period") {
      const statusDifference =
        getRingfenceStatusRank(first.fromDate, first.toDate) - getRingfenceStatusRank(second.fromDate, second.toDate);
      if (statusDifference !== 0) return statusDifference * direction;
      return compareText(first.fromDate, second.fromDate) * direction;
    }
    if (listSortField === "assets") return (first.assetCount - second.assetCount) * direction;
    return compareText(first.title, second.title) * direction;
  });
}

export function filterRingfenceAssets(
  sourceAssets: readonly RingfenceAsset[],
  {
    assetSearch,
    assetStatusFilter,
    assetSortField,
    assetSortDirection,
  }: Pick<RingfencePageState, "assetSearch" | "assetStatusFilter" | "assetSortField" | "assetSortDirection">,
) {
  const query = assetSearch.trim().toLocaleLowerCase();
  const assets = sourceAssets.filter((asset) => {
    const content = [
      asset.id,
      asset.description,
      asset.itemNumber,
      asset.warehouse,
      asset.warehouseLocation,
      asset.division,
    ]
      .filter(Boolean)
      .join(" ")
      .toLocaleLowerCase();
    return (!query || content.includes(query)) && (!assetStatusFilter || asset.status === assetStatusFilter);
  });

  return assets.sort((first, second) => {
    const direction = assetSortDirection === "asc" ? 1 : -1;
    if (assetSortField === "status") return compareText(first.status ?? "", second.status ?? "") * direction;
    if (assetSortField === "description")
      return compareText(first.description ?? "", second.description ?? "") * direction;
    return compareText(first.id, second.id) * direction;
  });
}
