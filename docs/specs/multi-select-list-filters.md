# Feature specification: Multi-select Agreement and Asset filters

## 1. Objective

**User and job:** Fleet planners and operational users need to narrow Agreement and Asset lists to several valid values in one view, such as two divisions or multiple statuses.

**Problem:** Option-based filters currently accept only one value, forcing users to switch repeatedly between values or work from a broader result set.

**Outcome:** Every option-based filter on the Agreement and Asset list/table/timeline surfaces supports selecting and clearing multiple values, with OR matching within a field and AND matching between fields.

## 2. Scope and boundaries

### In scope

- Page-level Agreement Division, Order type, and Fulfilment status filters.
- Page-level Asset Status and Division filters.
- Agreement table/timeline Division, Warehouse, and Fulfilment status option filters.
- Asset table/timeline Status, Division, and Warehouse option filters.
- A single synchronized Status selection shared by each page's top-row, table-header, and timeline-header controls.
- A single synchronized Division selection shared by the Asset page's top-row, table-header, and timeline-header controls.
- Removal of the redundant page-level Asset warehouse/location text filter.
- Backward-compatible server-side filtering for multiple Agreement order types/statuses and Asset statuses.
- Saved-view and session-state compatibility with existing single-value strings.

### Out of scope

- Text, date, saved-view, page-size, and ringfence-target controls.
- Changing filter option sources or permissions.

### Constraints and known rules

- Multiple values use OR semantics inside one filter; different filters retain AND semantics.
- Existing singular API query parameters remain supported.
- Division selections remain limited to divisions visible to the signed-in user.
- Controls remain keyboard-operable, visibly labelled, and hidden from print with their existing parent regions.
- Selections are encoded as normalized comma-separated strings at page level. Legacy saved/session views that stored Status or Asset Division in a column-filter field are promoted into the corresponding shared page-level filter when loaded.
- Legacy values from the removed page-level Asset warehouse/location filter are ignored; warehouse selections are now stored in the relevant table or timeline column filter.

## 3. User experience

### Primary flow

1. Open an option filter to see a checklist of allowed values.
2. Select or clear any number of values; results refresh using the combined selection.
3. A Status change—or an Asset Division change—made in the top row or a table/timeline column header is immediately reflected in the corresponding controls.
4. Close with the toggle, Escape, or an outside click; reopen to review the retained selection.
5. Use Clear all or Reset filters to remove selections.

### Information hierarchy

- The closed control always shows its field name and either All, the selected value, or a selected-count summary.
- The open checklist reveals all choices and a compact Clear all action.

### States and edge cases

- Loading: current rows remain governed by the pages' existing loading behavior.
- Empty: existing empty states remain visible when the combination has no matches.
- Error: existing load error and retry behavior remains unchanged.
- Success/confirmation: selection state in the closed control confirms the applied values.
- Disabled/no-permission: division options continue to respect current user access.

### Accessibility and responsive behaviour

- Keyboard/focus behaviour: native checkboxes; Enter/Space opens; Escape closes and returns focus; outside click closes.
- Accessible names/status treatment: the toggle names the field and selection summary; each checkbox has its visible option label.
- Mobile fallback: the popover stays within the viewport and its option list scrolls when needed.
- Print impact: filters remain within existing print-hidden regions.

## 4. Technical plan

- Affected routes/pages/components: Agreement and Asset list controls, tables, timelines, list/query models, saved-state parsing, and shared common controls.
- API/data changes: add optional CSV `orderTypes`/`statuses` query parameters while retaining the existing `orderType`/`status` parameters.
- Existing patterns/components to reuse: shared Button/tokens, existing array-valued column-filter state, existing CSV division handling, and existing i18n keys.
- Proposed tasks, each independently testable:
  1. Add a shared accessible option-checklist dropdown and focused component tests.
  2. Adopt it for all option-based Agreement and Asset page/table/timeline filters.
  3. Normalize page-level multi-selections and preserve saved/session state compatibility.
  4. Add plural API parameters and controller coverage for OR filtering and singular compatibility.
  5. Run focused frontend/backend tests, the frontend suite/build/lint/format checks, and visual review.

## 5. Acceptance criteria

- [x] Users can select multiple values in every in-scope option-based filter.
- [x] A record matches when it has any selected value within a field and satisfies all other active fields.
- [x] Current single-value saved views and API clients continue to work.
- [x] Reset and Clear all remove multi-selections.
- [x] The top-row and column-header Status controls remain synchronized on both Agreement and Asset pages.
- [x] Legacy column-header Status selections are retained through the shared filter when saved/session views load.
- [x] Asset WHS and DIV column filters are option-based multi-select controls, and the obsolete top warehouse/location text filter is absent.
- [x] Filter controls are keyboard accessible and show a meaningful closed-state summary.
- [x] Focused tests and production builds pass.

## 6. Verification

- Focused tests: shared filter control, Agreement/Asset controls, table/timeline filters, query builders, saved-view parsing, and API controllers.
- Build/lint/format commands: `npm test`, `npm run build`, `npm run lint`, `npm run format`; focused `dotnet test` for WebApp controllers.
- Manual visual checks: normal desktop width plus narrow fallback; open/closed summaries, checklist scrolling, focus, Escape/outside-click, empty results, table and timeline views.
- Data or migration checks, if applicable: verify legacy singular query parameters and single-value saved state decode unchanged.

## 7. Decisions and open questions

| Item                     | Decision or question                                                                      | Owner / resolution              |
| ------------------------ | ----------------------------------------------------------------------------------------- | ------------------------------- |
| API contract             | Add optional plural CSV parameters and retain singular parameters                         | Approved by user on 2026-09-08  |
| Filter semantics         | OR within a field; AND between fields                                                     | Product-standard interpretation |
| Asset warehouse/location | Use loaded Asset warehouses in WHS column multi-selects; remove the page-level text field | Approved by user on 2026-09-08  |
