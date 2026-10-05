export interface TableColumnDefinition<Key extends string = string> {
  key: Key;
  labelKey: string;
  compactLabelKey?: string;
  filterLabelKey?: string;
  defaultVisible: boolean;
  defaultWidth: number;
  minWidth: number;
  maxWidth: number;
  growWeight?: number;
  required?: boolean;
  locked?: boolean;
}

export interface TableColumnLayoutItem<Key extends string = string> {
  key: Key;
  visible: boolean;
  width: number;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function clampColumnWidth(width: number, definition: TableColumnDefinition): number {
  if (!Number.isFinite(width)) return definition.defaultWidth;
  return Math.min(definition.maxWidth, Math.max(definition.minWidth, Math.round(width)));
}

export function repairTableColumnLayout<Key extends string>(
  catalogue: readonly TableColumnDefinition<Key>[],
  value?: unknown,
): TableColumnLayoutItem<Key>[] {
  const definitions = new Map(catalogue.map((column) => [column.key, column]));
  const seen = new Set<Key>();
  const repaired: TableColumnLayoutItem<Key>[] = [];

  if (Array.isArray(value)) {
    value.forEach((candidate) => {
      if (!isRecord(candidate) || typeof candidate.key !== "string" || seen.has(candidate.key as Key)) return;
      const definition = definitions.get(candidate.key as Key);
      if (!definition) return;
      seen.add(definition.key);
      repaired.push({
        key: definition.key,
        visible: definition.required
          ? true
          : typeof candidate.visible === "boolean"
            ? candidate.visible
            : definition.defaultVisible,
        width: clampColumnWidth(
          typeof candidate.width === "number" ? candidate.width : definition.defaultWidth,
          definition,
        ),
      });
    });
  }

  catalogue.forEach((definition) => {
    if (seen.has(definition.key)) return;
    repaired.push({
      key: definition.key,
      visible: definition.required ? true : definition.defaultVisible,
      width: definition.defaultWidth,
    });
  });

  const locked = catalogue.filter((column) => column.locked).map((column) => column.key);
  locked.reverse().forEach((key) => {
    const index = repaired.findIndex((column) => column.key === key);
    if (index >= 0) repaired.unshift(...repaired.splice(index, 1));
  });

  return repaired;
}

export function visibleTableColumns<Key extends string>(layout: readonly TableColumnLayoutItem<Key>[]) {
  return layout.filter((column) => column.visible);
}

export function limitVisibleTableColumns<Key extends string>(
  catalogue: readonly TableColumnDefinition<Key>[],
  layout: readonly TableColumnLayoutItem<Key>[],
  maximumVisible: number,
): TableColumnLayoutItem<Key>[] {
  const requiredKeys = new Set(catalogue.filter((column) => column.required).map((column) => column.key));
  const requiredVisibleCount = layout.filter((column) => column.visible && requiredKeys.has(column.key)).length;
  let optionalSlots = Math.max(0, Math.floor(maximumVisible) - requiredVisibleCount);

  return layout.map((column) => {
    if (!column.visible || requiredKeys.has(column.key)) return { ...column };
    if (optionalSlots > 0) {
      optionalSlots -= 1;
      return { ...column };
    }
    return { ...column, visible: false };
  });
}

export function getTableMinimumWidth<Key extends string>(
  catalogue: readonly TableColumnDefinition<Key>[],
  layout: readonly TableColumnLayoutItem<Key>[],
): number {
  const definitionByKey = new Map(catalogue.map((column) => [column.key, column]));
  return visibleTableColumns(layout).reduce(
    (total, column) => total + (definitionByKey.get(column.key)?.minWidth ?? column.width),
    0,
  );
}

export function fitTableColumns<Key extends string>(
  catalogue: readonly TableColumnDefinition<Key>[],
  layout: readonly TableColumnLayoutItem<Key>[],
  availableWidth: number,
): TableColumnLayoutItem<Key>[] {
  const definitionByKey = new Map(catalogue.map((column) => [column.key, column]));
  const columns = visibleTableColumns(layout).map((column) => {
    const definition = definitionByKey.get(column.key);
    return {
      ...column,
      width: definition ? clampColumnWidth(column.width, definition) : column.width,
    };
  });
  if (columns.length === 0 || !Number.isFinite(availableWidth) || availableWidth <= 0) return columns;

  const minimumWidths = columns.map((column) => definitionByKey.get(column.key)?.minWidth ?? Math.max(0, column.width));
  const preferredWidths = columns.map((column) => column.width);
  const minimumTotal = sumWidths(minimumWidths);
  const preferredTotal = sumWidths(preferredWidths);
  const targetWidth = Math.max(minimumTotal, Math.floor(availableWidth));

  if (targetWidth < preferredTotal) {
    const widths = distributeWidth(
      minimumWidths,
      preferredWidths.map((width, index) => width - minimumWidths[index]),
      preferredWidths.map((width, index) => width - minimumWidths[index]),
      targetWidth - minimumTotal,
    );
    return columns.map((column, index) => ({ ...column, width: widths[index] }));
  }

  if (targetWidth > preferredTotal) {
    const growthCapacity = columns.map((column) => {
      const definition = definitionByKey.get(column.key);
      return Math.max(0, (definition?.maxWidth ?? column.width) - column.width);
    });
    const growthWeights = columns.map((column, index) => {
      const definition = definitionByKey.get(column.key);
      return growthCapacity[index] > 0 ? Math.max(0.01, definition?.growWeight ?? 1) : 0;
    });
    const widths = distributeWidth(preferredWidths, growthCapacity, growthWeights, targetWidth - preferredTotal);
    return columns.map((column, index) => ({ ...column, width: widths[index] }));
  }

  return columns;
}

function sumWidths(widths: readonly number[]): number {
  return widths.reduce((total, width) => total + width, 0);
}

function distributeWidth(
  baseWidths: readonly number[],
  capacities: readonly number[],
  weights: readonly number[],
  requestedExtra: number,
): number[] {
  const widths = [...baseWidths];
  let remaining = Math.min(
    Math.max(0, requestedExtra),
    capacities.reduce((total, capacity) => total + Math.max(0, capacity), 0),
  );
  let active = capacities.map((_, index) => index).filter((index) => capacities[index] > 0 && weights[index] > 0);

  while (remaining > 0.001 && active.length > 0) {
    const totalWeight = active.reduce((total, index) => total + weights[index], 0);
    let distributed = 0;
    active.forEach((index) => {
      const capacityLeft = baseWidths[index] + capacities[index] - widths[index];
      const allocation = Math.min(capacityLeft, (remaining * weights[index]) / totalWeight);
      widths[index] += allocation;
      distributed += allocation;
    });
    if (distributed <= 0.001) break;
    remaining -= distributed;
    active = active.filter((index) => baseWidths[index] + capacities[index] - widths[index] > 0.001);
  }

  const rounded = widths.map(Math.floor);
  let pixelsLeft = Math.min(
    Math.round(sumWidths(widths)) - sumWidths(rounded),
    capacities.reduce(
      (total, capacity, index) => total + Math.max(0, baseWidths[index] + capacity - rounded[index]),
      0,
    ),
  );
  const remainderOrder = widths
    .map((width, index) => ({ index, remainder: width - Math.floor(width) }))
    .sort((left, right) => right.remainder - left.remainder || left.index - right.index);
  for (const { index } of remainderOrder) {
    if (pixelsLeft <= 0) break;
    if (rounded[index] >= baseWidths[index] + capacities[index]) continue;
    rounded[index] += 1;
    pixelsLeft -= 1;
  }
  return rounded;
}

export function getTableColumnPrintWidth(width: number, totalWidth: number): string {
  if (!Number.isFinite(width) || width <= 0 || !Number.isFinite(totalWidth) || totalWidth <= 0) return "auto";
  return `${((width / totalWidth) * 100).toFixed(6)}%`;
}

export function setTableColumnVisibility<Key extends string>(
  catalogue: readonly TableColumnDefinition<Key>[],
  layout: readonly TableColumnLayoutItem<Key>[],
  key: Key,
  visible: boolean,
): TableColumnLayoutItem<Key>[] {
  const definition = catalogue.find((column) => column.key === key);
  if (!definition || (definition.required && !visible)) return [...layout];
  return layout.map((column) => (column.key === key ? { ...column, visible } : column));
}

export function setTableColumnWidth<Key extends string>(
  catalogue: readonly TableColumnDefinition<Key>[],
  layout: readonly TableColumnLayoutItem<Key>[],
  key: Key,
  width: number,
): TableColumnLayoutItem<Key>[] {
  const definition = catalogue.find((column) => column.key === key);
  if (!definition) return [...layout];
  return layout.map((column) =>
    column.key === key ? { ...column, width: clampColumnWidth(width, definition) } : column,
  );
}

export function moveTableColumn<Key extends string>(
  catalogue: readonly TableColumnDefinition<Key>[],
  layout: readonly TableColumnLayoutItem<Key>[],
  key: Key,
  direction: "left" | "right",
): TableColumnLayoutItem<Key>[] {
  const definition = catalogue.find((column) => column.key === key);
  if (!definition || definition.locked) return [...layout];
  const next = [...layout];
  const index = next.findIndex((column) => column.key === key);
  if (index < 0) return next;
  const step = direction === "left" ? -1 : 1;
  let target = index + step;
  while (target >= 0 && target < next.length && !next[target].visible) target += step;
  if (target < 0 || target >= next.length) return next;
  const targetDefinition = catalogue.find((column) => column.key === next[target].key);
  if (targetDefinition?.locked) return next;
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}

export function moveTableColumnToVisibleIndex<Key extends string>(
  catalogue: readonly TableColumnDefinition<Key>[],
  layout: readonly TableColumnLayoutItem<Key>[],
  key: Key,
  targetIndex: number,
): TableColumnLayoutItem<Key>[] {
  const definitionByKey = new Map(catalogue.map((column) => [column.key, column]));
  const sourceDefinition = definitionByKey.get(key);
  const visibleColumns = visibleTableColumns(layout);
  const sourceIndex = visibleColumns.findIndex((column) => column.key === key);
  if (!sourceDefinition || sourceDefinition.locked || sourceIndex < 0) return [...layout];

  const lockedPrefixLength = visibleColumns.findIndex((column) => !definitionByKey.get(column.key)?.locked);
  const firstMovableIndex = lockedPrefixLength < 0 ? visibleColumns.length : lockedPrefixLength;
  const nextIndex = Math.min(visibleColumns.length - 1, Math.max(firstMovableIndex, Math.round(targetIndex)));
  if (nextIndex === sourceIndex) return [...layout];

  const reorderedVisible = [...visibleColumns];
  const [moved] = reorderedVisible.splice(sourceIndex, 1);
  reorderedVisible.splice(nextIndex, 0, moved);
  let visibleIndex = 0;
  return layout.map((column) => (column.visible ? reorderedVisible[visibleIndex++] : column));
}
