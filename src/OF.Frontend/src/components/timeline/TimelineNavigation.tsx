import styles from "./TimelineNavigation.module.css";

interface TimelineNavigationProps {
  canPanEarlier: boolean;
  canPanLater: boolean;
  onPanEarlier: () => void;
  onPanLater: () => void;
  earlierTitle?: string;
  laterTitle?: string;
}

export function TimelineNavigation({
  canPanEarlier,
  canPanLater,
  onPanEarlier,
  onPanLater,
  earlierTitle = "Show an earlier period",
  laterTitle = "Show a later period",
}: TimelineNavigationProps) {
  return (
    <div className={styles.navigation} role="group" aria-label="Timeline navigation">
      <button
        type="button"
        className={styles.button}
        onClick={onPanEarlier}
        disabled={!canPanEarlier}
        title={earlierTitle}
      >
        ← Earlier
      </button>
      <button type="button" className={styles.button} onClick={onPanLater} disabled={!canPanLater} title={laterTitle}>
        Later →
      </button>
    </div>
  );
}
