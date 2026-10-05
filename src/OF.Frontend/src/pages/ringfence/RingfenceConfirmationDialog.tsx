import { useEffect, useRef, type FC, type ReactNode } from "react";
import { Button } from "../../components/common";
import type { RingfenceAsset, RingfenceDetail, RingfenceOverlap } from "../../services/ringfenceService";
import styles from "./RingfencePage.module.css";

export type DialogState =
  | { kind: "discard"; nextRingfenceId: number }
  | { kind: "delete"; ringfence: RingfenceDetail }
  | { kind: "remove"; ringfenceId: number; asset: RingfenceAsset }
  | { kind: "overlap"; ringfenceId: number; assetIds: string[]; overlaps: RingfenceOverlap[] };

interface ConfirmationDialogProps {
  open: boolean;
  title: string;
  confirmLabel: string;
  cancelLabel: string;
  confirmVariant: "primary" | "danger";
  children: ReactNode;
  onCancel: () => void;
  onConfirm: () => void;
}

export const ConfirmationDialog: FC<ConfirmationDialogProps> = ({
  open,
  title,
  confirmLabel,
  cancelLabel,
  confirmVariant,
  children,
  onCancel,
  onConfirm,
}) => {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal?.();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  return (
    <dialog
      ref={dialogRef}
      className={styles.dialog}
      aria-labelledby="ringfence-confirmation-title"
      onCancel={(event) => {
        event.preventDefault();
        onCancel();
      }}
    >
      <div className={styles.dialogContent}>
        <h2 id="ringfence-confirmation-title">{title}</h2>
        <div className={styles.dialogBody}>{children}</div>
        <div className={styles.dialogActions}>
          <Button label={cancelLabel} variant="secondary" onClick={onCancel} />
          <Button label={confirmLabel} variant={confirmVariant} onClick={onConfirm} />
        </div>
      </div>
    </dialog>
  );
};

export function getConfirmationTitle(
  state: DialogState,
  t: (key: string, options?: Record<string, unknown>) => string,
): string {
  if (state.kind === "discard") return t("ringfence.discardChangesTitle");
  if (state.kind === "delete") return t("ringfence.deleteTitle");
  if (state.kind === "remove") return t("ringfence.removeAssetTitle");
  return t("ringfence.overlapTitle");
}

export function getConfirmationButtonLabel(
  state: DialogState,
  t: (key: string, options?: Record<string, unknown>) => string,
): string {
  if (state.kind === "discard") return t("ringfence.discardChanges");
  if (state.kind === "delete") return t("ringfence.deleteRingfence");
  if (state.kind === "remove") return t("common.remove");
  return t("ringfence.addDespiteOverlap");
}
