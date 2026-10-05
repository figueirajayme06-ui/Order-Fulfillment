import { useCallback, useEffect, useId, useMemo, useRef, useState, type FC, type KeyboardEvent } from "react";
import { createPortal } from "react-dom";
import { LuChevronDown } from "react-icons/lu";
import { useTranslation } from "react-i18next";
import styles from "./MultiSelectFilter.module.css";

export interface MultiSelectFilterOption {
  label: string;
  value: string;
}

export interface MultiSelectFilterProps {
  allowEmpty?: boolean;
  allLabel?: string;
  className?: string;
  compact?: boolean;
  label: string;
  options: readonly MultiSelectFilterOption[];
  searchable?: boolean;
  showLabel?: boolean;
  values: readonly string[];
  onChange: (values: string[]) => void;
}

interface PopoverPosition {
  left: number;
  top: number;
  width: number;
}

export const MultiSelectFilter: FC<MultiSelectFilterProps> = ({
  allowEmpty = true,
  allLabel,
  className,
  compact = false,
  label,
  options,
  searchable = false,
  showLabel = true,
  values,
  onChange,
}) => {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");
  const [position, setPosition] = useState<PopoverPosition>({ left: 0, top: 0, width: 180 });
  const triggerRef = useRef<HTMLButtonElement>(null);
  const popoverRef = useRef<HTMLDivElement>(null);
  const popoverId = useId();
  const selectedValues = useMemo(() => new Set(values), [values]);
  const selectedOptions = options.filter((option) => selectedValues.has(option.value));
  const visibleOptions = useMemo(() => {
    const search = searchTerm.trim().toLocaleLowerCase();
    return search
      ? options.filter((option) => `${option.label} ${option.value}`.toLocaleLowerCase().includes(search))
      : options;
  }, [options, searchTerm]);
  const selectionSummary =
    selectedOptions.length === 0
      ? (allLabel ?? t("multiSelect.all"))
      : selectedOptions.length === 1
        ? selectedOptions[0].label
        : t("multiSelect.selectedCount", { count: selectedOptions.length });

  const updatePosition = useCallback(() => {
    const trigger = triggerRef.current;
    if (!trigger) return;
    const rect = trigger.getBoundingClientRect();
    const viewportPadding = 8;
    const width = Math.min(Math.max(rect.width, compact ? 180 : 220), window.innerWidth - viewportPadding * 2);
    const popoverHeight = popoverRef.current?.getBoundingClientRect().height ?? 0;
    const belowTop = rect.bottom + 4;
    const aboveTop = rect.top - popoverHeight - 4;
    const preferredTop =
      popoverHeight > 0 && belowTop + popoverHeight > window.innerHeight - viewportPadding ? aboveTop : belowTop;
    const left = Math.min(
      Math.max(viewportPadding, rect.left),
      Math.max(viewportPadding, window.innerWidth - width - viewportPadding),
    );
    const top = Math.min(
      Math.max(viewportPadding, preferredTop),
      Math.max(viewportPadding, window.innerHeight - popoverHeight - viewportPadding),
    );
    setPosition({ left, top, width });
  }, [compact]);

  useEffect(() => {
    if (!isOpen) return;
    updatePosition();

    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!triggerRef.current?.contains(target) && !popoverRef.current?.contains(target)) setIsOpen(false);
    };
    const handleViewportChange = () => updatePosition();
    document.addEventListener("pointerdown", handlePointerDown);
    window.addEventListener("resize", handleViewportChange);
    window.addEventListener("scroll", handleViewportChange, true);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      window.removeEventListener("resize", handleViewportChange);
      window.removeEventListener("scroll", handleViewportChange, true);
    };
  }, [isOpen, updatePosition]);

  useEffect(() => {
    if (!isOpen) return;
    const frame = window.requestAnimationFrame(() => {
      popoverRef.current?.querySelector<HTMLInputElement>("input:not(:disabled)")?.focus();
    });
    return () => window.cancelAnimationFrame(frame);
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) setSearchTerm("");
  }, [isOpen]);

  const closeFromKeyboard = (event: KeyboardEvent) => {
    if (event.key !== "Escape") return;
    event.preventDefault();
    setIsOpen(false);
    triggerRef.current?.focus();
  };

  const toggleValue = (value: string, checked: boolean) => {
    const nextSelection = new Set(values);
    if (checked) nextSelection.add(value);
    else nextSelection.delete(value);
    onChange(options.filter((option) => nextSelection.has(option.value)).map((option) => option.value));
  };

  const triggerText = showLabel ? `${label} — ${selectionSummary}` : selectionSummary;

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className={`${styles.trigger} ${compact ? styles.compactTrigger : ""} ${className ?? ""}`}
        aria-controls={popoverId}
        aria-expanded={isOpen}
        aria-haspopup="dialog"
        aria-label={t("multiSelect.filterSummary", { label, selection: selectionSummary })}
        onClick={() => setIsOpen((open) => !open)}
        onKeyDown={closeFromKeyboard}
      >
        <span>{triggerText}</span>
        <LuChevronDown aria-hidden="true" />
      </button>
      {isOpen &&
        createPortal(
          <div
            ref={popoverRef}
            id={popoverId}
            className={styles.popover}
            role="dialog"
            aria-label={t("multiSelect.optionsFor", { label })}
            style={{ left: position.left, top: position.top, width: position.width }}
            onKeyDown={closeFromKeyboard}
          >
            <div className={styles.popoverHeader}>
              <span>{label}</span>
              <button type="button" disabled={!allowEmpty || selectedOptions.length === 0} onClick={() => onChange([])}>
                {t("tableFilters.clear")}
              </button>
            </div>
            {searchable && (
              <input
                className={styles.search}
                type="search"
                aria-label={t("multiSelect.search", { label })}
                placeholder={t("multiSelect.searchPlaceholder", { label: label.toLocaleLowerCase() })}
                value={searchTerm}
                onChange={(event) => setSearchTerm(event.target.value)}
              />
            )}
            <div className={styles.options}>
              {visibleOptions.map((option) => {
                const checked = selectedValues.has(option.value);
                return (
                  <label key={option.value} className={styles.option}>
                    <input
                      type="checkbox"
                      checked={checked}
                      disabled={!allowEmpty && checked && selectedOptions.length === 1}
                      onChange={(event) => toggleValue(option.value, event.target.checked)}
                    />
                    <span>{option.label}</span>
                  </label>
                );
              })}
              {searchable && visibleOptions.length === 0 && (
                <span className={styles.noResults} role="status">
                  {t("multiSelect.noResults")}
                </span>
              )}
            </div>
          </div>,
          document.body,
        )}
    </>
  );
};
