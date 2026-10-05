export const MAX_MULTI_VALUE_FILTER_VALUES = 100;

export type MultiValueTextFilter = string[];
export type MultiValueTextFilterDrafts<TField extends string> = Partial<Record<TField, string>>;
export type MultiValueTextFilterValues<TField extends string> = Partial<Record<TField, string | string[]>>;

export interface MultiValueTextFilterParseResult {
  values: MultiValueTextFilter;
  exceededLimit: boolean;
}

const VALUE_SEPARATOR = /[,;\r\n]+/;

export function parseMultiValueTextFilter(
  input: string,
  existingValues: readonly string[] = [],
  limit = MAX_MULTI_VALUE_FILTER_VALUES,
): MultiValueTextFilterParseResult {
  const values: string[] = [];
  const normalizedValues = new Set<string>();
  let exceededLimit = false;

  const addValue = (candidate: string) => {
    const value = candidate.trim();
    if (!value) return;
    const normalized = value.toLocaleLowerCase();
    if (normalizedValues.has(normalized)) return;
    if (values.length >= limit) {
      exceededLimit = true;
      return;
    }
    normalizedValues.add(normalized);
    values.push(value);
  };

  existingValues.forEach(addValue);
  input.split(VALUE_SEPARATOR).forEach(addValue);

  return { values, exceededLimit };
}

export function toMultiValueTextFilter(value: unknown): MultiValueTextFilter {
  if (typeof value === "string") return parseMultiValueTextFilter(value).values;
  if (!Array.isArray(value)) return [];
  return parseMultiValueTextFilter(value.filter((entry): entry is string => typeof entry === "string").join("\n"))
    .values;
}

export function applyMultiValueTextFilterDrafts<TField extends string>(
  filters: Readonly<MultiValueTextFilterValues<TField>>,
  drafts: Readonly<MultiValueTextFilterDrafts<TField>>,
): MultiValueTextFilterValues<TField> {
  const effectiveFilters: MultiValueTextFilterValues<TField> = { ...filters };

  for (const [field, draft] of Object.entries(drafts) as Array<[TField, string]>) {
    if (!draft.trim()) continue;
    effectiveFilters[field] = parseMultiValueTextFilter(draft, toMultiValueTextFilter(filters[field])).values;
  }

  return effectiveFilters;
}

export function matchesMultiValueTextFilter(
  value: string | number | null | undefined,
  filters: readonly string[],
): boolean {
  if (filters.length === 0) return true;
  if (value == null) return false;
  const candidate = String(value).trim().toLocaleLowerCase();
  return filters.some((filter) => {
    const normalizedFilter = filter.trim().toLocaleLowerCase();
    return candidate.includes(normalizedFilter);
  });
}
