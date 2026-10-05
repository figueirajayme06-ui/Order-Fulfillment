export type ThemeMode = "light" | "dark" | "ats";

export const THEME_STORAGE_KEY = "of.theme";

export function readThemePreference(): ThemeMode {
  try {
    const storedTheme = window.localStorage.getItem(THEME_STORAGE_KEY);
    return storedTheme === "dark" || storedTheme === "ats" ? storedTheme : "light";
  } catch {
    return "light";
  }
}

export function storeThemePreference(theme: ThemeMode): void {
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // Theme switching remains available for the current session when storage is unavailable.
  }
}

export function applyThemePreference(theme: ThemeMode): void {
  document.documentElement.dataset.theme = theme;
}
