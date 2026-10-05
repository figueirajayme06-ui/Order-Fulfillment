import { useEffect, useMemo, useState, type FC } from "react";
import styles from "./AdvancedFilters.module.css";

export type FilterField =
  | {
      key: string;
      label: string;
      type: "text" | "select";
      placeholder?: string;
      options?: { value: string; label: string }[];
    }
  | {
      key: string;
      label: string;
      type: "dateRange";
      fromKey: string;
      toKey: string;
    };

interface AdvancedFiltersProps {
  fields: FilterField[];
  values: Record<string, string>;
  onApply: (values: Record<string, string>) => void;
}

export const AdvancedFilters: FC<AdvancedFiltersProps> = ({ fields, values, onApply }) => {
  const [isExpanded, setIsExpanded] = useState(false);
  const [draftValues, setDraftValues] = useState<Record<string, string>>(values);
  const [addedKeys, setAddedKeys] = useState<string[]>([]);

  useEffect(() => {
    setDraftValues(values);
    setAddedKeys(activeFields(fields, values).map((field) => field.key));
  }, [fields, values]);

  const visibleFields = useMemo(() => {
    const activeKeys = new Set(activeFields(fields, draftValues).map((field) => field.key));
    return fields.filter((field) => activeKeys.has(field.key) || addedKeys.includes(field.key));
  }, [addedKeys, draftValues, fields]);

  const availableFields = fields.filter((field) => !visibleFields.some((visible) => visible.key === field.key));
  const appliedFields = activeFields(fields, values);
  const activeCount = appliedFields.length;

  const updateDraftValue = (key: string, value: string) => {
    setDraftValues((previous) => ({ ...previous, [key]: value }));
  };

  const addField = (key: string) => {
    if (!key) return;
    setAddedKeys((previous) => (previous.includes(key) ? previous : [...previous, key]));
  };

  const removeField = (field: FilterField) => {
    const keys = field.type === "dateRange" ? [field.fromKey, field.toKey] : [field.key];
    setDraftValues((previous) => {
      const next = { ...previous };
      keys.forEach((key) => delete next[key]);
      return next;
    });
    setAddedKeys((previous) => previous.filter((key) => key !== field.key));
  };

  const applyFilters = () => {
    const allowedKeys = fields.flatMap((field) =>
      field.type === "dateRange" ? [field.fromKey, field.toKey] : [field.key],
    );
    const nextValues = Object.fromEntries(
      allowedKeys.filter((key) => draftValues[key]).map((key) => [key, draftValues[key]]),
    );
    onApply(nextValues);
  };

  const clearFilters = () => {
    setDraftValues({});
    setAddedKeys([]);
    onApply({});
  };

  return (
    <div className={styles.container}>
      <button
        className={styles.toggle}
        onClick={() => setIsExpanded((previous) => !previous)}
        type="button"
        aria-expanded={isExpanded}
      >
        <span aria-hidden="true">{isExpanded ? "▾" : "▸"}</span>
        More filters
        {activeCount > 0 && <span className={styles.badge}>{activeCount}</span>}
        {activeCount > 0 && !isExpanded && (
          <span className={styles.activeSummary} aria-label="Applied filters">
            {appliedFields.map((field) => (
              <span className={styles.activeFilter} key={field.key} title={formatAppliedFilter(field, values)}>
                {formatAppliedFilter(field, values)}
              </span>
            ))}
          </span>
        )}
      </button>

      {isExpanded && (
        <div className={styles.panel}>
          {visibleFields.map((field) => (
            <div key={field.key} className={styles.filterRow}>
              <span className={styles.label}>{field.label}</span>
              {field.type === "dateRange" ? (
                <div className={styles.dateRange}>
                  <label className={styles.dateValue}>
                    <span>From</span>
                    <input
                      type="date"
                      className={styles.input}
                      value={draftValues[field.fromKey] ?? ""}
                      onChange={(event) => updateDraftValue(field.fromKey, event.target.value)}
                      aria-label={`${field.label} from`}
                    />
                  </label>
                  <label className={styles.dateValue}>
                    <span>To</span>
                    <input
                      type="date"
                      className={styles.input}
                      value={draftValues[field.toKey] ?? ""}
                      onChange={(event) => updateDraftValue(field.toKey, event.target.value)}
                      aria-label={`${field.label} to`}
                    />
                  </label>
                </div>
              ) : field.type === "select" ? (
                <select
                  className={styles.input}
                  value={draftValues[field.key] ?? ""}
                  onChange={(event) => updateDraftValue(field.key, event.target.value)}
                  aria-label={field.label}
                >
                  <option value="">All</option>
                  {field.options?.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  type="text"
                  className={styles.input}
                  placeholder={field.placeholder ?? "Contains..."}
                  value={draftValues[field.key] ?? ""}
                  onChange={(event) => updateDraftValue(field.key, event.target.value)}
                  aria-label={field.label}
                />
              )}
              <button
                className={styles.removeButton}
                type="button"
                onClick={() => removeField(field)}
                aria-label={`Remove ${field.label} filter`}
              >
                ×
              </button>
            </div>
          ))}

          {availableFields.length > 0 && (
            <label className={styles.addField}>
              <span className={styles.addLabel}>Add filter</span>
              <select value="" onChange={(event) => addField(event.target.value)}>
                <option value="">Choose a field…</option>
                {availableFields.map((field) => (
                  <option key={field.key} value={field.key}>
                    {field.label}
                  </option>
                ))}
              </select>
            </label>
          )}

          <div className={styles.actions}>
            <button className={styles.applyButton} onClick={applyFilters} type="button">
              Apply filters
            </button>
            {activeCount > 0 && (
              <button className={styles.clearButton} onClick={clearFilters} type="button">
                Clear all
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

function activeFields(fields: FilterField[], values: Record<string, string>): FilterField[] {
  return fields.filter((field) =>
    field.type === "dateRange" ? Boolean(values[field.fromKey] || values[field.toKey]) : Boolean(values[field.key]),
  );
}

function formatAppliedFilter(field: FilterField, values: Record<string, string>): string {
  if (field.type !== "dateRange") {
    return `${field.label}: ${values[field.key]}`;
  }

  const from = values[field.fromKey] ? formatAppliedDate(values[field.fromKey]) : "any";
  const to = values[field.toKey] ? formatAppliedDate(values[field.toKey]) : "any";
  return `${field.label}: ${from}–${to}`;
}

function formatAppliedDate(value: string): string {
  const [year, month, day] = value.split("-");
  return year && month && day ? `${day}/${month}/${year}` : value;
}
