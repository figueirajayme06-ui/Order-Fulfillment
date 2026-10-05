import { useRef, useState, type ChangeEvent, type FC, type KeyboardEvent } from "react";
import { LuArrowDown, LuArrowUp, LuArrowUpDown, LuChevronDown, LuX } from "react-icons/lu";
import { useTranslation } from "react-i18next";
import {
  isIsoCalendarDate,
  requiresDateValue,
  type DateColumnFilterValue,
  type DateFilterOperator,
} from "./dateColumnFilterModel";
import { MultiSelectFilter } from "../MultiSelectFilter/MultiSelectFilter";
import styles from "./TableColumnControls.module.css";
import { MAX_MULTI_VALUE_FILTER_VALUES, parseMultiValueTextFilter } from "./multiValueTextFilterModel";

const DATE_COMPARISON_OPERATORS: readonly DateFilterOperator[] = ["on", "notOn", "after", "before"];
const DATE_RELATIVE_OPERATORS: readonly DateFilterOperator[] = [
  "today",
  "yesterday",
  "thisMonth",
  "lastMonth",
  "nextMonth",
  "thisYear",
  "lastYear",
  "nextYear",
];

export type ColumnSortDirection = "asc" | "desc" | undefined;

interface TableColumnHeaderProps {
  label: string;
  compactLabel?: string;
  sortDirection?: ColumnSortDirection;
  onSort: () => void;
}

interface TableColumnFilterBaseProps {
  includeAllOption?: boolean;
  label: string;
  type?: "search" | "date";
  placeholder?: string;
}

interface SingleValueTableColumnFilterProps extends TableColumnFilterBaseProps {
  multiValue?: false;
  value: string;
  onChange: (value: string) => void;
  options?: ReadonlyArray<{ label: string; value: string }>;
}

interface MultiValueTableColumnFilterProps extends TableColumnFilterBaseProps {
  multiValue: true;
  value: readonly string[];
  onChange: (value: string[]) => void;
  draftValue?: string;
  onDraftChange?: (value: string) => void;
  options?: ReadonlyArray<{ label: string; value: string }>;
}

type TableColumnFilterProps = SingleValueTableColumnFilterProps | MultiValueTableColumnFilterProps;

export const TableColumnHeader: FC<TableColumnHeaderProps> = ({ label, compactLabel, sortDirection, onSort }) => {
  const directionLabel = sortDirection === "asc" ? "ascending" : sortDirection === "desc" ? "descending" : "not sorted";
  const SortIcon = sortDirection === "asc" ? LuArrowUp : sortDirection === "desc" ? LuArrowDown : LuArrowUpDown;

  return (
    <button
      className={styles.headerButton}
      type="button"
      onClick={onSort}
      data-sorted={sortDirection ? "true" : "false"}
      aria-label={`Sort by ${label}, currently ${directionLabel}`}
    >
      <span className={`${styles.headerLabel} ${compactLabel ? styles.headerLabelWithCompact : ""}`}>{label}</span>
      {compactLabel && (
        <span className={styles.compactHeaderLabel} aria-hidden="true">
          {compactLabel}
        </span>
      )}
      <span
        className={`${styles.sortIndicator} ${sortDirection ? styles.sortIndicatorActive : ""}`}
        data-active={sortDirection ? "true" : "false"}
        aria-hidden="true"
      >
        <SortIcon strokeWidth={2.75} />
      </span>
    </button>
  );
};

export const TableColumnFilter: FC<TableColumnFilterProps> = (props) => {
  if (props.multiValue) return <MultiValueColumnFilter {...props} />;

  const { includeAllOption = true, label, value, onChange, type = "search", placeholder, options } = props;
  const handleChange = (event: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => onChange(event.target.value);

  if (options) {
    return (
      <select className={styles.filterControl} value={value} onChange={handleChange} aria-label={`Filter ${label}`}>
        {includeAllOption && <option value="">All</option>}
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    );
  }

  return (
    <input
      className={styles.filterControl}
      type={type}
      value={value}
      onChange={handleChange}
      aria-label={`Filter ${label}`}
      placeholder={type === "date" ? undefined : (placeholder ?? "Filter...")}
    />
  );
};

const MultiValueColumnFilter: FC<MultiValueTableColumnFilterProps> = ({
  includeAllOption = true,
  label,
  value,
  onChange,
  draftValue,
  onDraftChange,
  options,
  placeholder,
}) => {
  const { t } = useTranslation();
  const [internalDraft, setInternalDraft] = useState("");
  const [hasOverflow, setHasOverflow] = useState(false);
  const draft = draftValue ?? internalDraft;
  const setDraft = (nextDraft: string) => {
    if (onDraftChange) onDraftChange(nextDraft);
    else setInternalDraft(nextDraft);
  };

  if (options) {
    return (
      <MultiSelectFilter
        allowEmpty={includeAllOption}
        className={styles.filterControl}
        compact
        label={label}
        options={options}
        showLabel={false}
        values={value}
        onChange={onChange}
      />
    );
  }

  const commit = (input: string) => {
    const result = parseMultiValueTextFilter(input, value);
    onChange(result.values);
    setHasOverflow(result.exceededLimit);
    setDraft("");
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      event.preventDefault();
      commit(draft);
      return;
    }
    if (event.key === "Backspace" && !draft && value.length > 0) {
      event.preventDefault();
      onChange(value.slice(0, -1));
      setHasOverflow(false);
    }
  };

  return (
    <div
      className={styles.multiValueFilter}
      aria-label={t("tableFilters.valuesFor", { label, defaultValue: `Filter values for ${label}` })}
    >
      {value.length > 0 && (
        <div className={styles.filterTokens}>
          {value.map((token) => (
            <span className={styles.filterToken} key={token.toLocaleLowerCase()}>
              <span title={token}>{token}</span>
              <button
                type="button"
                aria-label={t("tableFilters.removeValue", {
                  label,
                  value: token,
                  defaultValue: `Remove ${token} from ${label} filter`,
                })}
                onClick={() => onChange(value.filter((candidate) => candidate !== token))}
              >
                <LuX aria-hidden="true" />
              </button>
            </span>
          ))}
          <button
            className={styles.clearFilterTokens}
            type="button"
            aria-label={t("tableFilters.clearValues", { label, defaultValue: `Clear all ${label} filter values` })}
            onClick={() => {
              onChange([]);
              setHasOverflow(false);
            }}
          >
            {t("tableFilters.clear", { defaultValue: "Clear all" })}
          </button>
        </div>
      )}
      <input
        className={styles.filterControl}
        type="search"
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        onKeyDown={handleKeyDown}
        onPaste={(event) => {
          const pasted = event.clipboardData.getData("text");
          if (!/[,;\r\n]/.test(pasted)) return;
          event.preventDefault();
          setDraft(`${draft}${pasted}`);
        }}
        aria-label={t("tableFilters.filter", { label, defaultValue: `Filter ${label}` })}
        aria-describedby={hasOverflow ? `filter-${label.replace(/\W/g, "-")}-error` : undefined}
        placeholder={placeholder ?? t("tableFilters.addValue", { defaultValue: "Add value…" })}
      />
      {hasOverflow && (
        <span id={`filter-${label.replace(/\W/g, "-")}-error`} className={styles.filterError} role="alert">
          {t("tableFilters.maximumValues", {
            count: MAX_MULTI_VALUE_FILTER_VALUES,
            defaultValue: `Up to ${MAX_MULTI_VALUE_FILTER_VALUES} values can be used. Extra values were ignored.`,
          })}
        </span>
      )}
    </div>
  );
};

interface DateColumnFilterProps {
  label: string;
  value?: DateColumnFilterValue;
  onChange: (value: DateColumnFilterValue | undefined) => void;
}

export const DateColumnFilter: FC<DateColumnFilterProps> = ({ label, value, onChange }) => {
  const { t } = useTranslation();
  const [draftOperator, setDraftOperator] = useState<DateFilterOperator | "">(value?.operator ?? "");
  const dateInputRef = useRef<HTMLInputElement>(null);
  const operator = value?.operator ?? draftOperator;
  const selectedDate = value && requiresDateValue(value.operator) ? (value.value ?? "") : "";

  const openCalendar = () => {
    try {
      dateInputRef.current?.showPicker?.();
    } catch {
      // The input remains fully keyboard-operable when a browser does not expose showPicker.
    }
  };

  return (
    <div className={styles.dateFilter}>
      <input
        ref={dateInputRef}
        className={`${styles.filterControl} ${styles.dateFilterDateInput}`}
        type="date"
        aria-label={t("dateFilter.filter", { label })}
        value={selectedDate}
        onClick={openCalendar}
        onChange={(event) => {
          if (!event.target.value) {
            setDraftOperator("");
            onChange(undefined);
            return;
          }

          const nextOperator = operator && requiresDateValue(operator) ? operator : "on";
          setDraftOperator(nextOperator);
          onChange({ operator: nextOperator, value: event.target.value });
        }}
      />
      <span className={styles.dateFilterOptions}>
        <LuChevronDown aria-hidden="true" />
        <select
          className={styles.dateFilterOptionsSelect}
          aria-label={t("dateFilter.moreOptions", { label })}
          value={operator}
          onChange={(event) => {
            const next = event.target.value as DateFilterOperator | "";
            setDraftOperator(next);

            if (!next) {
              onChange(undefined);
            } else if (requiresDateValue(next)) {
              if (isIsoCalendarDate(selectedDate)) onChange({ operator: next, value: selectedDate });
              else openCalendar();
            } else {
              onChange({ operator: next });
            }
          }}
        >
          <option value="">{t("dateFilter.all")}</option>
          <optgroup label={t("dateFilter.comparisonConditions")}>
            {DATE_COMPARISON_OPERATORS.map((option) => (
              <option key={option} value={option}>
                {t(`dateFilter.operators.${option}`)}
              </option>
            ))}
          </optgroup>
          <optgroup label={t("dateFilter.relativePeriods")}>
            {DATE_RELATIVE_OPERATORS.map((option) => (
              <option key={option} value={option}>
                {t(`dateFilter.operators.${option}`)}
              </option>
            ))}
          </optgroup>
        </select>
      </span>
    </div>
  );
};
