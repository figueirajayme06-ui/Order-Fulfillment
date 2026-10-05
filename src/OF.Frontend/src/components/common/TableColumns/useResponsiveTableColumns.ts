import { useEffect, useMemo, useRef, useState } from "react";
import {
  fitTableColumns,
  getTableMinimumWidth,
  type TableColumnDefinition,
  type TableColumnLayoutItem,
} from "../../../lib/tableColumnLayout";

const INITIAL_SCROLLBAR_GUTTER = 16;

interface TableViewportMeasurement {
  contentWidth: number;
  scrollbarGutter: number;
}

export function useResponsiveTableColumns<Key extends string>(
  catalogue: readonly TableColumnDefinition<Key>[],
  layout: readonly TableColumnLayoutItem<Key>[],
  reservedWidth = 0,
) {
  const viewportRef = useRef<HTMLDivElement>(null);
  const [measurement, setMeasurement] = useState<TableViewportMeasurement>({
    contentWidth: 0,
    scrollbarGutter: INITIAL_SCROLLBAR_GUTTER,
  });

  useEffect(() => {
    const viewport = viewportRef.current;
    if (!viewport) return;

    const measure = () => {
      const contentWidth = viewport.clientWidth;
      const scrollbarGutter = Math.max(0, viewport.offsetWidth - contentWidth);
      setMeasurement((current) =>
        current.contentWidth === contentWidth && current.scrollbarGutter === scrollbarGutter
          ? current
          : { contentWidth, scrollbarGutter },
      );
    };

    measure();
    if (typeof ResizeObserver === "undefined") {
      window.addEventListener("resize", measure);
      return () => window.removeEventListener("resize", measure);
    }

    const observer = new ResizeObserver(measure);
    observer.observe(viewport);
    return () => observer.disconnect();
  }, []);

  const columns = useMemo(
    () => fitTableColumns(catalogue, layout, Math.max(0, measurement.contentWidth - reservedWidth)),
    [catalogue, layout, measurement.contentWidth, reservedWidth],
  );
  const tableWidth = columns.reduce((total, column) => total + column.width, reservedWidth);
  const minimumSurfaceWidth = getTableMinimumWidth(catalogue, layout) + reservedWidth + measurement.scrollbarGutter;

  return { columns, minimumSurfaceWidth, tableWidth, viewportRef };
}
