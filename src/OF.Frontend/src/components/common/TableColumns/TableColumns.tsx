import {
  useEffect,
  useRef,
  useState,
  type DragEvent as ReactDragEvent,
  type FC,
  type PointerEvent as ReactPointerEvent,
} from "react";
import { LuChevronDown, LuChevronUp, LuColumns3, LuGripVertical } from "react-icons/lu";
import { useTranslation } from "react-i18next";
import {
  moveTableColumn,
  moveTableColumnToVisibleIndex,
  repairTableColumnLayout,
  type TableColumnDefinition,
  type TableColumnLayoutItem,
} from "../../../lib/tableColumnLayout";
import styles from "./TableColumns.module.css";

interface TableColumnsMenuProps<Key extends string> {
  catalogue: readonly TableColumnDefinition<Key>[];
  layout: readonly TableColumnLayoutItem<Key>[];
  maxVisibleColumns?: number;
  onChange: (layout: TableColumnLayoutItem<Key>[]) => void;
  onVisibilityChange: (key: Key, visible: boolean) => void;
}

export function TableColumnsMenu<Key extends string>({
  catalogue,
  layout,
  maxVisibleColumns,
  onChange,
  onVisibilityChange,
}: TableColumnsMenuProps<Key>) {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const [draggedKey, setDraggedKey] = useState<Key | null>(null);
  const [dropTargetKey, setDropTargetKey] = useState<Key | null>(null);
  const [announcement, setAnnouncement] = useState("");
  const buttonRef = useRef<HTMLButtonElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const definitionByKey = new Map(catalogue.map((column) => [column.key, column]));
  const visibleColumns = layout.filter((column) => column.visible);
  const availableColumns = layout.filter((column) => !column.visible);
  const visibleKeys = visibleColumns.map((column) => column.key);
  const hasReachedVisibleLimit = maxVisibleColumns != null && visibleColumns.length >= maxVisibleColumns;

  const closePanel = () => {
    setIsOpen(false);
    buttonRef.current?.focus();
  };

  const announceMove = (key: Key, position: number) => {
    const definition = definitionByKey.get(key);
    if (!definition) return;
    setAnnouncement(
      t("tableColumns.moved", {
        column: t(definition.labelKey),
        position,
      }),
    );
  };

  const handleDrop = (event: ReactDragEvent, targetIndex: number) => {
    event.preventDefault();
    if (!draggedKey) return;
    onChange(moveTableColumnToVisibleIndex(catalogue, layout, draggedKey, targetIndex));
    announceMove(draggedKey, targetIndex + 1);
    setDraggedKey(null);
    setDropTargetKey(null);
  };

  useEffect(() => {
    if (!isOpen) return;
    panelRef.current?.querySelector<HTMLElement>("input:not(:disabled), button:not(:disabled)")?.focus();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      closePanel();
    };
    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!panelRef.current?.contains(target) && !buttonRef.current?.contains(target)) setIsOpen(false);
    };
    document.addEventListener("keydown", handleKeyDown);
    document.addEventListener("pointerdown", handlePointerDown);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.removeEventListener("pointerdown", handlePointerDown);
    };
  }, [isOpen]);

  return (
    <div className={styles.menu} data-print-hidden>
      <button
        ref={buttonRef}
        className={styles.trigger}
        type="button"
        aria-expanded={isOpen}
        aria-haspopup="dialog"
        aria-label={t("tableColumns.columnsWithCount", { count: visibleColumns.length })}
        onClick={() => setIsOpen((open) => !open)}
      >
        <LuColumns3 aria-hidden="true" />
        {t("tableColumns.columns")}
        <span className={styles.triggerCount} aria-hidden="true">
          {visibleColumns.length}
        </span>
      </button>
      {isOpen && (
        <div ref={panelRef} className={styles.panel} role="dialog" aria-label={t("tableColumns.configureColumns")}>
          <div className={styles.panelHeader}>
            <strong>{t("tableColumns.configureColumns")}</strong>
            <span>{t("tableColumns.hint")}</span>
          </div>
          <section aria-labelledby="visible-columns-heading">
            <h3 id="visible-columns-heading" className={styles.sectionHeading}>
              {t("tableColumns.visibleColumns", { count: visibleColumns.length })}
            </h3>
            <ol className={styles.columnList}>
              {visibleColumns.map((column) => {
                const definition = definitionByKey.get(column.key);
                if (!definition) return null;
                const visibleIndex = visibleKeys.indexOf(column.key);
                const canMoveUp =
                  column.visible &&
                  !definition.locked &&
                  visibleIndex > 0 &&
                  !definitionByKey.get(visibleKeys[visibleIndex - 1])?.locked;
                const canMoveDown =
                  column.visible && !definition.locked && visibleIndex >= 0 && visibleIndex < visibleKeys.length - 1;
                return (
                  <li
                    key={column.key}
                    className={`${styles.columnRow} ${
                      dropTargetKey === column.key
                        ? draggedKey && visibleKeys.indexOf(draggedKey) < visibleIndex
                          ? styles.dropTargetAfter
                          : styles.dropTarget
                        : ""
                    }`}
                    onDragEnter={() => {
                      if (draggedKey && draggedKey !== column.key && !definition.locked) setDropTargetKey(column.key);
                    }}
                    onDragOver={(event) => {
                      if (!draggedKey || definition.locked) return;
                      event.preventDefault();
                      event.dataTransfer.dropEffect = "move";
                    }}
                    onDrop={(event) => {
                      if (!definition.locked) handleDrop(event, visibleIndex);
                    }}
                  >
                    {definition.locked ? (
                      <span className={styles.dragPlaceholder} aria-hidden="true" />
                    ) : (
                      <span
                        className={styles.dragHandle}
                        draggable
                        title={t("tableColumns.drag", { column: t(definition.labelKey) })}
                        aria-hidden="true"
                        onDragStart={(event) => {
                          event.dataTransfer.effectAllowed = "move";
                          event.dataTransfer.setData("text/plain", column.key);
                          setDraggedKey(column.key);
                        }}
                        onDragEnd={() => {
                          setDraggedKey(null);
                          setDropTargetKey(null);
                        }}
                      >
                        <LuGripVertical aria-hidden="true" />
                      </span>
                    )}
                    <label
                      className={styles.visibilityLabel}
                      title={definition.required ? t("tableColumns.requiredReason") : undefined}
                    >
                      <input
                        type="checkbox"
                        checked={column.visible}
                        disabled={definition.required}
                        onChange={(event) => onVisibilityChange(column.key, event.target.checked)}
                      />
                      <span>{t(definition.labelKey)}</span>
                      {definition.required && <span className={styles.required}>{t("tableColumns.required")}</span>}
                    </label>
                    <span className={styles.moveActions}>
                      <button
                        type="button"
                        disabled={!canMoveUp}
                        aria-label={t("tableColumns.moveUp", { column: t(definition.labelKey) })}
                        onClick={() => {
                          onChange(moveTableColumn(catalogue, layout, column.key, "left"));
                          announceMove(column.key, visibleIndex);
                        }}
                      >
                        <LuChevronUp aria-hidden="true" />
                      </button>
                      <button
                        type="button"
                        disabled={!canMoveDown}
                        aria-label={t("tableColumns.moveDown", { column: t(definition.labelKey) })}
                        onClick={() => {
                          onChange(moveTableColumn(catalogue, layout, column.key, "right"));
                          announceMove(column.key, visibleIndex + 2);
                        }}
                      >
                        <LuChevronDown aria-hidden="true" />
                      </button>
                    </span>
                  </li>
                );
              })}
            </ol>
          </section>
          {availableColumns.length > 0 && (
            <section aria-labelledby="available-columns-heading">
              <h3 id="available-columns-heading" className={styles.sectionHeading}>
                {t("tableColumns.availableColumns", { count: availableColumns.length })}
              </h3>
              <ul className={`${styles.columnList} ${styles.availableList}`}>
                {availableColumns.map((column) => {
                  const definition = definitionByKey.get(column.key);
                  if (!definition) return null;
                  return (
                    <li key={column.key} className={styles.columnRow}>
                      <label className={styles.visibilityLabel}>
                        <input
                          type="checkbox"
                          checked={false}
                          disabled={hasReachedVisibleLimit}
                          onChange={() => onVisibilityChange(column.key, true)}
                        />
                        <span>{t(definition.labelKey)}</span>
                      </label>
                    </li>
                  );
                })}
              </ul>
              {hasReachedVisibleLimit && (
                <p className={styles.limitMessage} role="status">
                  {t("tableColumns.visibleLimit", { count: maxVisibleColumns })}
                </p>
              )}
            </section>
          )}
          <p className={styles.srOnly} aria-live="polite">
            {announcement}
          </p>
          <div className={styles.panelActions}>
            <button className={styles.reset} type="button" onClick={() => onChange(repairTableColumnLayout(catalogue))}>
              {t("tableColumns.reset")}
            </button>
            <button className={styles.done} type="button" onClick={closePanel}>
              {t("tableColumns.done")}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

interface ColumnResizeHandleProps {
  label: string;
  width: number;
  minWidth: number;
  maxWidth: number;
  onResize: (width: number) => void;
}

export const ColumnResizeHandle: FC<ColumnResizeHandleProps> = ({ label, width, minWidth, maxWidth, onResize }) => {
  const { t } = useTranslation();
  const dragRef = useRef<{ startX: number; startWidth: number } | null>(null);

  const handlePointerDown = (event: ReactPointerEvent<HTMLSpanElement>) => {
    event.preventDefault();
    event.stopPropagation();
    dragRef.current = { startX: event.clientX, startWidth: width };
    event.currentTarget.style.setProperty("--resize-offset", "0px");
    event.currentTarget.setPointerCapture?.(event.pointerId);
  };

  const getPointerWidth = (clientX: number) => {
    if (!dragRef.current) return width;
    return Math.min(maxWidth, Math.max(minWidth, dragRef.current.startWidth + clientX - dragRef.current.startX));
  };

  return (
    <span
      className={styles.resizeHandle}
      data-print-hidden
      role="separator"
      aria-label={t("tableColumns.resize", { column: label })}
      aria-orientation="vertical"
      aria-valuemin={minWidth}
      aria-valuemax={maxWidth}
      aria-valuenow={width}
      tabIndex={0}
      onClick={(event) => event.stopPropagation()}
      onPointerDown={handlePointerDown}
      onPointerMove={(event) => {
        if (!dragRef.current) return;
        const nextWidth = getPointerWidth(event.clientX);
        event.currentTarget.style.setProperty("--resize-offset", `${nextWidth - dragRef.current.startWidth}px`);
      }}
      onPointerUp={(event) => {
        if (!dragRef.current) return;
        onResize(getPointerWidth(event.clientX));
        event.currentTarget.style.removeProperty("--resize-offset");
        dragRef.current = null;
        event.currentTarget.releasePointerCapture?.(event.pointerId);
      }}
      onPointerCancel={(event) => {
        event.currentTarget.style.removeProperty("--resize-offset");
        dragRef.current = null;
      }}
      onKeyDown={(event) => {
        if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
        event.preventDefault();
        event.stopPropagation();
        const step = event.shiftKey ? 20 : 5;
        onResize(width + (event.key === "ArrowRight" ? step : -step));
      }}
    />
  );
};
