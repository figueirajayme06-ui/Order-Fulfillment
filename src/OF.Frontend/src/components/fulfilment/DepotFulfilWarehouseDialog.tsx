import { useEffect, useId, useRef, useState, type FC, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { fetchDivisions, fetchWarehouses, type WarehouseLookup } from "../../services/lookupsService";
import { Alert, Button, Spinner } from "../common";
import styles from "./DepotFulfilWarehouseDialog.module.css";

interface DepotFulfilWarehouseDialogProps {
  restoreFocusTo?: HTMLElement | null;
  onConfirm: (warehouse: string) => void;
  onCancel: () => void;
}

export const DepotFulfilWarehouseDialog: FC<DepotFulfilWarehouseDialogProps> = ({
  restoreFocusTo,
  onConfirm,
  onCancel,
}) => {
  const { t } = useTranslation();
  const titleId = useId();
  const descriptionId = useId();
  const modalRef = useRef<HTMLDivElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);
  const [warehouses, setWarehouses] = useState<WarehouseLookup[]>([]);
  const [selectedWarehouse, setSelectedWarehouse] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [hasLoadError, setHasLoadError] = useState(false);

  useEffect(() => {
    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const focusId = window.requestAnimationFrame(() => modalRef.current?.focus());
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onCancel();
      }
    };
    document.addEventListener("keydown", onKeyDown);
    return () => {
      window.cancelAnimationFrame(focusId);
      document.removeEventListener("keydown", onKeyDown);
      const focusTarget = restoreFocusTo ?? previousFocusRef.current;
      if (focusTarget && document.contains(focusTarget)) focusTarget.focus();
    };
  }, [onCancel, restoreFocusTo]);

  useEffect(() => {
    let cancelled = false;
    void fetchDivisions()
      .then((divisions) => fetchWarehouses(divisions.map((division) => division.code).join(",")))
      .then((result) => {
        if (cancelled) return;
        const distinct = Array.from(
          new Map(result.map((warehouse) => [warehouse.warehouseCode.toUpperCase(), warehouse])).values(),
        ).sort((left, right) => left.warehouseCode.localeCompare(right.warehouseCode));
        setWarehouses(distinct);
      })
      .catch(() => {
        if (!cancelled) setHasLoadError(true);
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (selectedWarehouse) onConfirm(selectedWarehouse);
  };

  return (
    <div className={styles.overlay} role="presentation" onClick={onCancel} data-print-hidden>
      <div
        ref={modalRef}
        className={styles.modal}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        tabIndex={-1}
        onClick={(event) => event.stopPropagation()}
      >
        <header className={styles.header}>
          <div>
            <h2 id={titleId}>{t("fulfilment.depotFulfilFromWarehouse")}</h2>
            <p id={descriptionId}>{t("fulfilment.depotFulfilFromWarehouseDescription")}</p>
          </div>
          <button
            type="button"
            className={styles.close}
            onClick={onCancel}
            aria-label={t("fulfilment.closeWarehouseDialog")}
          >
            x
          </button>
        </header>
        <form className={styles.form} onSubmit={handleSubmit}>
          {isLoading ? (
            <div className={styles.loading} role="status">
              <Spinner size="small" />
              <span>{t("common.loading")}</span>
            </div>
          ) : hasLoadError ? (
            <Alert variant="error">{t("fulfilment.warehouseLoadError")}</Alert>
          ) : warehouses.length === 0 ? (
            <p className={styles.empty}>{t("fulfilment.noWarehousesAvailable")}</p>
          ) : (
            <div className={styles.field}>
              <label htmlFor="depot-fulfil-warehouse">{t("fulfilment.warehouse")}</label>
              <select
                id="depot-fulfil-warehouse"
                value={selectedWarehouse}
                onChange={(event) => setSelectedWarehouse(event.target.value)}
              >
                <option value="">{t("fulfilment.selectWarehouse")}</option>
                {warehouses.map((warehouse) => (
                  <option key={warehouse.warehouseCode} value={warehouse.warehouseCode}>
                    {warehouse.warehouseCode} - {warehouse.warehouse}
                  </option>
                ))}
              </select>
            </div>
          )}
          <footer className={styles.actions}>
            <Button label={t("common.cancel")} variant="secondary" onClick={onCancel} />
            <Button
              label={t("fulfilment.depotFulfil")}
              variant="primary"
              type="submit"
              disabled={!selectedWarehouse || isLoading || hasLoadError}
            />
          </footer>
        </form>
      </div>
    </div>
  );
};
