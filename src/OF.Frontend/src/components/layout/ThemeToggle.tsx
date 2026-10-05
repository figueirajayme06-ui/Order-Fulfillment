import type { FC } from "react";
import { useTranslation } from "react-i18next";
import { LuPalette } from "react-icons/lu";
import type { ThemeMode } from "./themePreference";
import styles from "./ThemeToggle.module.css";

interface Props {
  theme: ThemeMode;
  onChange: (theme: ThemeMode) => void;
  appearance?: "toolbar" | "sidebar";
  compact?: boolean;
}

const THEME_OPTIONS: ThemeMode[] = ["light", "dark", "ats"];

// Native <option> elements strip nested markup, so themes are distinguished with a plain glyph rather than an icon component.
const THEME_GLYPHS: Record<ThemeMode, string> = { light: "☀", dark: "☾", ats: "✦" };

export const ThemeToggle: FC<Props> = ({ theme, onChange, appearance = "toolbar", compact = false }) => {
  const { t } = useTranslation();

  return (
    <label
      className={`${styles.toggle} ${styles[appearance]} ${compact ? styles.compact : ""}`}
      title={compact ? t(`theme.${theme}`) : undefined}
    >
      <LuPalette className={styles.icon} aria-hidden />
      <span className={styles.label}>{t("theme.label")}</span>
      <select
        className={styles.select}
        value={theme}
        onChange={(event) => onChange(event.target.value as ThemeMode)}
        aria-label={t("theme.label")}
      >
        {THEME_OPTIONS.map((option) => (
          <option key={option} value={option} className={styles.option}>
            {THEME_GLYPHS[option]} {t(`theme.${option}`)}
          </option>
        ))}
      </select>
    </label>
  );
};
