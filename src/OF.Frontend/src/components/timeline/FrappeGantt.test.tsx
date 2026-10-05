import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import "../../i18n";
import { FrappeGantt, type GanttTask } from "./FrappeGantt";

const captured = vi.hoisted(() => ({
  tasks: [] as GanttTask[],
  popup: null as null | ((value: unknown) => void),
  options: null as null | Record<string, unknown>,
  constructorCount: 0,
  scrollCurrent: vi.fn(),
  scrollToDate: vi.fn(),
  scrollTo: vi.fn(),
}));
vi.mock("frappe-gantt", () => ({
  default: class {
    static VIEW_MODE = Object.fromEntries(
      ["Day", "Week", "Month", "Year"].map((name) => [name.toUpperCase(), { name, padding: "7d" }]),
    );
    dates: Date[];
    config: { column_width: number };

    constructor(
      element: HTMLElement,
      tasks: GanttTask[],
      options: { popup: (value: unknown) => void } & Record<string, unknown>,
    ) {
      captured.tasks = tasks;
      captured.popup = options.popup;
      captured.options = options;
      captured.constructorCount += 1;
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      this.dates = Array.from({ length: 181 }, (_, index) => {
        const date = new Date(today);
        date.setDate(date.getDate() + index - 90);
        return date;
      });
      this.config = { column_width: options.view_mode === "Week" ? 140 : 45 };
      const container = document.createElement("div");
      container.className = "gantt-container";
      Object.defineProperties(container, {
        clientWidth: { configurable: true, value: 600 },
        scrollWidth: { configurable: true, value: 9000 },
        scrollTo: {
          configurable: true,
          value: (options: ScrollToOptions) => {
            captured.scrollTo(options);
            container.scrollLeft = Number(options.left ?? container.scrollLeft);
          },
        },
      });
      const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      svg.setAttribute("class", "gantt");
      svg.setAttribute("height", "114");
      const progressLayer = document.createElementNS("http://www.w3.org/2000/svg", "g");
      progressLayer.setAttribute("class", "progress");
      svg.appendChild(progressLayer);
      container.appendChild(svg);
      const sideHeader = document.createElement("div");
      sideHeader.className = "side-header";
      const viewModeSelect = document.createElement("select");
      viewModeSelect.className = "viewmode-select";
      for (const mode of ["Day", "Week", "Month"]) {
        viewModeSelect.appendChild(new Option(mode, mode));
      }
      viewModeSelect.value = String(options.view_mode ?? "Day");
      viewModeSelect.addEventListener("change", () => {
        this.config.column_width = viewModeSelect.value === "Week" ? 140 : 45;
      });
      sideHeader.appendChild(viewModeSelect);
      container.appendChild(sideHeader);
      const rowCount = Math.max(0, ...tasks.map((task, index) => task.row ?? index)) + 1;
      for (let index = 0; index < rowCount; index += 1) {
        const gridRow = document.createElementNS("http://www.w3.org/2000/svg", "rect");
        gridRow.setAttribute("class", "grid-row");
        gridRow.setAttribute("x", "0");
        gridRow.setAttribute("y", String(index * 38));
        gridRow.setAttribute("width", "9000");
        gridRow.setAttribute("height", "38");
        svg.appendChild(gridRow);
      }
      for (const task of tasks) {
        const bar = document.createElementNS("http://www.w3.org/2000/svg", "g");
        bar.setAttribute("class", `bar-wrapper ${task.custom_class ?? ""}`);
        bar.setAttribute("data-id", task.id);
        container.appendChild(bar);
      }
      element.appendChild(container);
    }

    scroll_current() {
      captured.scrollCurrent();
    }

    set_scroll_position(date: Date | string) {
      captured.scrollToDate(date);
    }
  },
}));
afterEach(() => {
  cleanup();
  captured.options = null;
  captured.constructorCount = 0;
  captured.scrollCurrent.mockReset();
  captured.scrollToDate.mockReset();
  captured.scrollTo.mockReset();
  vi.unstubAllGlobals();
});

describe("Timeline date accessibility", () => {
  it("reserves space for the horizontal scrollbar without changing the grid height", () => {
    render(<FrappeGantt tasks={[{ id: "event-1", name: "Reservation", start: "2026-09-01", end: "2026-09-10" }]} />);

    const container = document.querySelector<HTMLElement>(".gantt-container")!;
    expect(container.style.getPropertyValue("--gv-grid-height")).toBe("124px");
    expect(container.style.height).toBe("var(--gv-grid-height)");
    expect(document.querySelector("svg.gantt")).toHaveAttribute("height", "114");
  });

  it("exposes original dates on keyboard focus even when geometry is clipped", () => {
    render(
      <FrappeGantt
        tasks={[
          {
            id: "reservation-1",
            name: "Reserved asset",
            start: "2026-08-01",
            end: "2026-08-20",
            period: { start: "2024-01-01", end: "2026-08-20" },
          },
        ]}
      />,
    );
    const bar = screen.getByRole("img", { name: /Reserved asset:.*2024.*2026/ });
    expect(bar).toHaveAttribute("tabindex", "0");
    fireEvent.focusIn(bar);
    expect(document.querySelector(".timeline-task-details")).toHaveTextContent(/Reserved asset:.*2024.*2026/);
    expect(captured.tasks[0].name).toBe("Reserved asset");
  });

  it("preserves keyboard activation and hides empty alignment bars from keyboard and screen readers", () => {
    const onClick = vi.fn();
    render(
      <FrappeGantt
        onClick={onClick}
        tasks={[
          { id: "reservation", name: "Reservation", start: "2026-08-01", end: "2026-08-01" },
          { id: "empty", name: "", start: "2026-08-01", end: "2026-08-01", custom_class: "event-none" },
        ]}
      />,
    );
    const bar = screen.getByRole("button", { name: /Reservation:/ });
    fireEvent.keyDown(bar, { key: "Enter" });
    fireEvent.keyDown(bar, { key: " " });
    expect(onClick).toHaveBeenCalledTimes(2);
    expect(onClick).toHaveBeenLastCalledWith(expect.objectContaining({ id: "reservation" }));
    expect(document.querySelector('[data-id="empty"]')).toHaveAttribute("aria-hidden", "true");
    expect(document.querySelector('[data-id="empty"]')).not.toHaveAttribute("tabindex");
  });

  it("shows unknown source dates and escapes names passed to third-party HTML sinks", () => {
    render(
      <FrappeGantt
        tasks={[
          {
            id: "reservation",
            name: "<img src=x onerror=alert(1)>",
            start: "2026-08-01",
            end: "2026-08-01",
            period: { start: null, end: null },
          },
        ]}
      />,
    );
    expect(screen.getByRole("img")).toHaveAccessibleName(/Start unknown.*Open-ended/);
    expect(captured.tasks[0].name).toContain("&lt;img");
    const setTitle = vi.fn();
    const setSubtitle = vi.fn();
    captured.popup?.({ task: captured.tasks[0], set_title: setTitle, set_subtitle: setSubtitle, set_details: vi.fn() });
    expect(setTitle).toHaveBeenCalledWith("&lt;img src=x onerror=alert(1)&gt;");
    expect(setSubtitle).toHaveBeenCalledWith(expect.stringMatching(/Start unknown.*Open-ended/));
    expect(document.querySelector("img")).toBeNull();
  });

  it("initializes at today and does not reset a stable timeline on rerender", () => {
    const tasks: GanttTask[] = [{ id: "event-1", name: "Reservation", start: "2026-09-01", end: "2026-09-10" }];
    const { rerender } = render(<FrappeGantt tasks={tasks} viewMode="Week" readonlyDates readonlyProgress />);

    expect(captured.constructorCount).toBe(1);
    expect(captured.options).toMatchObject({
      view_mode: "Week",
      scroll_to: "today",
      readonly_dates: true,
      readonly_progress: true,
    });

    rerender(<FrappeGantt tasks={tasks} viewMode="Week" readonlyDates readonlyProgress />);
    expect(captured.constructorCount).toBe(1);
  });

  it("prioritises a newly applied period over the previous viewport anchor", async () => {
    const tasks: GanttTask[] = [{ id: "event-1", name: "Reservation", start: "2026-09-01", end: "2026-12-31" }];
    const { rerender } = render(<FrappeGantt tasks={tasks} initialScrollDate={new Date(2026, 7, 20).getTime()} />);
    rerender(<FrappeGantt tasks={[...tasks]} focusDate={Date.now() + 24 * 60 * 60 * 1000} />);

    await waitFor(() => expect(captured.scrollToDate).toHaveBeenCalled());
  });

  it("highlights the calendar lane that corresponds to the hovered task", () => {
    const tasks: GanttTask[] = [
      { id: "first", name: "First", start: "2026-09-01", end: "2026-09-02" },
      { id: "second", name: "Second", start: "2026-09-01", end: "2026-09-02", row: 2 },
    ];
    const { rerender } = render(<FrappeGantt tasks={tasks} hoveredTaskId="second" />);

    expect(document.querySelector(".timeline-row-highlight")).toHaveAttribute("y", "76");

    rerender(<FrappeGantt tasks={tasks} hoveredTaskId={null} />);
    expect(document.querySelector(".timeline-row-highlight")).toBeNull();
  });

  it("jumps to today after the timescale changes", async () => {
    render(
      <FrappeGantt
        tasks={[{ id: "event-1", name: "Reservation", start: "2026-09-01", end: "2026-09-10" }]}
        viewMode="Day"
      />,
    );

    await waitFor(() => expect(captured.scrollCurrent).toHaveBeenCalled());
    const callsBeforeChange = captured.scrollCurrent.mock.calls.length;
    fireEvent.change(document.querySelector(".viewmode-select")!, { target: { value: "Week" } });

    await waitFor(() => expect(captured.scrollCurrent).toHaveBeenCalledTimes(callsBeforeChange + 1));
    expect(document.querySelector(".gantt-container")).toHaveAttribute("data-view-mode", "Week");
  });

  it("retains the planner-selected timescale when task data rebuilds the chart", async () => {
    const tasks: GanttTask[] = [{ id: "event-1", name: "Reservation", start: "2026-09-01", end: "2026-09-10" }];
    const { rerender } = render(<FrappeGantt tasks={tasks} viewMode="Day" />);

    fireEvent.change(document.querySelector(".viewmode-select")!, { target: { value: "Week" } });
    rerender(
      <FrappeGantt
        tasks={[...tasks, { id: "event-2", name: "Service", start: "2026-09-12", end: "2026-09-14" }]}
        viewMode="Day"
      />,
    );

    await waitFor(() => expect(captured.options).toMatchObject({ view_mode: "Week" }));
  });

  it("positions today after changing from a larger timescale back to days", async () => {
    render(
      <FrappeGantt
        tasks={[{ id: "event-1", name: "Reservation", start: "2026-09-01", end: "2026-09-10" }]}
        viewMode="Week"
      />,
    );

    await waitFor(() => expect(captured.scrollCurrent).toHaveBeenCalled());
    const callsBeforeChange = captured.scrollCurrent.mock.calls.length;
    fireEvent.change(document.querySelector(".viewmode-select")!, { target: { value: "Day" } });

    await waitFor(() => expect(captured.scrollCurrent).toHaveBeenCalledTimes(callsBeforeChange + 1));
    expect(document.querySelector(".gantt-container")).toHaveAttribute("data-view-mode", "Day");
  });

  it("uses Frappe's native today behaviour after a view-mode change", async () => {
    render(
      <FrappeGantt
        tasks={[{ id: "event-1", name: "Reservation", start: "2026-09-01", end: "2026-09-10" }]}
        viewMode="Week"
      />,
    );

    await waitFor(() => expect(captured.scrollCurrent).toHaveBeenCalled());
    const callsBeforeChange = captured.scrollCurrent.mock.calls.length;
    fireEvent.change(document.querySelector(".viewmode-select")!, { target: { value: "Day" } });

    await waitFor(() => expect(captured.scrollCurrent).toHaveBeenCalledTimes(callsBeforeChange + 1));
  });
});
