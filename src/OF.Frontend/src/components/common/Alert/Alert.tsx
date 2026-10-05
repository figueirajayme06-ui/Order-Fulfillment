import { type FC, type ReactNode } from "react";
import styles from "./Alert.module.css";

export type AlertVariant = "success" | "warning" | "error" | "info";

interface AlertProps {
  variant: AlertVariant;
  children: ReactNode;
  onDismiss?: () => void;
}

export const Alert: FC<AlertProps> = ({ variant, children, onDismiss }) => {
  return (
    <div className={`${styles.alert} ${styles[variant]}`} role="alert">
      <div className={styles.content}>{children}</div>
      {onDismiss && (
        <button className={styles.dismiss} onClick={onDismiss} aria-label="Dismiss">
          ×
        </button>
      )}
    </div>
  );
};
