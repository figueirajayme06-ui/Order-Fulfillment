import { memo, type FC } from "react";
import styles from "./Badge.module.css";

export type BadgeVariant = "success" | "warning" | "error" | "info" | "neutral";

export interface BadgeProps {
  label: string;
  variant?: BadgeVariant;
}

export const Badge: FC<BadgeProps> = memo(({ label, variant = "neutral" }) => {
  return <span className={`${styles.badge} ${styles[variant]}`}>{label}</span>;
});
