# Feature specification: Availability stock focus and asset schedule context

> **ID:** FE-02  
> **Status:** Complete — implemented, merged, and manually UAT-validated on 2 September 2026
> **Priority:** P1  
> **Builds on:** [Availability divisions and location filters](availability-stock-areas.md)

## 1. Objective

**User and job:** Fulfilment users need to focus on warehouses that can supply the selected item and understand an
individual Asset's physical location and existing commitments before reserving it.

**Problem:** Availability initially shows warehouse columns with no positive stock, making the matrix unnecessarily
wide. Expanding a cell shows only Asset ID and status, so users cannot see location or reservation context without
leaving the selection flow.

**Outcome:** Non-agreement warehouses with no positive availability start hidden but remain individually restorable or
collectively revealable through **Include empty warehouses**. The expanded Asset list adds location and a compact,
date-aligned commitment view before the Reserve action.

## 2. Scope and boundaries

### In scope

- Determine empty warehouses from the current availability result and hide them by default.
- Keep the agreement warehouse visible even when it is empty or has no summary row.
- Keep every hidden warehouse in **Add warehouse** so the user can reveal it individually.
- Add an **Include empty warehouses** checkbox so the user can reveal or hide all automatically hidden empty warehouses
  without refetching availability.
- Preserve zero-availability cells as selectable after their warehouse is restored, because NOF may expose repair Assets
  behind a reported zero.
- Add Asset location plus existing reservation/commitment events to the expanded availability selection panel.
- Fetch schedule events once per expanded cell for the returned Asset IDs and the selected line period.
- Provide clear partial-failure behaviour when Assets load but schedules do not.

### Out of scope

- Removing zero rows/cells from the underlying availability response.
- Changing availability calculations, warehouse suffix rules, reservation validation, or division access.
- Copying the CPQ component or CSS directly across repositories.
- A full interactive Gantt editor, schedule mutation, or Asset-profile replacement inside Availability.
- Fetching one Asset profile or one schedule request per Asset.

### Constraints and known rules

- A warehouse is **empty** when every selectable availability row reports `available <= 0` or has no summary value for
  that warehouse. Generic-only/quantity-only rows that cannot select an Asset do not keep an Asset warehouse visible.
- The agreement warehouse is protected and always visible. This follows the CPQ reference rule in
  `CPQ.Frontend/src/components/product/AvailabilityTable.tsx` while retaining NOF's individual Add warehouse control.
- A manually restored empty warehouse stays visible for the current line/criteria. Changing the selected fulfilment line
  resets visibility to the new result's defaults.
- **Include empty warehouses** affects automatic empty-warehouse pruning only. It does not reveal a manually hidden
  warehouse or facility, or persist when the selected fulfilment line changes.
- Facility visibility composes with warehouse visibility: a hidden facility hides its warehouses without forgetting
  individually restored warehouse choices.
- Missing configured-summary data remains an em dash, not a synthetic zero.
- Asset lookup remains capped and exact by item, warehouse, and authorised division. Schedule lookup must be batched for
  those Asset IDs.
- The selected line's valid-from/valid-to dates define the schedule strip. Fall back to header on/off-hire dates through
  the current props. If no usable period exists, show textual commitment dates without inventing a timeline range.
- The schedule is supporting context; Reserve stays visually and keyboard-accessibly distinct.

## 3. User experience

### Primary flow

1. The user selects a fulfilment line and Availability loads.
2. Warehouses with positive availability appear, together with the agreement warehouse. Empty warehouses are available
   from **Add warehouse** but do not widen the grid by default.
3. The user can select **Include empty warehouses** to inspect all empty warehouses in the visible facilities, or restore
   one empty warehouse individually when repair/exception stock needs inspection.
4. Expanding a cell lists matching Assets with Asset ID, status, location, commitments across the requested period, and
   Reserve.
5. The user reviews the location and any reservation bar/label, then reserves the appropriate Asset.

### Information hierarchy

- Availability matrix: current item context, positive-stock locations, and protected agreement location.
- Include empty warehouses: a compact, unchecked-by-default checkbox in View options.
- Add warehouse: all currently hidden configured/summary warehouses, with division, facility, code, and name.
- Expanded row: Asset ID, status, warehouse location, compact schedule, then Reserve.
- Schedule bars use concise labels; accessible text provides event label and dates. Do not duplicate every event field in
  the scanning row.

### States and edge cases

- Loading: retain matrix loading; show a bounded skeleton/status while Asset and schedule context loads.
- Empty availability: retain the current empty result message and selectors.
- No positive warehouse: show the protected agreement warehouse and explain that other empty warehouses can be added;
  hide this hint while **Include empty warehouses** is selected.
- No individual Assets: retain the explicit no-Assets result for an actionable cell.
- No commitments: show **No reservations in this period** and an empty schedule strip, not an “Available” event bar.
- Schedule error: keep Asset rows and Reserve actions available; show one retryable schedule-context warning.
- Stale requests: changing cell, line, division, facility, or warehouse visibility prevents older Asset/event responses
  from replacing the new state.

### Accessibility and responsive behaviour

- Include empty warehouses is a labelled native checkbox, and Add warehouse remains a labelled native select or equally
  complete keyboard-operable picker.
- Timeline bars have text alternatives with event label, start, and end; colour is not the only status cue.
- Asset rows use real table semantics where practical and retain a logical ID → status → location → schedule → action
  focus/read order.
- Horizontal scrolling remains the narrow-width fallback; the expanded panel is height-bounded for 20 Assets.
- Availability remains excluded from print.

## 4. Technical plan

- Affected areas: `AvailabilityPanel`, `availabilityGrid` model/tests, Availability CSS/translations, Asset event service,
  and the existing availability specification.
- API/data changes: none expected. Reuse exact `GET /api/assets` and batch `POST /api/event/events` for read-only schedule
  data. Add an API contract only if the existing event payload cannot identify reservation labels and periods reliably.
- Existing patterns to reuse: NOF facility/warehouse visibility overrides, Asset timeline event service, theme tokens,
  latest-request guards, and the CPQ empty-warehouse predicate as a behavioural reference.

### Proposed agent tasks

1. Add a pure, tested `isWarehouseEmpty`/default-visibility calculation that operates on selectable NOF rows and protects
   the agreement warehouse.
2. Reconcile automatic defaults with manual Add/hide warehouse overrides and stale-request/reset behaviour.
3. Batch-load Asset events after an availability cell returns Assets; map them to a small schedule view model clipped to
   the requested period.
4. Render and test location, empty commitment, one/multiple commitment, partial-error, and Reserve states.
5. Update the existing availability decision record and run focused builds plus desktop/keyboard review.

## 5. Acceptance criteria

- [x] A non-agreement warehouse with only zero or missing availability across selectable rows is hidden on initial load.
- [x] Any warehouse with positive availability for at least one selectable row is initially visible within a visible
      facility.
- [x] The agreement warehouse remains visible even when empty or missing from the availability summary.
- [x] Every automatically hidden warehouse remains available in Add warehouse and can be restored individually without a
      refetch.
- [x] **Include empty warehouses** reveals automatically hidden empty warehouses in visible facilities without
      refetching; clearing it restores the stock-focused default while retaining individual manual choices.
- [x] **Include empty warehouses** does not reveal manually hidden facilities/warehouses,
      and it resets to off when the selected fulfilment line changes.
- [x] Restoring an empty warehouse exposes its genuine zero cells; reported zero cells remain keyboard-operable and can
      reveal repair Assets.
- [x] A manually restored warehouse is not immediately auto-hidden by the same result, and changing fulfilment line resets
      the defaults.
- [x] Expanding an Asset cell displays Asset ID, status, warehouse location (with sensible facility/warehouse fallback),
      commitment label/date context, and Reserve.
- [x] Schedule events are loaded in one batch for the displayed Asset IDs and are constrained to the selected line period.
- [x] An Asset with no event renders a labelled empty schedule rather than a placeholder Available bar.
- [x] A schedule lookup failure does not discard Assets or prevent an otherwise valid reservation.
- [x] Facility/division grouping, configured-only em dashes, manual warehouse choices, access checks, and latest-request
      protection continue to work.

## 6. Verification

- Focused tests: empty predicate; positive/missing/zero cells; agreement protection; Add warehouse restoration; facility
  composition; line reset; Asset-event batching; schedule rendering/error; stale responses.
- Commands: focused `npm test -- AvailabilityPanel availabilityGrid`; `npm run build`; relevant WebApp event tests if the
  contract changes; `git diff --check`.
- Manual checks: wide and narrow matrices, all-empty result, restored zero warehouse, multiple Assets/events, no-event and
  event-error states, keyboard path, and Reserve action clarity.
- Reference comparison: validate behaviour, not styling, against CPQ's `AvailabilityTable` empty-warehouse tests.

## 7. Decisions and open questions

| Item                  | Decision or question                                                                                     | Owner / resolution                                 |
| --------------------- | -------------------------------------------------------------------------------------------------------- | -------------------------------------------------- |
| Empty definition      | All selectable rows are zero or missing; any positive row keeps the warehouse visible.                   | Recommended from the CPQ implementation.           |
| Agreement warehouse   | Always visible, even when empty.                                                                         | Recommended to preserve fulfilment context.        |
| Zero cells            | Hidden only through the default warehouse pruning; still actionable when restored.                       | Required to retain NOF repair selection.           |
| Schedule presentation | Use a compact read-only strip with accessible event text, not a full Gantt dependency.                   | Recommended KISS decision.                         |
| Event contract        | Reuse the batch Asset event endpoint unless investigation proves it cannot identify reservation context. | Agent must confirm before proposing an API change. |
