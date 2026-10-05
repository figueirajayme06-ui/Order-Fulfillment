import { afterEach, describe, expect, it } from "vitest";
import { applyThemePreference, readThemePreference, storeThemePreference, THEME_STORAGE_KEY } from "./themePreference";

describe("themePreference", () => {
  afterEach(() => {
    window.localStorage.clear();
    delete document.documentElement.dataset.theme;
  });

  it("defaults invalid or missing preferences to light mode", () => {
    window.localStorage.setItem(THEME_STORAGE_KEY, "sepia");

    expect(readThemePreference()).toBe("light");
  });

  it("stores and reads dark mode", () => {
    storeThemePreference("dark");

    expect(readThemePreference()).toBe("dark");
  });

  it("stores and reads ATS theme", () => {
    storeThemePreference("ats");

    expect(readThemePreference()).toBe("ats");
  });

  it("applies the theme to the document root", () => {
    applyThemePreference("dark");

    expect(document.documentElement).toHaveAttribute("data-theme", "dark");
  });
});
