import {
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type FC,
  type FormEvent,
} from "react";
import { useTranslation } from "react-i18next";
import {
  createEquipmentLine,
  fetchEquipmentCatalog,
  fetchEquipmentGenericOptions,
  type EquipmentCatalog,
  type EquipmentGenericOptions,
} from "../../services/agreementsService";
import type { AgreementLine } from "../../types";
import { Alert, Button, Spinner } from "../common";
import styles from "./AddEquipmentDialog.module.css";

interface AddEquipmentDialogProps {
  headerId: number;
  parentLine: AgreementLine;
  restoreFocusTo?: HTMLElement | null;
  onAdded: () => void | Promise<void>;
  onCancel: () => void;
}

const FOCUSABLE_SELECTOR = [
  "button:not([disabled])",
  "[href]",
  "input:not([disabled]):not([type='hidden'])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

function isVisibleFocusable(element: HTMLElement): boolean {
  const rect = element.getBoundingClientRect();
  return rect.width > 0 && rect.height > 0;
}

function formatLineDate(value: string | null, locale: string): string {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(date);
}

function toAttributeFilters(selectedAttributes: Record<string, string>): string[] {
  return Object.entries(selectedAttributes)
    .filter(([, value]) => value !== "")
    .map(([name, value]) => `${name}:${value}`);
}

export const AddEquipmentDialog: FC<AddEquipmentDialogProps> = ({
  headerId,
  parentLine,
  restoreFocusTo,
  onAdded,
  onCancel,
}) => {
  const { t, i18n } = useTranslation();
  const dialogTitleId = useId();
  const dialogDescriptionId = useId();
  const modalRef = useRef<HTMLDivElement>(null);
  const previousFocusedElementRef = useRef<HTMLElement | null>(null);
  const genericRequestIdRef = useRef(0);
  const isSubmittingRef = useRef(false);
  const [catalog, setCatalog] = useState<EquipmentCatalog | null>(null);
  const [isCatalogLoading, setIsCatalogLoading] = useState(true);
  const [catalogError, setCatalogError] = useState(false);
  const [productLineId, setProductLineId] = useState("");
  const [genericId, setGenericId] = useState("");
  const [genericOptions, setGenericOptions] = useState<EquipmentGenericOptions | null>(null);
  const [selectedAttributes, setSelectedAttributes] = useState<Record<string, string>>({});
  const [itemNumber, setItemNumber] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [isOptionsLoading, setIsOptionsLoading] = useState(false);
  const [optionsError, setOptionsError] = useState(false);
  const [submitError, setSubmitError] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const getFocusableElements = useCallback(() => {
    if (!modalRef.current) return [] as HTMLElement[];

    return Array.from(modalRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(
      (element) => !element.hasAttribute("disabled") && isVisibleFocusable(element),
    );
  }, []);

  const requestClose = useCallback(() => {
    if (!isSubmittingRef.current) onCancel();
  }, [onCancel]);

  useEffect(() => {
    previousFocusedElementRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const rafId = window.requestAnimationFrame(() => {
      const focusables = getFocusableElements();
      if (focusables.length > 0) focusables[0].focus();
      else modalRef.current?.focus();
    });

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        requestClose();
        return;
      }

      if (event.key !== "Tab") return;
      const focusables = getFocusableElements();
      if (focusables.length === 0) {
        event.preventDefault();
        modalRef.current?.focus();
        return;
      }

      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement;
      if (event.shiftKey) {
        if (active === first || !(active instanceof Node) || !modalRef.current?.contains(active)) {
          event.preventDefault();
          last.focus();
        }
      } else if (active === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      window.cancelAnimationFrame(rafId);
      document.removeEventListener("keydown", handleKeyDown);
      const restoreTarget = restoreFocusTo ?? previousFocusedElementRef.current;
      if (restoreTarget && document.contains(restoreTarget)) restoreTarget.focus();
    };
  }, [getFocusableElements, requestClose, restoreFocusTo]);

  useEffect(() => {
    let cancelled = false;
    setIsCatalogLoading(true);
    setCatalogError(false);

    void fetchEquipmentCatalog(headerId)
      .then((result) => {
        if (!cancelled) setCatalog(result);
      })
      .catch(() => {
        if (!cancelled) setCatalogError(true);
      })
      .finally(() => {
        if (!cancelled) setIsCatalogLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [headerId]);

  const attributeFilters = useMemo(() => toAttributeFilters(selectedAttributes), [selectedAttributes]);
  const selectedAttributeEntries = useMemo(
    () => Object.entries(selectedAttributes).filter(([, value]) => value !== ""),
    [selectedAttributes],
  );

  useEffect(() => {
    if (!genericId) {
      setGenericOptions(null);
      setIsOptionsLoading(false);
      return;
    }

    const requestId = ++genericRequestIdRef.current;
    setIsOptionsLoading(true);
    setOptionsError(false);

    void fetchEquipmentGenericOptions(headerId, Number(genericId), attributeFilters)
      .then((result) => {
        if (requestId === genericRequestIdRef.current) setGenericOptions(result);
      })
      .catch(() => {
        if (requestId === genericRequestIdRef.current) setOptionsError(true);
      })
      .finally(() => {
        if (requestId === genericRequestIdRef.current) setIsOptionsLoading(false);
      });
  }, [attributeFilters, genericId, headerId]);

  const availableGenerics = useMemo(
    () => catalog?.generics.filter((generic) => String(generic.productLineId) === productLineId) ?? [],
    [catalog, productLineId],
  );

  const handleProductLineChange = (event: ChangeEvent<HTMLSelectElement>) => {
    setProductLineId(event.target.value);
    setGenericId("");
    setGenericOptions(null);
    setSelectedAttributes({});
    setItemNumber("");
    setOptionsError(false);
    setSubmitError(false);
  };

  const handleGenericChange = (event: ChangeEvent<HTMLSelectElement>) => {
    setGenericId(event.target.value);
    setGenericOptions(null);
    setSelectedAttributes({});
    setItemNumber("");
    setOptionsError(false);
    setSubmitError(false);
  };

  const handleAttributeChange = (name: string, value: string) => {
    setSelectedAttributes((current) => {
      const next = { ...current };
      if (value) next[name] = value;
      else delete next[name];
      return next;
    });
    setItemNumber("");
    setSubmitError(false);
  };

  const numericQuantity = Number(quantity);
  const quantityIsValid = Number.isInteger(numericQuantity) && numericQuantity >= 1 && numericQuantity <= 1000;
  const canSubmit = Boolean(genericId) && quantityIsValid && !isOptionsLoading && !optionsError && !isSubmitting;

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmit || isSubmittingRef.current) return;

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setSubmitError(false);
    try {
      await createEquipmentLine(headerId, {
        parentLineId: parentLine.id,
        genericId: Number(genericId),
        itemNumber: itemNumber || null,
        attributes: attributeFilters,
        quantity: numericQuantity,
      });
      await onAdded();
    } catch {
      setSubmitError(true);
      isSubmittingRef.current = false;
      setIsSubmitting(false);
    }
  };

  const lineLabel = parentLine.agreementLineNumber ?? String(parentLine.id);
  const hasCatalogOptions = Boolean(catalog?.productLines.length && catalog.generics.length);

  return (
    <div className={styles.overlay} onClick={requestClose} role="presentation" data-print-hidden>
      <div
        className={styles.modal}
        onClick={(event) => event.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby={dialogTitleId}
        aria-describedby={dialogDescriptionId}
        aria-busy={isSubmitting}
        tabIndex={-1}
        ref={modalRef}
      >
        <header className={styles.header}>
          <div>
            <h2 id={dialogTitleId}>{t("equipment.dialogTitle", { line: lineLabel })}</h2>
            <p id={dialogDescriptionId}>{t("equipment.dialogDescription")}</p>
          </div>
          <button
            type="button"
            className={styles.close}
            onClick={requestClose}
            aria-label={t("equipment.closeDialog")}
            disabled={isSubmitting}
          >
            ×
          </button>
        </header>

        <dl className={styles.context} aria-label={t("equipment.inheritedContext")}>
          <div>
            <dt>{t("equipment.sourceLine")}</dt>
            <dd>{lineLabel}</dd>
          </div>
          <div>
            <dt>{t("equipment.warehouse")}</dt>
            <dd>{parentLine.warehouse || "—"}</dd>
          </div>
          <div>
            <dt>{t("equipment.hirePeriod")}</dt>
            <dd>
              {formatLineDate(parentLine.validFromDate, i18n.language)} –{" "}
              {formatLineDate(parentLine.validToDate, i18n.language)}
            </dd>
          </div>
        </dl>

        <form className={styles.form} onSubmit={handleSubmit}>
          {catalogError && <Alert variant="error">{t("equipment.catalogError")}</Alert>}
          {submitError && <Alert variant="error">{t("equipment.addError")}</Alert>}

          {isCatalogLoading ? (
            <div className={styles.loading} role="status">
              <Spinner size="small" />
              <span>{t("equipment.loadingCatalog")}</span>
            </div>
          ) : !catalogError && !hasCatalogOptions ? (
            <p className={styles.empty}>{t("equipment.catalogEmpty")}</p>
          ) : !catalogError ? (
            <>
              <div className={styles.primaryFields}>
                <div className={styles.field}>
                  <label htmlFor="equipment-product-line">{t("equipment.productLine")}</label>
                  <select
                    id="equipment-product-line"
                    value={productLineId}
                    onChange={handleProductLineChange}
                    disabled={isSubmitting}
                  >
                    <option value="">{t("equipment.selectProductLine")}</option>
                    {catalog?.productLines.map((productLine) => (
                      <option key={productLine.id} value={productLine.id}>
                        {productLine.description}
                        {productLine.familyDescription ? ` — ${productLine.familyDescription}` : ""}
                      </option>
                    ))}
                  </select>
                </div>

                <div className={styles.field}>
                  <label htmlFor="equipment-generic">{t("equipment.generic")}</label>
                  <select
                    id="equipment-generic"
                    value={genericId}
                    onChange={handleGenericChange}
                    disabled={!productLineId || isSubmitting}
                  >
                    <option value="">{t("equipment.selectGeneric")}</option>
                    {availableGenerics.map((generic) => (
                      <option key={generic.id} value={generic.id}>
                        {generic.code} — {generic.description}
                      </option>
                    ))}
                  </select>
                  {productLineId && availableGenerics.length === 0 && (
                    <span className={styles.hint}>{t("equipment.noGenerics")}</span>
                  )}
                </div>
              </div>

              {isOptionsLoading && (
                <div className={styles.loading} role="status">
                  <Spinner size="small" />
                  <span>{t("equipment.loadingOptions")}</span>
                </div>
              )}
              {optionsError && <Alert variant="error">{t("equipment.optionsError")}</Alert>}

              {genericId && genericOptions && !optionsError && (
                <section className={styles.configuration} aria-labelledby="equipment-configuration-title">
                  <div className={styles.configurationHeader}>
                    <div>
                      <h3 id="equipment-configuration-title">{t("equipment.refineSelection")}</h3>
                      <p>{t("equipment.refineSelectionHint")}</p>
                    </div>
                    <span className={styles.matchCount}>
                      {t("equipment.matchingItems", { count: genericOptions.items.length })}
                    </span>
                  </div>

                  {genericOptions.attributes.length > 0 && (
                    <>
                      <details className={styles.attributeDisclosure}>
                        <summary>
                          <span>{t("equipment.attributes")}</span>
                          <span className={styles.attributeSummary}>
                            {selectedAttributeEntries.length > 0
                              ? t("equipment.attributesSelected", { count: selectedAttributeEntries.length })
                              : t("equipment.attributesOptional")}
                          </span>
                        </summary>
                        <fieldset className={styles.attributeGroup} disabled={isSubmitting || isOptionsLoading}>
                          <legend className={styles.visuallyHidden}>{t("equipment.attributes")}</legend>
                          <div className={styles.attributeFields}>
                            {genericOptions.attributes.map((attribute) => (
                              <div className={styles.field} key={attribute.name}>
                                <label htmlFor={`equipment-attribute-${attribute.name}`}>{attribute.name}</label>
                                <select
                                  id={`equipment-attribute-${attribute.name}`}
                                  value={selectedAttributes[attribute.name] ?? ""}
                                  onChange={(event) => handleAttributeChange(attribute.name, event.target.value)}
                                >
                                  <option value="">{t("equipment.anyAttribute")}</option>
                                  {attribute.values.map((value) => (
                                    <option key={value} value={value}>
                                      {value}
                                    </option>
                                  ))}
                                </select>
                              </div>
                            ))}
                          </div>
                        </fieldset>
                      </details>

                      {selectedAttributeEntries.length > 0 && (
                        <div className={styles.attributeChips} aria-label={t("equipment.selectedAttributes")}>
                          {selectedAttributeEntries.map(([name, value]) => (
                            <span className={styles.attributeChip} key={name}>
                              <span>
                                <strong>{name}:</strong> {value}
                              </span>
                              <button
                                type="button"
                                onClick={() => handleAttributeChange(name, "")}
                                aria-label={t("equipment.removeAttribute", { name })}
                                disabled={isSubmitting || isOptionsLoading}
                              >
                                ×
                              </button>
                            </span>
                          ))}
                        </div>
                      )}
                    </>
                  )}

                  <div className={styles.field}>
                    <label htmlFor="equipment-item">{t("equipment.exactItem")}</label>
                    <select
                      id="equipment-item"
                      value={itemNumber}
                      onChange={(event) => setItemNumber(event.target.value)}
                      disabled={isSubmitting || isOptionsLoading}
                    >
                      <option value="">{t("equipment.anySuitableItem")}</option>
                      {genericOptions.items.map((item) => (
                        <option key={item.itemNumber} value={item.itemNumber}>
                          {item.itemNumber} — {item.description}
                        </option>
                      ))}
                    </select>
                  </div>
                </section>
              )}

              <div className={styles.quantityField}>
                <label htmlFor="equipment-quantity">{t("equipment.quantity")}</label>
                <input
                  id="equipment-quantity"
                  type="number"
                  min={1}
                  max={1000}
                  step={1}
                  value={quantity}
                  onChange={(event) => setQuantity(event.target.value)}
                  aria-invalid={!quantityIsValid}
                  aria-describedby={!quantityIsValid ? "equipment-quantity-error" : undefined}
                  disabled={isSubmitting}
                />
                {!quantityIsValid && (
                  <span id="equipment-quantity-error" className={styles.validation} role="alert">
                    {t("equipment.quantityRange")}
                  </span>
                )}
              </div>
            </>
          ) : null}

          <footer className={styles.actions}>
            <Button label={t("common.cancel")} variant="secondary" onClick={requestClose} disabled={isSubmitting} />
            <Button
              label={isSubmitting ? t("equipment.adding") : t("equipment.add")}
              variant="primary"
              type="submit"
              disabled={!canSubmit}
            />
          </footer>
        </form>
      </div>
    </div>
  );
};
