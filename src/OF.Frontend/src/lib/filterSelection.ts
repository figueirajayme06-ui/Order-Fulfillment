export function parseFilterSelection(value: unknown): string[] {
  const candidates = Array.isArray(value) ? value : typeof value === "string" ? value.split(",") : [];
  const selection: string[] = [];
  const normalized = new Set<string>();

  candidates.forEach((candidate) => {
    if (typeof candidate !== "string") return;
    const item = candidate.trim();
    const key = item.toLocaleLowerCase();
    if (!item || normalized.has(key)) return;
    normalized.add(key);
    selection.push(item);
  });

  return selection;
}

export function serializeFilterSelection(values: readonly string[]): string {
  return parseFilterSelection(values).join(",");
}

export function normalizeFilterSelection(value: unknown, allowedValues: readonly string[]): string {
  const selected = new Set(parseFilterSelection(value).map((item) => item.toLocaleLowerCase()));
  return allowedValues.filter((item) => selected.has(item.toLocaleLowerCase())).join(",");
}

export function haveSameFilterSelection(first: unknown, second: unknown): boolean {
  const firstValues = parseFilterSelection(first)
    .map((item) => item.toLocaleLowerCase())
    .sort();
  const secondValues = parseFilterSelection(second)
    .map((item) => item.toLocaleLowerCase())
    .sort();
  return firstValues.length === secondValues.length && firstValues.every((item, index) => item === secondValues[index]);
}

export function matchesExactFilterSelection(
  value: string | number | null | undefined,
  filters: readonly string[],
): boolean {
  if (filters.length === 0) return true;
  if (value == null) return false;
  const candidate = String(value).trim().toLocaleLowerCase();
  return filters.some((filter) => filter.trim().toLocaleLowerCase() === candidate);
}
