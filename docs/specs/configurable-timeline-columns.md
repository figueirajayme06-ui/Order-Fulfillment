# Feature specification: Configurable timeline context columns

## 1. Objective

**User and job:** Fleet planners reviewing Agreement and Asset timelines need to choose the row context that supports
their planning decision while keeping the calendar and event bars in view.

**Problem:** Timeline context columns are hard-coded. Agreement and Asset timelines also use different pane-width
behaviour, so adding more fields directly would allow supporting data to compete with the calendar.

**Outcome:** Users can show, hide, reorder, and resize an approved set of timeline context columns while a protected
calendar viewport remains the primary work surface.

## 2. Scope and boundaries

### In scope

- Agreement and Asset list timeline modes.
- Separate timeline column catalogues and layouts; table and timeline preferences do not overwrite one another.
- Reuse of the v2 Columns panel, layout repair, drag/keyboard reorder, reset, and bounded column resizing.
- A resizable context pane on both timelines with a calendar-first maximum width.
- Backward-compatible saved-view persistence for timeline column layouts.
- Dynamic timeline headers, filters, cells, widths, and empty-state spans.

### Out of scope

- The Agreement-detail timeline, which has no separate row-context table.
- Arbitrary API/database fields, calculated columns, sticky columns, grouping, or aggregation.
- Changing timeline sorting, event-range calculation, pagination, or event semantics.
- Persisting timeline pane width in shared saved views.

### Constraints and known rules

- The calendar stays visible and receives at least 60% of the timeline surface at normal desktop widths.
- Agreement Status and Agreement number remain required. Asset Status and Asset ID remain required; the Asset
  selection checkbox stays outside the saved column layout.
- Agreements may show at most five context columns and Assets at most four. Users swap supporting fields instead of
  recreating the full table beside the calendar.
- The current visible timeline fields remain the product defaults.
- Hiding a filtered column clears its filter so an invisible filter cannot explain missing rows.
- New copy is localised and all controls retain complete keyboard paths, visible focus, and print-safe behaviour.

## 3. User experience

### Primary flow

1. The user opens **Columns** from the page filter toolbar beside **Reset filters**, in the same location used by the
   table view.
2. They hide, restore, reorder, or reset timeline context fields using the same interaction as table column v2.
3. They resize a visible column or the whole context pane using pointer or keyboard controls.
4. The context pane fits or scrolls within its fixed budget; the calendar does not shrink in response to column changes.
5. Saving a view stores the timeline layout independently from the table layout.

### Information hierarchy

- The visible period, navigation, calendar grid, and event bars remain the primary timeline content.
- Required row identity stays visible in the context pane.
- Optional customer, location, description, and supporting fields are progressive disclosure through Columns.
- The existing palette, Inter Tight data type, Space Grotesk headings, and application tokens remain unchanged. The
  distinctive behaviour is the protected calendar viewport rather than a new decorative treatment.

### States and edge cases

- Loading and error behaviour remain page-owned and unchanged.
- Empty timelines keep their configured headers, filters, and Columns action visible.
- At the visible-column limit, hidden fields explain that another optional field must be hidden first.
- Invalid or obsolete saved entries are repaired individually; older views receive the current timeline defaults.
- A resize at a narrow width clamps before the calendar becomes unusable.

### Accessibility and responsive behaviour

- Dragging is optional; labelled move controls provide the complete reorder path.
- Column and pane resize separators expose orientation, current/min/max values, and Arrow-key operation.
- The Columns panel closes with Escape or Done and returns focus to its trigger.
- At narrow widths, the context pane stays bounded and its own overflow does not remove the calendar from view.
- Print hides interactive controls, preserves the selected context order, and keeps the calendar dominant.

## 4. Technical plan

- Affected components: shared table-column model/menu, saved-view state decoder, `AgreementsPage`,
  `AgreementsTimeline`, `AssetsPage`, `AssetsTimeline`, their CSS/tests, translations, and API contract documentation.
- API/data changes: no endpoint, response, or database changes. The opaque frontend saved state gains optional validated
  `timelineColumns` entries and advances to state version 4.
- Existing patterns to reuse: v2 table column definitions and repair/move/fit helpers, `TableColumnsMenu`,
  `ColumnResizeHandle`, CSS Modules/tokens, `ResizeObserver`, and the Agreement timeline separator.
- Proposed tasks:
  1. Define approved timeline catalogues and add a maximum-visible option to the shared Columns menu.
  2. Extend saved-view state parsing and capture/application with independent timeline layouts.
  3. Render both timeline context tables from their layout and add shared calendar-first pane constraints.
  4. Add translations, focused model/component/parser tests, builds, lint/format, and visual/print review.

## 5. Acceptance criteria

- [x] Agreement and Asset timeline users can hide, restore, reorder, resize, and reset approved context columns.
- [x] Required identity columns and the Asset selection cell cannot be hidden or moved incorrectly.
- [x] Agreements allow no more than five visible context columns and Assets no more than four.
- [x] Column changes never expand the context pane or reduce the protected calendar allocation.
- [x] Both timelines expose an accessible pointer/keyboard pane resize control with the same constraints.
- [x] Header, filter, body, empty-state span, hover synchronisation, selection, and Gantt lanes remain aligned.
- [x] Hiding a filtered timeline column clears that filter.
- [x] Table and timeline column layouts persist independently; version 1-3 saved views remain readable.
- [x] Print keeps the current period/calendar readable and excludes configuration controls.
- [x] All new visible copy is translated in every supported locale.

## 6. Verification

- Focused tests: layout limits, state migration/repair, both timeline components, both page integrations, and hidden filters.
- Commands: focused `npm test -- --run`, full `npm test`, `npm run build`, `npm run lint`, `npm run format`, and
  `git diff --check`.
- Manual checks: both timelines at wide/narrow desktop widths; pane and column resize; pointer/keyboard reorder; visible
  limit; filters; saved-view restore; Asset selection; hover sync; empty state; dark mode; and landscape print.
- Data/migration checks: apply representative version 1, 2, and 3 saved states and confirm default timeline layouts.

Implementation verification completed with the full frontend test suite, lint, production build, changed-file formatting,
and `git diff --check`. Browser QA covered both live timelines, column-limit states, popup placement, accessible pane
separators, the protected calendar allocation, and browser console warnings/errors.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| Layout relationship | Table and timeline layouts are independent because their primary jobs and width budgets differ. | Approved implementation direction. |
| Calendar allocation | Context pane is capped at 40% at normal desktop widths; column changes never move the divider. | Approved implementation direction. |
| Pane persistence | Store pane width locally per page, not in shared views. | Avoid viewport-specific shared layouts. |
| Initial catalogue | Expose decision-supporting fields only; add further fields after workflow evidence. | KISS/YAGNI decision. |
