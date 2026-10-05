import { forwardRef, useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from "react";
import styles from "./SearchableSelect.module.css";

export interface SearchableSelectOption {
  label: string;
  value: string;
}

export interface SearchableSelectProps {
  ariaLabel?: string;
  className?: string;
  disabled?: boolean;
  id?: string;
  options: readonly SearchableSelectOption[];
  placeholder: string;
  searchLabel?: string;
  value: string;
  onChange: (value: string) => void;
}

export const SearchableSelect = forwardRef<HTMLButtonElement, SearchableSelectProps>(function SearchableSelect(
  { ariaLabel, className, disabled = false, id, options, placeholder, searchLabel, value, onChange },
  ref,
) {
  const generatedId = useId();
  const pickerId = id ?? generatedId;
  const listboxId = `${pickerId}-options`;
  const inputRef = useRef<HTMLInputElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const selectedOption = options.find((option) => option.value === value);
  const visibleOptions = useMemo(() => {
    const search = query.trim().toLocaleLowerCase();
    return search
      ? options.filter((option) => `${option.label} ${option.value}`.toLocaleLowerCase().includes(search))
      : options;
  }, [options, query]);

  useEffect(() => {
    if (!isOpen) return;
    setActiveIndex(0);
    inputRef.current?.focus();
  }, [isOpen, query]);

  const close = () => {
    setIsOpen(false);
    setQuery("");
  };

  const selectOption = (option: SearchableSelectOption) => {
    onChange(option.value);
    close();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      close();
      return;
    }
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      const direction = event.key === "ArrowDown" ? 1 : -1;
      setActiveIndex((current) => Math.max(0, Math.min(visibleOptions.length - 1, current + direction)));
      return;
    }
    if (event.key === "Enter" && visibleOptions[activeIndex]) {
      event.preventDefault();
      selectOption(visibleOptions[activeIndex]);
    }
  };

  return (
    <div
      className={`${styles.picker} ${className ?? ""}`}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) close();
      }}
    >
      {isOpen ? (
        <input
          ref={inputRef}
          className={styles.input}
          type="search"
          role="combobox"
          aria-label={searchLabel ?? `Search ${ariaLabel ?? placeholder}`}
          aria-expanded="true"
          aria-autocomplete="list"
          aria-controls={listboxId}
          aria-activedescendant={visibleOptions[activeIndex] ? `${pickerId}-option-${activeIndex}` : undefined}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={handleKeyDown}
        />
      ) : (
        <button
          ref={ref}
          className={styles.trigger}
          type="button"
          role="combobox"
          aria-label={ariaLabel}
          aria-haspopup="listbox"
          aria-expanded="false"
          disabled={disabled}
          onClick={() => setIsOpen(true)}
        >
          <span className={selectedOption ? undefined : styles.placeholder}>
            {selectedOption?.label ?? placeholder}
          </span>
          <span className={styles.chevron} aria-hidden="true" />
        </button>
      )}
      {isOpen && (
        <div className={styles.popup}>
          <ul id={listboxId} className={styles.options} role="listbox" aria-label={ariaLabel}>
            {visibleOptions.map((option, index) => (
              <li
                id={`${pickerId}-option-${index}`}
                key={option.value}
                className={styles.option}
                role="option"
                aria-selected={option.value === value}
                data-active={index === activeIndex || undefined}
                onMouseDown={(event) => event.preventDefault()}
                onMouseEnter={() => setActiveIndex(index)}
                onClick={() => selectOption(option)}
              >
                {option.label}
              </li>
            ))}
          </ul>
          {visibleOptions.length === 0 && <span className={styles.noResults}>No matching options</span>}
        </div>
      )}
      <select
        id={id}
        className={styles.nativeSelect}
        aria-hidden="true"
        tabIndex={-1}
        disabled={disabled}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">{placeholder}</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
});
