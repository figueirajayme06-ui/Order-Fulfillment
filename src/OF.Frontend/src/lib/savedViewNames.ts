export interface NamedSavedView {
  id: string;
  name: string;
}

export function normalizeSavedViewName(name: string): string {
  return name.trim().toLowerCase();
}

export function isSavedViewNameTaken(views: readonly NamedSavedView[], name: string, excludedViewId?: string): boolean {
  const normalizedName = normalizeSavedViewName(name);
  if (!normalizedName) return false;

  return views.some((view) => view.id !== excludedViewId && normalizeSavedViewName(view.name) === normalizedName);
}
