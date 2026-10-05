import { memo, type FC, type MouseEvent, type ReactNode } from "react";
import styles from "./Button.module.css";

export type ButtonVariant = "primary" | "secondary" | "danger" | "dangerSecondary" | "ghost";
export type ButtonSize = "small" | "medium" | "large";

export interface ButtonProps {
  label: string;
  ariaLabel?: string;
  variant?: ButtonVariant;
  size?: ButtonSize;
  disabled?: boolean;
  onClick?: (event: MouseEvent<HTMLButtonElement>) => void;
  icon?: ReactNode;
  iconOnly?: boolean;
  className?: string;
  type?: "button" | "submit" | "reset";
}

export const Button: FC<ButtonProps> = memo(
  ({
    label,
    ariaLabel,
    variant = "primary",
    size = "medium",
    disabled,
    onClick,
    icon,
    iconOnly,
    className,
    type = "button",
  }) => {
    return (
      <button
        type={type}
        className={`${styles.button} ${styles[variant]} ${styles[size]} ${iconOnly ? styles.iconOnly : ""} ${className ?? ""}`}
        aria-label={ariaLabel}
        disabled={disabled}
        onClick={onClick}
        title={label}
      >
        {icon && <span className={styles.icon}>{icon}</span>}
        {!iconOnly && label}
      </button>
    );
  },
);
