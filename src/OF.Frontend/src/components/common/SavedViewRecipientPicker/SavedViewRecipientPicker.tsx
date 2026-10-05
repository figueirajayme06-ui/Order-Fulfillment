import { useEffect, useId, useMemo, useRef, useState, type FC } from "react";
import { LuX } from "react-icons/lu";
import { useTranslation } from "react-i18next";
import type { SavedViewRecipient } from "../../../services/viewsService";
import styles from "./SavedViewRecipientPicker.module.css";

export interface SavedViewRecipientPickerProps {
  candidates: readonly SavedViewRecipient[];
  disabled?: boolean;
  error: string | null;
  isLoading: boolean;
  /** @deprecated Use showValidation to make the save-attempt state explicit. */
  focusValidation?: boolean;
  showValidation?: boolean;
  selected: readonly SavedViewRecipient[];
  onCancel: () => void;
  onChange: (recipients: SavedViewRecipient[]) => void;
  onSearch: (search: string) => void;
  onRetry: () => void;
}

const SEARCH_DEBOUNCE_MS = 250;
const MAX_RESULTS = 10;

export const SavedViewRecipientPicker: FC<SavedViewRecipientPickerProps> = ({
  candidates,
  disabled = false,
  error,
  isLoading,
  focusValidation = false,
  showValidation = focusValidation,
  selected,
  onCancel,
  onChange,
  onSearch,
  onRetry,
}) => {
  const { t } = useTranslation();
  const id = useId();
  const [search, setSearch] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const searchRef = useRef<HTMLInputElement>(null);
  const selectedKeys = useMemo(
    () => new Set(selected.map((recipient) => recipient.loginName.toLocaleLowerCase())),
    [selected],
  );
  const normalizedSearch = search.trim();
  const normalizedSearchKey = normalizedSearch.toLocaleLowerCase();
  const available = normalizedSearch
    ? candidates
        .filter(
          (candidate) =>
            !selectedKeys.has(candidate.loginName.toLocaleLowerCase()) &&
            (candidate.fullName.toLocaleLowerCase().includes(normalizedSearchKey) ||
              candidate.loginName.toLocaleLowerCase().includes(normalizedSearchKey)),
        )
        .slice(0, MAX_RESULTS)
    : [];
  const searchId = `${id}-search`;
  const resultsId = `${id}-results`;
  const statusId = `${id}-status`;
  const validationId = `${id}-validation`;
  const showResults = Boolean(normalizedSearch && !isLoading && !error && available.length > 0);
  const activeCandidate = showResults ? available[activeIndex] : undefined;

  const selectCandidate = (candidate: SavedViewRecipient) => {
    onChange([...selected, candidate]);
    setSearch("");
    setActiveIndex(0);
    searchRef.current?.focus();
  };

  useEffect(() => {
    searchRef.current?.focus();
  }, []);

  useEffect(() => {
    if (showValidation) searchRef.current?.focus();
  }, [showValidation]);

  useEffect(() => {
    setActiveIndex(0);
  }, [normalizedSearch, candidates]);

  useEffect(() => {
    if (!normalizedSearch) {
      onSearch("");
      return;
    }

    const timeout = window.setTimeout(() => onSearch(normalizedSearch), SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timeout);
  }, [normalizedSearch, onSearch]);

  return (
    <div
      className={styles.picker}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.preventDefault();
          onCancel();
        }
      }}
    >
      <div className={styles.heading}>
        <label className={styles.searchLabel} htmlFor={searchId}>
          {t("savedViews.recipientSearch")}
        </label>
        <button type="button" className={styles.cancel} onClick={onCancel}>
          {t("common.cancel")}
        </button>
      </div>
      {selected.length > 0 && (
        <ul className={styles.chips} aria-label={t("savedViews.selectedRecipients")}>
          {selected.map((recipient) => (
            <li key={recipient.loginName} className={styles.chip}>
              <span>
                <strong>{recipient.fullName || recipient.loginName}</strong>
                {recipient.fullName && recipient.fullName !== recipient.loginName && (
                  <small>{recipient.loginName}</small>
                )}
              </span>
              <button
                type="button"
                disabled={disabled}
                aria-label={t("savedViews.removeRecipient", { name: recipient.fullName || recipient.loginName })}
                onClick={() => {
                  onChange(selected.filter((entry) => entry.loginName !== recipient.loginName));
                  searchRef.current?.focus();
                }}
              >
                <LuX aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
      )}
      <input
        ref={searchRef}
        id={searchId}
        className={styles.search}
        type="search"
        role="combobox"
        disabled={disabled}
        value={search}
        aria-autocomplete="list"
        aria-controls={showResults ? resultsId : undefined}
        aria-expanded={showResults}
        aria-activedescendant={activeCandidate ? `${id}-result-${activeIndex}` : undefined}
        aria-describedby={selected.length === 0 && !isLoading && !error ? `${statusId} ${validationId}` : statusId}
        aria-invalid={showValidation || undefined}
        placeholder={t("savedViews.recipientSearchPlaceholder")}
        onChange={(event) => {
          setSearch(event.target.value);
          setActiveIndex(0);
        }}
        onKeyDown={(event) => {
          if (available.length === 0) return;

          if (event.key === "ArrowDown") {
            event.preventDefault();
            setActiveIndex((current) => (current + 1) % available.length);
          } else if (event.key === "ArrowUp") {
            event.preventDefault();
            setActiveIndex((current) => (current - 1 + available.length) % available.length);
          } else if (event.key === "Enter" && activeCandidate) {
            event.preventDefault();
            selectCandidate(activeCandidate);
          }
        }}
      />
      <div id={statusId} className={styles.status} role={error ? "alert" : "status"} aria-live="polite">
        {isLoading
          ? t("savedViews.loadingRecipients")
          : error
            ? error
            : normalizedSearch && candidates.length === 0
              ? t("savedViews.noRecipientMatches")
              : null}
        {error && (
          <button type="button" className={styles.retry} onClick={onRetry}>
            {t("common.retry")}
          </button>
        )}
      </div>
      {showResults && (
        <ul id={resultsId} className={styles.results} role="listbox" aria-label={t("savedViews.recipientResults")}>
          {available.map((candidate, index) => (
            <li key={candidate.loginName} role="presentation">
              <button
                id={`${id}-result-${index}`}
                type="button"
                role="option"
                aria-selected={index === activeIndex}
                className={index === activeIndex ? styles.activeResult : undefined}
                disabled={disabled}
                onMouseMove={() => setActiveIndex(index)}
                onClick={() => selectCandidate(candidate)}
              >
                <strong>{candidate.fullName || candidate.loginName}</strong>
                {candidate.fullName && candidate.fullName !== candidate.loginName && <span>{candidate.loginName}</span>}
              </button>
            </li>
          ))}
        </ul>
      )}
      {!isLoading && !error && selected.length === 0 && (
        <p
          id={validationId}
          className={showValidation ? styles.validation : styles.guidance}
          role={showValidation ? "alert" : undefined}
        >
          {t(showValidation ? "savedViews.recipientRequired" : "savedViews.recipientHint")}
        </p>
      )}
    </div>
  );
};
