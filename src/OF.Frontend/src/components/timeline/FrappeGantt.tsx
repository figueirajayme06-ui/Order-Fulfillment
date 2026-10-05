import { forwardRef, useCallback, useEffect, useImperativeHandle, useRef, useState } from "react";
import Gantt from "frappe-gantt";
import { useTranslation } from "react-i18next";
import { formatTimelineDate, timelineDate, type TimelinePeriod } from "./timelineDates";
import "./frappe-gantt.css";
import {
  getTimelineDateAtPosition,
  getTimelinePositionForDate,
  getVisibleTimelineLabelStart,
} from "./timelineViewport";

export interface GanttTask {
  id: string;
  name: string;
  start: string;
  end: string;
  /** Zero-based visual row. Tasks sharing a row must not overlap. */
  row?: number;
  progress?: number;
  custom_class?: string;
  dependencies?: string;
  /** Original dates, retained when geometry is clipped. */
  period?: TimelinePeriod;
}

export type ViewMode = "Day" | "Week" | "Month" | "Year";

export interface FrappeGanttNavigation {
  jumpToToday: () => void;
  scrollToDate: (date: number) => void;
  panEarlier: () => void;
  panLater: () => void;
}

export interface FrappeGanttNavigationState {
  canPanEarlier: boolean;
  canPanLater: boolean;
}

export interface FrappeGanttVisibleRange {
  start: Date;
  end: Date;
}

interface FrappeGanttProps {
  tasks: GanttTask[];
  viewMode?: ViewMode;
  onClick?: (task: GanttTask) => void;
  onDateChange?: (task: GanttTask, start: Date, end: Date) => void;
  minimumDate?: string;
  readonlyDates?: boolean;
  readonlyProgress?: boolean;
  language?: string;
  initialScrollDate?: number | null;
  /** Moves the calendar to the beginning of an explicitly applied period. */
  focusDate?: number | null;
  /** Height reserved for a page-level sticky timeline toolbar. */
  stickyHeaderOffset?: number;
  hoveredTaskId?: string | null;
  onHoverTaskIdChange?: (taskId: string | null) => void;
  onNavigationStateChange?: (state: FrappeGanttNavigationState) => void;
  onVisibleRangeChange?: (range: FrappeGanttVisibleRange) => void;
  onViewportAnchorChange?: (date: number) => void;
}

interface GanttViewportInternals {
  dates?: Date[];
  config?: { column_width?: number };
  scroll_current?: () => void;
  set_scroll_position?: (date?: Date | string) => void;
}

const SCROLL_AMOUNT = 300;
const TIMELINE_SCROLLBAR_CLEARANCE = 10;
const SUPPORTED_VIEW_MODES: readonly ViewMode[] = ["Day", "Week", "Month", "Year"];

export const FrappeGantt = forwardRef<FrappeGanttNavigation, FrappeGanttProps>(
  (
    {
      tasks,
      viewMode = "Day",
      onClick,
      onDateChange,
      minimumDate,
      readonlyDates = false,
      readonlyProgress = false,
      language = "en",
      initialScrollDate,
      focusDate,
      stickyHeaderOffset = 0,
      hoveredTaskId,
      onHoverTaskIdChange,
      onNavigationStateChange,
      onVisibleRangeChange,
      onViewportAnchorChange,
    },
    ref,
  ) => {
    const { t, i18n } = useTranslation();
    const [activeTask, setActiveTask] = useState<GanttTask | null>(null);
    // The Frappe selector changes its own DOM. Retain that choice so a data or
    // layout update rebuilds the chart at the planner's chosen scale.
    const activeViewModeRef = useRef<ViewMode>(viewMode);
    const formatPeriod = useCallback(
      (task: GanttTask) => {
        const period = task.period ?? { start: task.start, end: task.end };
        const startDate = timelineDate(period.start);
        const endDate = timelineDate(period.end);
        if ((period.start && !startDate) || (period.end && !endDate) || (startDate && endDate && endDate < startDate)) {
          return t("timeline.invalidPeriod");
        }
        const start = startDate ? formatTimelineDate(startDate, i18n.language) : t("timeline.unknownStart");
        const end = endDate ? formatTimelineDate(endDate, i18n.language) : t("timeline.openEnded");
        return t("timeline.dateRange", { start, end });
      },
      [i18n.language, t],
    );
    const containerRef = useRef<HTMLDivElement>(null);
    const ganttRef = useRef<InstanceType<typeof Gantt> | null>(null);
    const [canScrollLeft, setCanScrollLeft] = useState(false);
    const [canScrollRight, setCanScrollRight] = useState(false);

    const getMinimumScrollLeft = useCallback(() => {
      if (!minimumDate) return 0;

      const gantt = ganttRef.current as GanttViewportInternals | null;
      const minimumTime = parseTimelineDate(minimumDate);
      if (minimumTime == null) return 0;

      return getTimelinePositionForDate(gantt?.dates, gantt?.config?.column_width, minimumTime) ?? 0;
    }, [minimumDate]);

    const updateScrollState = useCallback(() => {
      const ganttEl = containerRef.current?.querySelector(".gantt-container") as HTMLElement | null;
      if (!ganttEl) return;
      const minimumScrollLeft = getMinimumScrollLeft();
      setCanScrollLeft(ganttEl.scrollLeft > minimumScrollLeft + 1);
      setCanScrollRight(ganttEl.scrollLeft + ganttEl.clientWidth < ganttEl.scrollWidth - 1);
    }, [getMinimumScrollLeft]);

    const updateVisibleRange = useCallback(() => {
      const ganttEl = containerRef.current?.querySelector(".gantt-container") as HTMLElement | null;
      const gantt = ganttRef.current as
        | (InstanceType<typeof Gantt> & {
            dates?: Date[];
            config?: { column_width?: number };
          })
        | null;
      const columnWidth = gantt?.config?.column_width;
      const dates = gantt?.dates;

      if (!ganttEl || !columnWidth || !dates?.length) return;

      const startIndex = Math.max(0, Math.floor(ganttEl.scrollLeft / columnWidth));
      const endIndex = Math.min(dates.length - 1, Math.ceil((ganttEl.scrollLeft + ganttEl.clientWidth) / columnWidth));
      const start = dates[startIndex];
      const end = dates[endIndex];

      if (start && end) {
        onVisibleRangeChange?.({ start: new Date(start), end: new Date(end) });
      }
    }, [onVisibleRangeChange]);

    const scrollLeft = useCallback(() => {
      const ganttEl = containerRef.current?.querySelector(".gantt-container") as HTMLElement | null;
      if (!ganttEl) return;
      ganttEl.scrollLeft = Math.max(getMinimumScrollLeft(), ganttEl.scrollLeft - SCROLL_AMOUNT);
      updateScrollState();
    }, [getMinimumScrollLeft, updateScrollState]);

    const scrollRight = useCallback(() => {
      const ganttEl = containerRef.current?.querySelector(".gantt-container") as HTMLElement | null;
      if (!ganttEl) return;
      ganttEl.scrollLeft = ganttEl.scrollLeft + SCROLL_AMOUNT;
      updateScrollState();
    }, [updateScrollState]);

    const positionTimelineAtDate = useCallback((date: number) => {
      const gantt = ganttRef.current as GanttViewportInternals | null;
      gantt?.set_scroll_position?.(new Date(date));
    }, []);

    const positionTimelineAtToday = useCallback(() => {
      (ganttRef.current as GanttViewportInternals | null)?.scroll_current?.();
    }, []);

    const jumpToToday = useCallback(() => {
      requestAnimationFrame(() => {
        positionTimelineAtToday();
        updateScrollState();
        updateVisibleRange();
      });
    }, [positionTimelineAtToday, updateScrollState, updateVisibleRange]);

    useImperativeHandle(
      ref,
      () => ({
        jumpToToday,
        scrollToDate: (date) => {
          requestAnimationFrame(() => {
            positionTimelineAtDate(date);
            updateScrollState();
            updateVisibleRange();
          });
        },
        panEarlier: scrollLeft,
        panLater: scrollRight,
      }),
      [jumpToToday, positionTimelineAtDate, scrollLeft, scrollRight, updateScrollState, updateVisibleRange],
    );

    useEffect(() => {
      onNavigationStateChange?.({
        canPanEarlier: canScrollLeft,
        canPanLater: canScrollRight,
      });
    }, [canScrollLeft, canScrollRight, onNavigationStateChange]);

    useEffect(() => {
      if (!containerRef.current || tasks.length === 0) return;

      const activeViewMode = activeViewModeRef.current;
      containerRef.current.innerHTML = "<svg></svg>";
      const taskById = new Map(tasks.map((task) => [task.id, task]));
      const escapedTasks = tasks.map((task) => ({
        ...task,
        name: escapeHtml(task.name),
      }));

      ganttRef.current = new Gantt(containerRef.current, escapedTasks, {
        view_mode: activeViewMode,
        language,
        readonly_dates: readonlyDates,
        readonly_progress: readonlyProgress,
        bar_height: 24,
        padding: 14,
        bar_corner_radius: 4,
        today_button: true,
        view_mode_select: true,
        view_modes: minimumDate ? viewModesIncludingToday(tasks) : [...SUPPORTED_VIEW_MODES],
        infinite_padding: minimumDate ? false : true,
        scroll_to: "today",
        popup: ({
          task,
          set_title,
          set_subtitle,
          set_details,
        }: {
          task: GanttTask;
          set_title: (h: string) => void;
          set_subtitle: (h: string) => void;
          set_details: (h: string) => void;
        }) => {
          set_title(escapeHtml(taskById.get(task.id)?.name ?? task.name));
          set_subtitle(escapeHtml(formatPeriod(task)));
          set_details("");
        },
        on_click: (task: GanttTask) => {
          onClick?.(task);
        },
        on_date_change: (task: GanttTask, start: Date, end: Date) => {
          onDateChange?.(task, start, end);
        },
      });

      const configuredModeSelect = containerRef.current.querySelector(
        ".side-header select",
      ) as HTMLSelectElement | null;
      if (configuredModeSelect && configuredModeSelect.value !== activeViewMode) {
        configuredModeSelect.value = activeViewMode;
        configuredModeSelect.dispatchEvent(new Event("change", { bubbles: true }));
      }

      // Block wheel scroll on the gantt-container but allow vertical page scroll
      const ganttEl = containerRef.current.querySelector(".gantt-container") as HTMLElement | null;
      if (ganttEl) {
        ganttEl.dataset.viewMode = configuredModeSelect?.value ?? activeViewMode;

        const reportViewportAnchor = () => {
          const gantt = ganttRef.current as GanttViewportInternals | null;
          const anchor = getTimelineDateAtPosition(
            gantt?.dates,
            gantt?.config?.column_width,
            ganttEl.scrollLeft + ganttEl.clientWidth / 2,
          );
          if (anchor != null) {
            onViewportAnchorChange?.(anchor);
          }
        };

        const blockWheel = (e: WheelEvent) => {
          if (e.shiftKey || Math.abs(e.deltaX) > Math.abs(e.deltaY)) {
            e.stopPropagation();
          } else {
            e.stopPropagation();
          }
        };

        const onHover = (e: MouseEvent) => {
          const target = e.target as Element | null;
          const wrapper = target?.closest(".bar-wrapper") as SVGGElement | null;
          const id = wrapper?.getAttribute("data-id") ?? null;
          onHoverTaskIdChange?.(id);
          if (id && !wrapper?.classList.contains("event-none")) setActiveTask(taskById.get(id) ?? null);
        };

        const onLeave = () => {
          onHoverTaskIdChange?.(null);
        };

        const onFocus = (event: FocusEvent) => {
          const wrapper = (event.target as Element).closest(".bar-wrapper");
          const id = wrapper?.getAttribute("data-id");
          if (id) {
            setActiveTask(taskById.get(id) ?? null);
            onHoverTaskIdChange?.(id);
          }
        };
        const onTaskKeyDown = (event: KeyboardEvent) => {
          const wrapper = (event.target as Element).closest(".bar-wrapper");
          const id = wrapper?.getAttribute("data-id");
          if (id && (event.key === "Enter" || event.key === " ")) {
            event.preventDefault();
            const task = taskById.get(id);
            if (task) onClick?.(task);
          }
        };
        const annotateBars = () => {
          compactTaskRows(ganttEl, tasks);
          reserveTimelineScrollbarSpace(ganttEl);
          ganttEl.querySelectorAll(".bar-wrapper").forEach((wrapper) => {
            const task = taskById.get(wrapper.getAttribute("data-id") ?? "");
            if (!task || task.custom_class === "event-none") {
              wrapper.setAttribute("aria-hidden", "true");
              wrapper.removeAttribute("tabindex");
              return;
            }
            wrapper.setAttribute("tabindex", "0");
            wrapper.setAttribute("role", onClick ? "button" : "img");
            wrapper.setAttribute("aria-label", `${task.name}: ${formatPeriod(task)}`);
          });
        };
        annotateBars();
        // Frappe rebuilds bars when view modes or date-grid padding change.
        const barObserver = new MutationObserver(annotateBars);
        barObserver.observe(ganttEl, { childList: true, subtree: true });
        ganttEl.addEventListener("focusin", onFocus);
        ganttEl.addEventListener("keydown", onTaskKeyDown);

        const onScroll = () => {
          updateVisibleBarLabels(ganttEl);
          updateScrollState();
          updateVisibleRange();
          reportViewportAnchor();
        };

        const verticalScrollParent = findVerticalScrollParent(containerRef.current);
        const verticalScrollTarget = verticalScrollParent ?? window;
        let pinnedHeaderFrame = 0;
        const updatePinnedCalendarHeader = () => {
          const calendarHeader = containerRef.current?.querySelector(".grid-header") as HTMLElement | null;
          if (!calendarHeader) return;

          const viewportTop = verticalScrollParent?.getBoundingClientRect().top ?? 0;
          const ganttBounds = ganttEl.getBoundingClientRect();
          const maximumOffset = Math.max(0, ganttEl.clientHeight - calendarHeader.offsetHeight);
          const desiredTop = Math.max(ganttBounds.top, viewportTop + stickyHeaderOffset);
          const offset = Math.min(maximumOffset, Math.max(0, desiredTop - ganttBounds.top));
          calendarHeader.style.transform = `translateY(${Math.round(offset)}px)`;
        };
        const schedulePinnedCalendarHeader = () => {
          cancelAnimationFrame(pinnedHeaderFrame);
          pinnedHeaderFrame = requestAnimationFrame(updatePinnedCalendarHeader);
        };
        const onViewModeChange = (event: Event) => {
          if (!(event.target instanceof HTMLSelectElement) || !event.target.matches(".viewmode-select")) return;

          ganttEl.dataset.viewMode = event.target.value;
          if (SUPPORTED_VIEW_MODES.includes(event.target.value as ViewMode)) {
            activeViewModeRef.current = event.target.value as ViewMode;
          }

          requestAnimationFrame(() => {
            positionTimelineAtToday();
            updateVisibleBarLabels(ganttEl);
            updateScrollState();
            updateVisibleRange();
            updatePinnedCalendarHeader();
          });
        };

        ganttEl.addEventListener("wheel", blockWheel, { passive: false, capture: true });
        ganttEl.addEventListener("mousemove", onHover);
        ganttEl.addEventListener("mouseleave", onLeave);
        ganttEl.addEventListener("scroll", onScroll, { passive: true });
        ganttEl.addEventListener("change", onViewModeChange);
        verticalScrollTarget.addEventListener("scroll", schedulePinnedCalendarHeader, { passive: true });
        const onWindowResize = () => {
          schedulePinnedCalendarHeader();
          requestAnimationFrame(() => updateVisibleBarLabels(ganttEl));
        };
        window.addEventListener("resize", onWindowResize);
        requestAnimationFrame(() => {
          const nativeScrollTarget = focusDate ?? initialScrollDate;
          if (nativeScrollTarget != null) positionTimelineAtDate(nativeScrollTarget);
          else positionTimelineAtToday();
          updateVisibleBarLabels(ganttEl);
          updateScrollState();
          updateVisibleRange();
          reportViewportAnchor();
          updatePinnedCalendarHeader();
        });

        return () => {
          cancelAnimationFrame(pinnedHeaderFrame);
          barObserver.disconnect();
          ganttEl.removeEventListener("focusin", onFocus);
          ganttEl.removeEventListener("keydown", onTaskKeyDown);
          ganttEl.removeEventListener("wheel", blockWheel, { capture: true } as EventListenerOptions);
          ganttEl.removeEventListener("mousemove", onHover);
          ganttEl.removeEventListener("mouseleave", onLeave);
          ganttEl.removeEventListener("scroll", onScroll);
          ganttEl.removeEventListener("change", onViewModeChange);
          verticalScrollTarget.removeEventListener("scroll", schedulePinnedCalendarHeader);
          window.removeEventListener("resize", onWindowResize);
        };
      }

      return;
    }, [
      tasks,
      readonlyDates,
      readonlyProgress,
      language,
      onClick,
      onDateChange,
      getMinimumScrollLeft,
      positionTimelineAtToday,
      updateScrollState,
      updateVisibleRange,
      onHoverTaskIdChange,
      onViewportAnchorChange,
      formatPeriod,
      focusDate,
      initialScrollDate,
      positionTimelineAtDate,
      stickyHeaderOffset,
    ]);

    useEffect(() => {
      const ganttEl = containerRef.current?.querySelector(".gantt-container") as HTMLElement | null;
      if (!ganttEl) return;

      let hasVisibleHoveredTask = false;
      const hoveredTaskIndex = hoveredTaskId ? tasks.findIndex((task) => task.id === hoveredTaskId) : -1;
      const hoveredRow = hoveredTaskIndex < 0 ? null : (tasks[hoveredTaskIndex].row ?? hoveredTaskIndex);
      ganttEl.querySelectorAll(".bar-wrapper").forEach((node) => {
        const bar = node as SVGGElement;
        const id = bar.getAttribute("data-id");
        if (id && hoveredTaskId === id) {
          bar.classList.add("is-hovered");
          hasVisibleHoveredTask = !bar.classList.contains("event-none");
        } else {
          bar.classList.remove("is-hovered");
        }
      });

      const hoveredHighlightClass = hoveredTaskId ? `highlight-${hoveredTaskId}` : null;
      ganttEl.querySelectorAll(".date-range-highlight").forEach((node) => {
        const isHoveredRange =
          hasVisibleHoveredTask && hoveredHighlightClass !== null && node.classList.contains(hoveredHighlightClass);
        node.classList.toggle("hide", !isHoveredRange);
      });
      const svg = ganttEl.querySelector<SVGSVGElement>("svg.gantt");
      const existingRowHighlight = svg?.querySelector<SVGRectElement>(".timeline-row-highlight");
      const hoveredGridRow =
        hoveredRow == null ? null : ganttEl.querySelectorAll<SVGRectElement>(".grid-row")[hoveredRow];
      if (!svg || !hoveredGridRow) {
        existingRowHighlight?.remove();
        return;
      }

      const rowHighlight = existingRowHighlight ?? document.createElementNS("http://www.w3.org/2000/svg", "rect");
      rowHighlight.setAttribute("class", "timeline-row-highlight");
      for (const attribute of ["x", "y", "width", "height"]) {
        const value = hoveredGridRow.getAttribute(attribute);
        if (value != null) rowHighlight.setAttribute(attribute, value);
      }
      (svg.querySelector("g.progress") ?? svg).appendChild(rowHighlight);
    }, [hoveredTaskId, tasks]);

    useEffect(() => {
      return () => {
        if (containerRef.current) {
          containerRef.current.innerHTML = "";
        }
        ganttRef.current = null;
      };
    }, []);

    const handleKeyDown = useCallback(
      (e: React.KeyboardEvent<HTMLDivElement>) => {
        if (e.key !== "ArrowLeft" && e.key !== "ArrowRight") return;
        e.preventDefault();
        const step = e.shiftKey ? SCROLL_AMOUNT * 3 : SCROLL_AMOUNT;
        const ganttEl = containerRef.current?.querySelector(".gantt-container") as HTMLElement | null;
        if (!ganttEl) return;

        if (e.key === "ArrowLeft") {
          ganttEl.scrollLeft = Math.max(getMinimumScrollLeft(), ganttEl.scrollLeft - step);
        } else {
          ganttEl.scrollLeft = ganttEl.scrollLeft + step;
        }
        updateScrollState();
      },
      [getMinimumScrollLeft, updateScrollState],
    );

    if (tasks.length === 0) {
      return (
        <p style={{ color: "var(--colour-text-muted)", padding: "2rem", textAlign: "center" }}>{t("timeline.empty")}</p>
      );
    }

    return (
      <div className="gantt-shell" style={{ width: "100%", height: "100%" }} tabIndex={0} onKeyDown={handleKeyDown}>
        <div ref={containerRef} style={{ width: "100%", height: "100%" }} />
        <div className="timeline-task-details" aria-live="polite" data-print-hidden>
          {activeTask && tasks.some((task) => task.id === activeTask.id)
            ? `${activeTask.name}: ${formatPeriod(activeTask)}`
            : t("timeline.dateHint")}
        </div>
      </div>
    );
  },
);

FrappeGantt.displayName = "FrappeGantt";

function findVerticalScrollParent(element: HTMLElement): HTMLElement | null {
  let parent = element.parentElement;

  while (parent) {
    const overflowY = window.getComputedStyle(parent).overflowY;
    if ((overflowY === "auto" || overflowY === "scroll") && parent.scrollHeight > parent.clientHeight) {
      return parent;
    }
    parent = parent.parentElement;
  }

  return null;
}

function viewModesIncludingToday(tasks: readonly GanttTask[]) {
  const today = new Date().setHours(0, 0, 0, 0);
  const starts = tasks.map((task) => parseTimelineDate(task.start)).filter((date) => date != null);
  const ends = tasks.map((task) => parseTimelineDate(task.end)).filter((date) => date != null);
  const daysBefore = Math.ceil((Math.min(...starts) - today) / 86_400_000);
  const daysAfter = Math.ceil((today - Math.max(...ends)) / 86_400_000);

  // Native "Today" navigation needs today inside the calendar. Extend calendar
  // padding, not task dates, and leave room beyond today for the visible viewport.
  return SUPPORTED_VIEW_MODES.map((name) => {
    const mode = Gantt.VIEW_MODE[name.toUpperCase()];
    const [before, after] = typeof mode.padding === "string" ? [mode.padding, mode.padding] : mode.padding;
    return {
      ...mode,
      padding: [daysBefore > 0 ? `${daysBefore + 31}d` : before, daysAfter > 0 ? `${daysAfter + 31}d` : after] as [
        string,
        string,
      ],
    };
  });
}

function compactTaskRows(container: HTMLElement, tasks: readonly GanttTask[]): void {
  const taskRows = tasks.map((task, index) => task.row ?? index);
  if (!taskRows.some((row, index) => row !== index)) return;

  const rowHeight = 38;
  const rowCount = Math.max(...taskRows) + 1;
  const taskIndexes = new Map(tasks.map((task, index) => [task.id, index]));

  container.querySelectorAll<SVGGElement>(".bar-wrapper").forEach((wrapper) => {
    const taskIndex = taskIndexes.get(wrapper.dataset.id ?? "");
    if (taskIndex == null) return;
    const offset = (taskRows[taskIndex] - taskIndex) * rowHeight;
    wrapper.setAttribute("transform", `translate(0 ${offset})`);
  });

  const svg = container.querySelector<SVGSVGElement>("svg.gantt");
  const gridRows = Array.from(container.querySelectorAll<SVGRectElement>(".grid-row"));
  const headerHeight = Number(gridRows[0]?.getAttribute("y"));
  if (!svg || !Number.isFinite(headerHeight)) return;

  const gridHeight = headerHeight + rowCount * rowHeight + 4;
  svg.setAttribute("height", String(gridHeight));
  container.style.height = `${gridHeight}px`;
  container.querySelector<SVGRectElement>(".grid-background")?.setAttribute("height", String(gridHeight));

  gridRows.forEach((row, index) => {
    row.style.display = index < rowCount ? "" : "none";
  });
  container.querySelectorAll<SVGLineElement>(".row-line").forEach((line, index) => {
    line.style.display = index < rowCount ? "" : "none";
  });
  container.querySelectorAll<SVGLineElement>(".tick").forEach((tick) => {
    tick.setAttribute("y2", String(gridHeight));
  });
  container.querySelectorAll<SVGRectElement>(".grid-column, .holiday-highlight").forEach((column) => {
    column.setAttribute("height", String(gridHeight - headerHeight));
  });
  const currentHighlight = container.querySelector<HTMLElement>(".current-highlight");
  if (currentHighlight) currentHighlight.style.height = `${gridHeight - headerHeight}px`;
}

function reserveTimelineScrollbarSpace(container: HTMLElement): void {
  const svg = container.querySelector<SVGSVGElement>("svg.gantt");
  const gridHeight = Number(svg?.getAttribute("height"));
  if (!Number.isFinite(gridHeight)) return;

  container.style.setProperty("--gv-grid-height", `${gridHeight + TIMELINE_SCROLLBAR_CLEARANCE}px`);
  container.style.height = "var(--gv-grid-height)";
}

function parseTimelineDate(value: string): number | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return null;

  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return Number.isNaN(date.getTime()) ? null : date.getTime();
}

function updateVisibleBarLabels(container: HTMLElement): void {
  const viewportStart = container.scrollLeft + 8;
  const viewportEnd = container.scrollLeft + container.clientWidth - 8;

  container.querySelectorAll(".bar-wrapper").forEach((wrapper) => {
    const bar = wrapper.querySelector(".bar") as SVGRectElement | null;
    const label = wrapper.querySelector(".bar-label") as SVGTextElement | null;
    if (!bar || !label) return;

    const barStart = Number(bar.getAttribute("x"));
    const barWidth = Number(bar.getAttribute("width"));
    if (!Number.isFinite(barStart) || !Number.isFinite(barWidth)) return;

    const visibleBarWidth = Math.min(barStart + barWidth, viewportEnd) - Math.max(barStart, viewportStart);
    const fullLabel = label.dataset.fullLabel ?? label.textContent ?? "";
    label.dataset.fullLabel = fullLabel;
    label.textContent = fitTimelineLabel(label, fullLabel, visibleBarWidth - 12);

    const labelWidth = label.getBBox().width;
    const labelStart = getVisibleTimelineLabelStart(barStart, barWidth, labelWidth, viewportStart, viewportEnd);
    if (labelStart == null) return;

    label.setAttribute("x", String(labelStart));
  });
}

function fitTimelineLabel(label: SVGTextElement, text: string, availableWidth: number): string {
  if (availableWidth <= 0) return "";

  label.textContent = text;
  if (label.getComputedTextLength() <= availableWidth) return text;

  const ellipsis = "…";
  label.textContent = ellipsis;
  if (label.getComputedTextLength() > availableWidth) return "";

  let low = 0;
  let high = text.length;
  while (low < high) {
    const middle = Math.ceil((low + high) / 2);
    const candidate = `${text.slice(0, middle).trimEnd()}${ellipsis}`;
    label.textContent = candidate;
    if (label.getComputedTextLength() <= availableWidth) {
      low = middle;
    } else {
      high = middle - 1;
    }
  }

  return `${text.slice(0, low).trimEnd()}${ellipsis}`;
}

function escapeHtml(value: string): string {
  return value.replace(
    /[&<>"']/g,
    (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[character]!,
  );
}
