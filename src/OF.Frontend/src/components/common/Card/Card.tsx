import { type FC, type HTMLAttributes, type ReactNode } from "react";
import styles from "./Card.module.css";

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  title?: string;
  children: ReactNode;
  className?: string;
  bodyClassName?: string;
}

export const Card: FC<CardProps> = ({ title, children, className, bodyClassName, ...rest }) => {
  return (
    <div className={`${styles.card} ${className ?? ""}`} {...rest}>
      {title && <h3 className={styles.title}>{title}</h3>}
      <div className={`${styles.body} ${bodyClassName ?? ""}`}>{children}</div>
    </div>
  );
};
