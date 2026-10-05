import { useEffect, useId, useMemo, useRef, useState, type FC, type KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import { Button } from "../../components/common";
import type { RingfenceListItem } from "../../services/ringfenceService";
import styles from "./AssetsPage.module.css";
import pickerStyles from "./AssetRingfenceActions.module.css";

export interface AssetRingfenceActionsProps {
  isBusy: boolean;
  ringfences: readonly RingfenceListItem[];
  selectedAssetCount: number;
  selectedAssetsOnPageCount: number;
  selectedRingfenceId: number | "";
  contextualReturnPath: string | null;
  isContextualTarget: boolean;
  isTargetLocked?: boolean;
  onAdd: () => void;
  onClear: () => void;
  onSelectedRingfenceChange: (ringfenceId: number | "") => void;
}

export const AssetRingfenceActions: FC<AssetRingfenceActionsProps> = ({
  isBusy,
  ringfences,
  selectedAssetCount,
  selectedAssetsOnPageCount,
  selectedRingfenceId,
  contextualReturnPath,
  isContextualTarget,
  isTargetLocked = false,
  onAdd,
  onClear,
  onSelectedRingfenceChange,
}) => {
  const { t } = useTranslation();
  const pickerId = useId();
  const labelId = `${pickerId}-label`;
  const valueId = `${pickerId}-value`;
  const listboxId = `${pickerId}-listbox`;
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const [targetSearch, setTargetSearch] = useState("");
  const triggerRef = useRef<HTMLButtonElement>(null);
  const targetSearchRef = useRef<HTMLInputElement>(null);
  const restoreTriggerFocusRef = useRef(false);
  const selectedRingfenceLabel =
    selectedRingfenceId === ""
      ? ""
      : (ringfences.find((ringfence) => ringfence.id === selectedRingfenceId)?.title ??
        t("assets.ringfence.unnamedTarget", { id: selectedRingfenceId }));
  const matchingRingfences = useMemo(() => {
    const search = targetSearch.trim().toLocaleLowerCase();
    return search
      ? ringfences.filter((ringfence) => ringfence.title.trim().toLocaleLowerCase().includes(search))
      : ringfences;
  }, [ringfences, targetSearch]);
  const selectedResultIndex = matchingRingfences.findIndex((ringfence) => ringfence.id === selectedRingfenceId);
  const [activeResultIndex, setActiveResultIndex] = useState(0);
  const activeRingfence = matchingRingfences[activeResultIndex];

  useEffect(() => {
    if (isPickerOpen) {
      targetSearchRef.current?.focus();
    } else if (restoreTriggerFocusRef.current) {
      triggerRef.current?.focus();
      restoreTriggerFocusRef.current = false;
    }
  }, [isPickerOpen]);

  useEffect(() => {
    if (matchingRingfences.length === 0) {
      setActiveResultIndex(-1);
      return;
    }

    setActiveResultIndex(selectedResultIndex >= 0 ? selectedResultIndex : 0);
  }, [matchingRingfences, selectedResultIndex]);

  useEffect(() => {
    if (isPickerOpen && activeRingfence) {
      document.getElementById(`${pickerId}-option-${activeRingfence.id}`)?.scrollIntoView?.({
        block: "nearest",
      });
    }
  }, [activeRingfence, isPickerOpen, pickerId]);

  const closePicker = (restoreFocus = false) => {
    restoreTriggerFocusRef.current = restoreFocus;
    setIsPickerOpen(false);
    setTargetSearch("");
  };

  const selectRingfence = (ringfence: RingfenceListItem) => {
    onSelectedRingfenceChange(ringfence.id);
    closePicker(true);
  };

  const handleSearchKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      if (matchingRingfences.length > 0) {
        setActiveResultIndex((current) => (current + 1) % matchingRingfences.length);
      }
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      if (matchingRingfences.length > 0) {
        setActiveResultIndex((current) => (current <= 0 ? matchingRingfences.length - 1 : current - 1));
      }
      return;
    }

    if (event.key === "Enter" && activeRingfence) {
      event.preventDefault();
      selectRingfence(activeRingfence);
      return;
    }

    if (event.key === "Escape") {
      event.preventDefault();
      closePicker(true);
    }
  };

  return (
    <>
      <div className={styles.ringfenceBar} data-print-hidden>
        <div className={styles.ringfenceTarget}>
          <label id={labelId} htmlFor="asset-ringfence-target" className={styles.ringfenceTargetLabel}>
            {t("assets.ringfence.targetLabel")}
          </label>
          {isContextualTarget ? (
            <select
              id="asset-ringfence-target"
              className={styles.filterSelect}
              value={selectedRingfenceId}
              disabled={isBusy || isTargetLocked}
              onChange={(event) =>
                onSelectedRingfenceChange(event.target.value === "" ? "" : Number(event.target.value))
              }
            >
              <option value="">{t("assets.ringfence.selectTarget")}</option>
              {ringfences.map((ringfence) => (
                <option key={ringfence.id} value={ringfence.id}>
                  {ringfence.title}
                </option>
              ))}
            </select>
          ) : (
            <div
              className={pickerStyles.picker}
              onBlur={(event) => {
                if (!event.currentTarget.contains(event.relatedTarget)) {
                  closePicker();
                }
              }}
            >
              {isPickerOpen ? (
                <input
                  id="asset-ringfence-target"
                  ref={targetSearchRef}
                  className={pickerStyles.input}
                  type="search"
                  role="combobox"
                  aria-labelledby={labelId}
                  aria-expanded="true"
                  aria-autocomplete="list"
                  aria-controls={listboxId}
                  aria-activedescendant={activeRingfence ? `${pickerId}-option-${activeRingfence.id}` : undefined}
                  value={targetSearch}
                  disabled={isBusy || isTargetLocked}
                  placeholder={t("assets.ringfence.searchTargetPlaceholder")}
                  onChange={(event) => setTargetSearch(event.target.value)}
                  onKeyDown={handleSearchKeyDown}
                />
              ) : (
                <button
                  id="asset-ringfence-target"
                  ref={triggerRef}
                  className={pickerStyles.trigger}
                  type="button"
                  role="combobox"
                  aria-labelledby={`${labelId} ${valueId}`}
                  aria-haspopup="listbox"
                  aria-expanded="false"
                  aria-controls={listboxId}
                  disabled={isBusy || isTargetLocked}
                  onClick={() => setIsPickerOpen(true)}
                >
                  <span id={valueId} className={selectedRingfenceLabel ? undefined : pickerStyles.placeholder}>
                    {selectedRingfenceLabel || t("assets.ringfence.selectTarget")}
                  </span>
                  <span className={pickerStyles.chevron} aria-hidden="true" />
                </button>
              )}
              {isPickerOpen && (
                <div className={pickerStyles.popup}>
                  <ul id={listboxId} className={pickerStyles.options} role="listbox" aria-labelledby={labelId}>
                    {matchingRingfences.map((ringfence, index) => (
                      <li
                        id={`${pickerId}-option-${ringfence.id}`}
                        key={ringfence.id}
                        className={pickerStyles.option}
                        role="option"
                        aria-selected={ringfence.id === selectedRingfenceId}
                        data-active={index === activeResultIndex || undefined}
                        onMouseDown={(event) => event.preventDefault()}
                        onMouseEnter={() => setActiveResultIndex(index)}
                        onClick={() => selectRingfence(ringfence)}
                      >
                        {ringfence.title}
                      </li>
                    ))}
                  </ul>
                  {targetSearch && matchingRingfences.length === 0 && (
                    <span className={pickerStyles.noResults} role="status" aria-live="polite">
                      {t("assets.ringfence.noMatchingTargets")}
                    </span>
                  )}
                  {targetSearch && matchingRingfences.length > 0 && (
                    <span className={styles.srOnly} role="status" aria-live="polite">
                      {t("assets.ringfence.targetSearchCount", { count: matchingRingfences.length })}
                    </span>
                  )}
                </div>
              )}
            </div>
          )}
        </div>
        {isContextualTarget && contextualReturnPath && selectedRingfenceLabel && (
          <div className={styles.ringfenceContext}>
            <span>{t("assets.ringfence.contextTarget", { title: selectedRingfenceLabel })}</span>
            <Link className={styles.ringfenceReturnLink} to={contextualReturnPath}>
              {t("assets.ringfence.returnToTarget", { title: selectedRingfenceLabel })}
            </Link>
          </div>
        )}
        <Button
          label={isBusy ? t("assets.ringfence.adding") : t("assets.ringfence.add", { count: selectedAssetCount })}
          variant="primary"
          size="small"
          onClick={onAdd}
          disabled={selectedRingfenceId === "" || selectedAssetCount === 0 || isBusy || isTargetLocked}
        />
      </div>

      {selectedAssetCount > 0 && (
        <div className={styles.selectionSummaryBar} role="status" aria-live="polite" data-print-hidden>
          <div className={styles.selectionSummaryText}>
            <span className={styles.selectionSummaryCount}>{selectedAssetCount}</span>{" "}
            {t("assets.ringfence.assetSelected", { count: selectedAssetCount })}
            <span className={styles.selectionSummaryMeta}>
              {t("assets.ringfence.onThisPage", { count: selectedAssetsOnPageCount })}
              {selectedRingfenceLabel
                ? t("assets.ringfence.selectedTarget", { title: selectedRingfenceLabel })
                : t("assets.ringfence.selectTargetToContinue")}
            </span>
          </div>
          <Button
            label={t("assets.ringfence.clearSelection")}
            variant="secondary"
            size="small"
            onClick={onClear}
            disabled={isBusy || isTargetLocked}
          />
        </div>
      )}
    </>
  );
};
