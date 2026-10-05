import { useTranslation } from "react-i18next";
import styles from "./AvailabilityPanel.module.css";

interface StockAllocatorProps {
  stockQuantity: string;
  maximumQuantity: number;
  disabled: boolean;
  stockSubmitting: boolean;
  stockError: string | null;
  onQuantityChange: (quantity: string) => void;
  onReserve: () => void;
}

export function StockAllocator({
  stockQuantity,
  maximumQuantity,
  disabled,
  stockSubmitting,
  stockError,
  onQuantityChange,
  onReserve,
}: StockAllocatorProps) {
  const { t } = useTranslation();
  const quantity = Number(stockQuantity);
  return (
    <form
      className={styles.stockAllocator}
      onSubmit={(event) => {
        event.preventDefault();
        onReserve();
      }}
    >
      <label className={styles.quantityField}>
        <span>{t("availability.stockQuantityToReserve")}</span>
        <input
          type="number"
          min="1"
          max={maximumQuantity}
          step="1"
          value={stockQuantity}
          disabled={disabled}
          onChange={(event) => onQuantityChange(event.target.value)}
        />
      </label>
      <button type="submit" className={styles.reserveBtn} disabled={disabled}>
        {stockSubmitting
          ? t("availability.reservingStock")
          : t("availability.reserveStock", {
              count: Number.isFinite(quantity) ? quantity : 0,
            })}
      </button>
      {stockError && (
        <p className={styles.assetError} role="alert">
          {stockError}
        </p>
      )}
    </form>
  );
}
