import { useEffect, useState, type RefObject } from "react";

/**
 * Keeps a timeline's left table header directly below its sticky toolbar.
 *
 * The table needs horizontal scrolling, which makes native CSS sticky
 * positioning unreliable for its header. This small hook measures the toolbar
 * and translates the header only while it would otherwise scroll underneath it.
 */
export function useTimelineStickyHeaders(
  metaBarRef: RefObject<HTMLElement | null>,
  tableRef: RefObject<HTMLElement | null>,
  contentVersion: unknown,
) {
  const [stickyHeaderOffset, setStickyHeaderOffset] = useState(0);

  useEffect(() => {
    const metaBar = metaBarRef.current;
    if (!metaBar) return;

    const updateOffset = () => setStickyHeaderOffset(Math.ceil(metaBar.getBoundingClientRect().height));
    updateOffset();

    const observer = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(updateOffset);
    observer?.observe(metaBar);
    window.addEventListener("resize", updateOffset);
    return () => {
      observer?.disconnect();
      window.removeEventListener("resize", updateOffset);
    };
  }, [metaBarRef]);

  useEffect(() => {
    const table = tableRef.current;
    const metaBar = metaBarRef.current;
    const tableHead = table?.querySelector("thead") as HTMLElement | null;
    if (!table || !metaBar || !tableHead) return;

    let frame: number | null = null;
    const updatePosition = () => {
      frame = null;
      const metaBarBounds = metaBar.getBoundingClientRect();
      const tableBounds = table.getBoundingClientRect();

      if (tableBounds.top >= metaBarBounds.bottom || tableBounds.bottom <= metaBarBounds.bottom) {
        tableHead.style.transform = "";
        return;
      }

      const maximumOffset = Math.max(0, tableBounds.height - tableHead.offsetHeight);
      const offset = Math.min(maximumOffset, Math.max(0, metaBarBounds.bottom - tableBounds.top));
      tableHead.style.transform = `translateY(${Math.round(offset)}px)`;
    };
    const scheduleUpdate = () => {
      if (frame == null) frame = requestAnimationFrame(updatePosition);
    };

    scheduleUpdate();
    window.addEventListener("scroll", scheduleUpdate, true);
    window.addEventListener("resize", scheduleUpdate);
    return () => {
      if (frame != null) cancelAnimationFrame(frame);
      window.removeEventListener("scroll", scheduleUpdate, true);
      window.removeEventListener("resize", scheduleUpdate);
      tableHead.style.transform = "";
    };
  }, [contentVersion, metaBarRef, stickyHeaderOffset, tableRef]);

  return stickyHeaderOffset;
}
