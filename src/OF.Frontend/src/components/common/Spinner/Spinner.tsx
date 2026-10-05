import { type FC } from "react";
import styles from "./Spinner.module.css";

interface SpinnerProps {
  size?: "small" | "medium" | "large";
}

export const Spinner: FC<SpinnerProps> = ({ size = "medium" }) => {
  return <div className={`${styles.spinner} ${styles[size]}`} role="status" aria-label="Loading" />;
};
