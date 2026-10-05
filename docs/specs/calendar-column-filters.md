# Feature specification: Rich calendar-column filters

> **ID:** FE-03  
> **Status:** Complete — implemented, merged, and manually UAT-validated on 2 September 2026
> **Priority:** P1

## 1. Objective

**User and job:** Planners scanning Agreement and Asset tables need to filter date columns by business-friendly
conditions without manually calculating date ranges.

**Problem:** Each visible date column currently provides only a native date value. The legacy grid offered useful
operators and relative periods such as Today, This month, and Next year.

**Outcome:** Every visible calendar column offers a consistent compact filter menu with exact, comparative, and relative
date predicates, and the selection persists in saved views.

## 2. Scope and boundaries

### In scope

- Add the following predicates to visible date columns: **On**, **Not on**, **After**, **Before**, **Today**,
  **Yesterday**, **This month**, **Last month**, **Next month**, **This year**, **Last year**, and **Next year**.
- Require a date value only for On, Not on, After, and Before.
- Apply the shared interaction to Agreement and Asset table date columns.
- Show the active predicate/value in the collapsed column control and provide Clear filter.
- Persist and validate structured date-filter state in Agreement and Asset saved views.
- Keep old saved views readable.

### Out of scope

- Reproducing Infragistics menus or styling.
- Date/time-of-day, rolling “last N days”, fiscal periods, user-defined relative ranges, or an **Is empty** predicate.
- Applying these column predicates to timeline scales or non-date columns.
- Server-side filtering in this increment; the current pages load their authorised result set before column filtering.

### Constraints and known rules

- Date comparisons use calendar dates, not timestamps.
- **On** is equality; **Not on** is inequality; **After** and **Before** are exclusive.
- Rows with a null/invalid date do not match any predicate, including Not on. A later explicit Is empty feature can
  address nulls without surprising users now.
- Relative periods are inclusive date ranges calculated from the user's browser-local current date when the filter is
  applied/rendered. Tests inject a fixed clock.
- Month/year predicates use calendar boundaries: first day inclusive, first day of the following period exclusive.
- Changing locale affects display formatting, not the persisted ISO `YYYY-MM-DD` value or predicate.
- The current date input must remain keyboard accessible. The menu closes with Escape and restores focus to its trigger.

## 3. User experience

### Primary flow

1. The user opens a date column filter.
2. They choose a predicate. Absolute predicates reveal a labelled date input; relative predicates apply immediately.
3. The table filters and the column control shows a concise active label such as **This month** or
   **After 02 Sep 2026**.
4. The user clears the filter from the same menu or applies a saved view containing it.

### Information hierarchy

- The column name and active sort remain in the header.
- The filter row shows a compact active predicate; the full menu contains the operator choices and conditional value.
- Do not duplicate visible date filters in More filters.

### States and edge cases

- Incomplete absolute predicate: do not apply until a valid date is supplied; announce the required value.
- Invalid persisted state: ignore only the invalid filter and continue applying the rest of the view.
- Relative saved filter: remains relative when reopened; it is not converted to the date on which the view was saved.
- Day/month/year rollover: recalculate against the current local date without requiring a new saved view.
- Empty result: keep the active filter visible and offer the existing reset path.

### Accessibility and responsive behaviour

- Use a button with expanded state, a labelled predicate select/list, and labelled native date input.
- Provide Arrow/Tab keyboard navigation consistent with native controls, Escape to close, and visible focus.
- Announce application/clear state without relying on the filter icon colour.
- The popup must stay within the viewport; table horizontal scrolling remains available.
- Print shows filtered rows and hides the interactive filter controls.

## 4. Technical plan

- Affected areas: shared `TableColumnFilter` controls, Agreement/Asset tables and list models, saved-view state/decoders,
  translations, CSS, and focused tests.
- API/data changes: none.
- Saved state: add a typed `dateColumnFilters` map with `{ operator, value? }` entries and increment the page-state version
  while accepting existing version-1 states whose string column filters remain valid.
- Existing patterns to reuse: native date input, current column filter row, saved-view validation, `dayjs` only if it
  materially simplifies boundary calculations already supported by the project.

### Proposed agent tasks

1. Define the date predicate type and pure calendar-boundary/matching functions with fixed-clock tests.
2. Build the shared accessible date-filter popover and integrate it with every Agreement/Asset date column.
3. Extend saved-state serialisation/decoding with backwards compatibility and malformed-state tests.
4. Add table behaviour tests, translations, print checks, build, and visual/keyboard review.

## 5. Acceptance criteria

- [x] Every visible Agreement and Asset date column offers all twelve agreed predicates.
- [x] On/Not on/After/Before require and persist an ISO date; After/Before are exclusive.
- [x] Relative day, month, and year predicates return the correct calendar range at boundary dates, including leap years
      and December/January transitions.
- [x] Null or invalid row dates do not match a date predicate.
- [x] The active predicate/value is understandable without reopening the menu and can be cleared in one interaction.
- [x] A saved relative predicate remains relative when applied on a later day.
- [x] Existing saved views without rich date filters still load with their previous filter/sort state.
- [x] Invalid date-filter state is ignored safely rather than invalidating the whole saved view.
- [x] Keyboard users can open, operate, close, and clear the filter with focus restored predictably.
- [x] Printed tables contain only the filtered rows and no interactive filter menu.

## 6. Verification

- Focused tests: predicate model, clock/month/year boundaries, Agreement/Asset table interactions, saved-view decoder
  migration, malformed values, and print markers.
- Commands: targeted `npm test`; `npm run build`; `npm run lint`; `npm run format`; `git diff --check`.
- Manual checks: each predicate, two simultaneous date filters, popup clipping at rightmost columns, keyboard/Escape,
  saved/reloaded relative view, empty results, narrow window, and print preview.

## 7. Decisions and open questions

| Item            | Decision or question                                                                 | Owner / resolution                       |
| --------------- | ------------------------------------------------------------------------------------ | ---------------------------------------- |
| Predicate set   | Use the twelve options visible in the supplied legacy screenshot.                    | Product note, 2 September 2026.          |
| Comparison zone | Browser-local calendar date.                                                         | Recommended to match visible user dates. |
| Null behaviour  | Null/invalid dates match none, including Not on.                                     | Recommended predictable rule.            |
| Server contract | Keep client-side while lists are loaded client-side; revisit with server pagination. | Recommended KISS boundary.               |
