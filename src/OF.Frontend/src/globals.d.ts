declare module "*.module.css" {
  const classes: { readonly [key: string]: string };
  export default classes;
}

declare module "frappe-gantt" {
  interface Task {
    id: string;
    name: string;
    start: string;
    end: string;
    progress?: number;
    custom_class?: string;
    dependencies?: string;
  }

  interface GanttOptions {
    view_mode?: string;
    language?: string;
    readonly_dates?: boolean;
    readonly_progress?: boolean;
    readonly?: boolean;
    infinite_padding?: boolean;
    bar_height?: number;
    padding?: number;
    bar_corner_radius?: number;
    today_button?: boolean;
    view_mode_select?: boolean;
    view_modes?: (string | GanttViewMode)[];
    scroll_to?: string;
    popup?: (params: {
      task: Task;
      set_title: (h: string) => void;
      set_subtitle: (h: string) => void;
      set_details: (h: string) => void;
    }) => void | string | false;
    on_click?: (task: Task) => void;
    on_date_change?: (task: Task, start: Date, end: Date) => void;
  }

  interface GanttViewMode {
    name: string;
    padding: string | [string, string];
  }

  export default class Gantt {
    static VIEW_MODE: Record<string, GanttViewMode>;
    constructor(element: string | HTMLElement, tasks: Task[], options?: GanttOptions);
    change_view_mode(mode: string): void;
    refresh(tasks: Task[]): void;
    scroll_current(): void;
  }
}
