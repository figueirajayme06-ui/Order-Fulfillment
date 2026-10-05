# Feature specification: Availability divisions and location filters

## 1. Objective

**User and job:** Fleet planners and fulfilment users need to compare suitable stock in other divisions they are authorised to use while reserving an agreement line.

**Problem:** The fulfilment availability window requests only the selected line's division and renders a flat list of warehouse columns, so users cannot reveal stock from another authorised division without leaving the workflow.

**Outcome:** A user can add authorised divisions to the availability window, understand each warehouse's division and facility, filter facility and warehouse columns, and reserve an asset without losing the agreement warehouse context.

## 2. Scope and boundaries

### In scope

- Keep the agreement line's division selected and non-removable.
- Let users add and remove other divisions returned by the existing authorised division lookup.
- Query availability with the selected comma-separated division codes.
- Return and render the existing availability result's division and facility metadata.
- Load the configured warehouses for the selected authorised divisions so local warehouse columns remain visible even when an item has no availability-summary row there.
- Group warehouse columns as Division, Facility, and Warehouse, with the agreement location first.
- Show only the agreement facility initially, and let users add or hide other facilities without refetching.
- Let users hide non-agreement warehouses from the grouped header and add hidden warehouses back without refetching.
- Let users reveal all automatically hidden empty warehouses with an unchecked-by-default checkbox, without refetching.
- Preserve selectable zero-availability cells used to find repair assets.
- In the expanded Asset list, show physical location and date-aligned reservation/commitment context before Reserve.

### Out of scope

- Selecting business regions or adding a new Region-to-Division taxonomy.
- Expanding a user's division permissions or allowing reservations for inaccessible assets.
- Changing NOF availability calculations or reservation business rules beyond consuming the established fulfilment procedure contract.
- Persisting division or location-filter choices across agreements or browser sessions.

### Constraints and known rules

- The API remains the security boundary and intersects requested divisions with the caller's assigned divisions.
- Asset drill-down uses exact item-number and warehouse matching so a cell cannot offer a substring-matched asset.
- Asset schedule context uses one `POST /api/event/events` batch for the displayed Asset IDs and selected line period;
  an event failure does not remove the Assets or disable Reserve.
- The agreement division is always included and appears first.
- Within each visible facility, all warehouse codes follow the same stock and manual visibility filters.
- A reported availability of `0` is actionable repair context and must not be treated as an empty warehouse.
- The existing SQL permission-error compatibility behaviour remains unchanged.
- NOF consumes `dbo.GetFulfilmentAvailabilitySummary` and `dbo.GetFulfilmentGenericsWithSubstitutions`; `dbo.GetCPQAvailabilitySummary` and `dbo.GetGenericsWithSubstitutions` remain CPQ Next-only compatibility contracts.
- Quantity rows subtract the peak quantity of simultaneously active unconfirmed reservations overlapping the inclusive requested period from `StockQuantity - AllocatedQuantity`; disjoint pending periods are not added together and confirmed allocations are not double-counted.
- Serialized rows treat a dated current `OnHire` state as available only after its collection, termination, or valid-to release date; overlapping reservations and Ringfences still block the asset.
- No new external dependency or shared generic multi-select is introduced. `src/OF.Data.Design` remains the sole deployment owner for the shared-database procedure and function definitions.

## 3. User experience

### Primary flow

1. Select a fulfilment line and view availability for its agreement division.
2. Choose an additional division from the compact selector.
3. The availability query refreshes and the selected division appears as a removable labelled chip.
4. Scan the agreement facility and its grouped warehouse headers, with the agreement warehouse highlighted.
5. Add another facility when broader availability is needed, or hide a non-agreement facility or warehouse from its grouped header.
6. Select any populated availability cell, including `0`, and reserve an accessible asset.
7. Remove an additional division to narrow the grid again.

### Information hierarchy

- The selected line's item and description remain the sticky row context.
- Selected divisions and view options remain immediately above the grid.
- Division and facility group labels provide location context without adding fields to each item row.
- Facility filtering stays client-side because the selected divisions' availability rows already contain the hierarchy.
- Individual assets remain progressively disclosed below the selected availability cell, with ID, status, physical
  location, compact commitment context, and Reserve in that reading order.

### States and edge cases

- Loading: retain the established availability spinner while stock results refresh; disable the add control while division choices load.
- Empty: retain the established no-availability message when the query returns no rows.
- Error: retain the availability error and show a concise lookup error if additional divisions or configured warehouses cannot be loaded. A warehouse-lookup failure falls back to the locations present in the availability summary.
- Success/confirmation: the selected-division chips and grouped columns are the immediate result of the action.
- Disabled/no-permission: users with no additional authorised divisions see the agreement division only and no enabled add option.
- Facility filter: only the agreement facility is initially visible. Other facilities are available from **Add facility**, and the agreement facility cannot be hidden.
- Newly available facility: a facility introduced by a configured-warehouse response or an added division starts hidden until the user adds it.
- Warehouse filter: configured local warehouses ending in `0` or `1` remain available to the grid, but the default
  visibility is refined by [Availability stock focus and asset schedule context](availability-stock-focus-and-asset-context.md):
  empty non-agreement warehouses start hidden, while the agreement warehouse remains visible and cannot be hidden. The
  **Include empty warehouses** checkbox reveals these automatic omissions within already-visible facilities.
- Configured-only warehouse: when no availability-summary row exists for an item and warehouse, show the existing em dash rather than synthesising a zero or an actionable cell.
- Filter composition: hiding a facility also hides its warehouses without discarding any manual warehouse choices beneath it.
- Stale requests: only the latest availability request may update the grid.

### Accessibility and responsive behaviour

- Use a native labelled `select` to add a division and native labelled buttons to remove optional divisions.
- Use a native labelled `select` to restore hidden facilities and native labelled buttons to hide non-agreement facilities.
- Use a native labelled `select` to restore hidden warehouses and native labelled buttons to hide non-agreement warehouses.
- Restore focus to the facility selector when a focused facility header is removed.
- Restore focus to the warehouse selector when a focused warehouse header is removed.
- Keep the global visible focus treatment and expose the agreement division and warehouse through text or a visible symbol, not colour alone.
- Preserve keyboard operation for availability cells and reservation actions.
- The existing horizontal scroll remains the desktop and narrow-screen fallback for the expanded grid.
- The availability panel is already excluded from print; no print output changes.

## 4. Technical plan

- Affected page/component: agreement detail fulfilment availability in `AvailabilityPanel`.
- API/data changes: retain the availability-summary array and authorised `GET /api/lookups/warehouses?division=...`
  reference-data endpoint. Reuse the existing batch `POST /api/event/events` contract with `assetIds`; no API or
  database change is required for schedule context.
- Existing patterns/components to reuse: `fetchDivisions`, semantic form controls, CSS Modules, theme tokens, existing spinner and availability cells.
- Proposed tasks, each independently testable:
  1. Map and contract-test the location metadata emitted for NOF by `GetFulfilmentAvailabilitySummary`.
  2. Add selected-division state, authorised lookup loading, and latest-request protection.
  3. Add and contract-test the configured warehouse lookup, intersecting its explicit division selection with the caller's access.
  4. Merge configured warehouse columns with summary-derived columns without creating synthetic availability cells.
  5. Build the Division-to-Facility-to-Warehouse column hierarchy and agreement-first ordering.
  6. Add client-side facility and warehouse pruning with the agreement location protected.
  7. Add localised division/facility/warehouse controls and focused component tests.
  8. Verify the frontend and WebApp builds plus relevant focused test suites.

## 5. Acceptance criteria

- [ ] The agreement line division is selected by default, cannot be removed, and remains first.
- [ ] A user can add and remove only divisions returned by the authorised lookup.
- [ ] Availability requests contain the current selected division codes as a comma-separated value.
- [ ] Results render grouped Division, Facility, and Warehouse headers.
- [ ] The agreement warehouse remains visually and textually identifiable.
- [ ] Only the agreement facility is initially visible; users can add and then hide non-agreement facilities independently without refetching.
- [ ] Facilities introduced by adding a division or loading configured warehouses start in the **Add facility** selector rather than appearing automatically.
- [ ] Facility identity includes its division so equal facility codes in different divisions do not filter each other.
- [ ] The agreement facility cannot be hidden, and changing facility visibility closes an expanded asset list.
- [ ] All configured warehouses for the selected authorised divisions are available to the grid; users can hide and restore non-agreement warehouses independently without refetching.
- [ ] A configured local suffix-`1` warehouse remains available in **Add warehouse** even when the availability summary
      contains no row for that item and warehouse; it starts hidden when it is empty unless it is the agreement warehouse.
- [ ] A configured warehouse with no summary row renders an em dash and is not treated as reported zero or made clickable.
- [ ] Warehouse codes are normalised for identity and suffix classification so padded or differently cased suffix-`1`
      warehouses are classified correctly and remain available to restore.
- [ ] The agreement warehouse cannot be hidden, and changing warehouse visibility closes an expanded asset list.
- [ ] Hiding the final warehouse in a facility or division removes its empty group headers and keeps all column spans aligned.
- [ ] The agreement warehouse header remains highlighted, accessibly named, and marked with a compact inline selection cue without a second visible **Agreement** line.
- [ ] Local warehouse suffixes `0` and `1` remain available within every facility the user has made visible; positive
      warehouses and the agreement warehouse are visible by default, while empty non-agreement warehouses can be restored
      from **Add warehouse**.
- [ ] **Include empty warehouses** reveals all automatically hidden empty warehouses in visible facilities without
      refetching or bypassing the manual location filters.
- [ ] When a warehouse is visible, its reported zero-availability cells remain
      keyboard-operable and selectable.
- [ ] Changing divisions closes any expanded asset list and stale responses cannot replace newer results.
- [ ] Assets offered from a cell match its exact item number, warehouse, and authorised division.
- [ ] Expanded Assets show location and batched commitment context for the selected line period; no-event Assets are
      labelled clearly, and an event failure remains retryable without blocking Reserve.
- [ ] Division lookup, warehouse lookup, availability, empty, and no-additional-area states remain understandable; a failed warehouse lookup does not discard usable summary results.
- [ ] New user-facing and accessible text is present in every supported locale.

## 6. Verification

- Focused tests: availability component selection/grouping/repair flow; default empty-warehouse visibility and
  restoration; batched schedule rendering, failure, retry, and stale-response handling; availability frontend service;
  availability Web API controller contract.
- Build/lint/format commands: `npm run build`, focused `npm test`, targeted .NET tests, and `git diff --check`.
- Manual visual checks: normal desktop width, horizontal overflow with several divisions, agreement location emphasis, keyboard focus, loading/error/empty states, and selectable zero cells.
- Data or migration checks: contract-test the named `GetFulfilmentAvailabilitySummary` result columns for Facility, DivisionCode, and DivisionName, and publish the procedure/helper only from `OF.Data.Design`.

## 7. Decisions and open questions

| Item                | Decision or question                                                                                                                                                                 | Owner / resolution                                                                                                |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------- |
| User-facing term    | Use **Divisions** and **Add division** while displaying the division code and name.                                                                                                  | Matches the data users are selecting and removes ambiguous stock-area terminology.                                |
| Geographic scope    | Add authorised divisions only; do not introduce free-form business-region selection.                                                                                                 | Agreed implementation recommendation.                                                                             |
| Empty warehouses    | Hide empty non-agreement warehouses by default but keep them individually restorable; a restored reported-zero cell remains actionable for repair selection.                         | Superseded by the September 2026 stock-focus requirement.                                                         |
| Refresh interaction | Refresh automatically when a division is added or removed.                                                                                                                           | Keeps the existing line-selection availability behaviour and avoids a second Search action.                       |
| Facility filter     | Start with only the agreement facility visible. Add another facility from the compact selector and hide it from its header.                                                          | Keeps the default grid focused while retaining CPQ Next's client-side filtering and protected agreement facility. |
| Facility identity   | Use normalised Division + Facility rather than facility name alone.                                                                                                                  | Prevents equal facility codes in different divisions from being coupled.                                          |
| Warehouse source    | Union the authorised configured warehouse catalogue with summary-derived warehouses, while keeping cells exclusively summary-derived.                                                | Makes configured local warehouses visible without presenting missing data as zero availability.                   |
| Warehouse filter    | Start with positive-stock warehouses plus the agreement warehouse. Hide other empty warehouses automatically and restore any hidden non-agreement warehouse from a compact selector. | Refined by the September 2026 stock-focus requirement without adding a dependency.                                |
| Warehouse identity  | Use the normalised warehouse code and retain manual visibility choices.                                                                                       | Warehouse code is the existing availability-cell identity and exact asset lookup key.                             |
| Availability SQL    | Consume the NOF-specific procedure/helper and preserve the CPQ-only compatibility contract.                                                                                          | `OF.Data.Design` is the sole deployment owner for both contracts.                                                 |
