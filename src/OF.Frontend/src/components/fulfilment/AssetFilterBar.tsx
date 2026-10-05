import { useId, type FC } from "react";
import { useTranslation } from "react-i18next";
import styles from "./AssetFilterBar.module.css";

export interface AssetFilterValues {
  search: string;
  status: string;
  warehouse: string;
  division: string;
  itemNumber: string;
  description: string;
}

interface AssetFilterBarProps {
  values: AssetFilterValues;
  onChange: (field: keyof AssetFilterValues, value: string) => void;
  onSearch: () => void;
  onClear: () => void;
  isLoading: boolean;
  warehouses?: string[];
  divisions?: string[];
}

const ASSET_STATUSES = [
  { value: "Available", label: "assetPicker.available" },
  { value: "OnHire", label: "assetPicker.onHire" },
  { value: "Service", label: "assetPicker.service" },
  { value: "Repair", label: "assetPicker.repair" },
  { value: "Collection", label: "assetPicker.collection" },
  { value: "In Transit", label: "assetPicker.inTransit" },
];

export const AssetFilterBar: FC<AssetFilterBarProps> = ({
  values,
  onChange,
  onSearch,
  onClear,
  isLoading,
  warehouses,
  divisions,
}) => {
  const { t } = useTranslation();
  const id = useId();

  return (
    <form
      className={styles.filterBar}
      onSubmit={(event) => {
        event.preventDefault();
        if (!isLoading) onSearch();
      }}
    >
      <div className={styles.filterRow}>
        <div className={styles.filterGroup}>
          <label className={styles.label} htmlFor={`${id}-status`}>
            {t("ringfence.status")}
          </label>
          <select
            className={styles.select}
            id={`${id}-status`}
            value={values.status}
            onChange={(e) => onChange("status", e.target.value)}
          >
            <option value="">{t("assetPicker.all")}</option>
            {ASSET_STATUSES.map((s) => (
              <option key={s.value} value={s.value}>
                {t(s.label)}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.filterGroup}>
          <label className={styles.label} htmlFor={`${id}-warehouse`}>
            {t("ringfence.warehouse")}
          </label>
          {warehouses && warehouses.length > 0 ? (
            <select
              className={styles.select}
              id={`${id}-warehouse`}
              value={values.warehouse}
              onChange={(e) => onChange("warehouse", e.target.value)}
            >
              <option value="">{t("assetPicker.all")}</option>
              {warehouses.map((w) => (
                <option key={w} value={w}>
                  {w}
                </option>
              ))}
            </select>
          ) : (
            <input
              type="text"
              className={styles.input}
              placeholder={t("assetPicker.searchField", { field: t("ringfence.warehouse") })}
              id={`${id}-warehouse`}
              value={values.warehouse}
              onChange={(e) => onChange("warehouse", e.target.value)}
            />
          )}
        </div>

        <div className={styles.filterGroup}>
          <label className={styles.label} htmlFor={`${id}-division`}>
            {t("ringfence.division")}
          </label>
          {divisions && divisions.length > 0 ? (
            <select
              className={styles.select}
              id={`${id}-division`}
              value={values.division}
              onChange={(e) => onChange("division", e.target.value)}
            >
              <option value="">{t("assetPicker.all")}</option>
              {divisions.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>
          ) : (
            <input
              type="text"
              className={styles.input}
              placeholder={t("assetPicker.searchField", { field: t("ringfence.division") })}
              id={`${id}-division`}
              value={values.division}
              onChange={(e) => onChange("division", e.target.value)}
            />
          )}
        </div>
      </div>

      <div className={styles.filterRow}>
        <div className={styles.filterGroupWide}>
          <label className={styles.label} htmlFor={`${id}-itemNumber`}>
            {t("ringfence.itemNumber")}
          </label>
          <input
            type="text"
            className={styles.input}
            placeholder={t("assetPicker.searchField", { field: t("ringfence.itemNumber") })}
            id={`${id}-itemNumber`}
            value={values.itemNumber}
            onChange={(e) => onChange("itemNumber", e.target.value)}
          />
        </div>

        <div className={styles.filterGroupWide}>
          <label className={styles.label} htmlFor={`${id}-description`}>
            {t("ringfence.description")}
          </label>
          <input
            type="text"
            className={styles.input}
            placeholder={t("assetPicker.searchField", { field: t("ringfence.description") })}
            id={`${id}-description`}
            value={values.description}
            onChange={(e) => onChange("description", e.target.value)}
          />
        </div>
      </div>

      <div className={styles.filterRow}>
        <div className={styles.filterGroupFull}>
          <label className={styles.label} htmlFor={`${id}-search`}>
            {t("assetPicker.generalSearch")}
          </label>
          <input
            type="search"
            className={styles.input}
            placeholder={t("assetPicker.generalHint")}
            id={`${id}-search`}
            value={values.search}
            onChange={(e) => onChange("search", e.target.value)}

            autoFocus
          />
        </div>
      </div>

      <div className={styles.actions}>
        <button className={styles.searchBtn} disabled={isLoading} type="submit">
          {isLoading ? t("assetPicker.searching") : t("common.search")}
        </button>
        <button className={styles.clearBtn} onClick={onClear} type="button">
          {t("assetPicker.clear")}
        </button>
      </div>
    </form>
  );
};
