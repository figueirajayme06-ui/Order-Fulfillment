# Feature specification: Legacy list parity and timeline focus

## 1. Objective

**User and job:** Fleet planners need the Agreement and Asset lists to expose the operational fields they used in the legacy UI, make existing notes immediately apparent, and keep timeline navigation centred on the present after changing temporal context.

**Problem:** The React lists omit several legacy column choices, note presence is only discoverable after opening a record, timeline rebuilds can retain an obsolete viewport, division 200 inherits an incorrect source-data name, and inactive warehouse reference rows can leak through inconsistent code formatting.

**Outcome:** Legacy business fields are optional configurable columns, note-bearing records have an accessible indicator, timelines return to today after a timescale or applied date-range change, division 200 is labelled USA, and every warehouse on the established inactive list is consistently excluded.

## 2. Scope and boundaries

### In scope

- Add non-technical legacy Agreement and Asset grid fields to the React list API and configurable column catalogues.
- Intentionally omit Lowest Status and Highest Status from the column choices following user review. Keep their API fields, database identifiers, and timeline bar payloads internal.
- Return and display note counts on Agreement and Asset list rows and on the existing Notes trigger.
- Jump to today after Frappe Gantt timescale changes and after the Asset profile's applied date range rebuilds its timeline.
- Override the display name for division `200` to `USA`.
- Apply the repository's established inactive warehouse exclusion list case-insensitively and document its members.

### Out of scope

- Reproducing the legacy Infragistics layout or changing current default-visible columns.
- Adding new note mutation behaviour, changing note permissions, or preloading note bodies in list responses.
- Deleting warehouse data or hiding Agreements/Assets merely because their recorded warehouse is inactive.
- Guessing additional inactive warehouse codes not identified by the repository's maintained exclusion list.

### Constraints and known rules

- Existing saved views remain readable; new columns use stable additive keys and default to hidden.
- Current required identity/status columns and timeline visible-column limits remain unchanged.
- Note status is conveyed by icon, text available to assistive technology, and tooltip—not colour alone.
- New copy is localised and table/print alignment remains intact.

## 3. User experience

### Primary flow

1. Open Columns on Agreements or Assets and enable any legacy business field needed for comparison.
2. Scan the identity cell for the notes indicator and open the record to work with its notes.
3. Change a timeline timescale, or apply a new Asset profile period, and continue from today's position.

### Information hierarchy

- Existing default columns remain the primary scanning surface.
- Added legacy fields are optional and hidden by default.
- Note count is compactly attached to the record identity and the existing Notes action.

### States and edge cases

- Loading/error: existing list and notes states remain unchanged.
- Empty: zero-note records show no list indicator; a failed notes request does not claim there are no notes.
- Success: creating a note immediately updates the Notes trigger count.
- Timeline: if today is outside a finite timeline, the existing Gantt clamping behaviour selects the nearest valid position.

### Accessibility and responsive behaviour

- Notes indicators have a translated accessible label and title.
- Existing keyboard-operable row/profile links and Notes dialog remain unchanged.
- Optional columns participate in existing responsive sizing and print column calculations.

## 4. Technical plan

- Affected areas: Agreement/Asset list response mapping, frontend types/list models/tables/catalogues, shared Gantt wrapper, Asset profile period application, division and warehouse lookups, localisation, tests, and API documentation.
- API/data changes: additive list-response fields only; no schema change.
- Existing patterns/components to reuse: table column catalogues and repair logic, date filters/cells, fulfilment status icon, Frappe Gantt `scroll_current`, and the repository warehouse exclusion constant.
- Proposed tasks:
  1. Extend list response contracts and mappings with legacy business fields and note counts.
  2. Add hidden-by-default column definitions, rendering, filtering, sorting, and saved-view repair coverage.
  3. Add accessible notes indicators to list identity cells and the Notes editor trigger.
  4. Reset timeline focus after timescale and applied-period changes.
  5. Correct division 200 naming and harden inactive warehouse exclusion.
  6. Run focused frontend/.NET tests, build, lint/format checks, and diff validation.

## 5. Acceptance criteria

- [x] Every requested non-technical legacy column is available in the corresponding React table column chooser, except the intentionally omitted Lowest Status and Highest Status options.
- [x] Added columns are hidden by default and round-trip through saved-view repair without invalidating older views.
- [x] Agreement and Asset rows with notes show an accessible count indicator; rows without notes do not.
- [x] The Notes trigger reflects loaded and newly created note counts.
- [x] Changing the Gantt timescale jumps to today after the rebuilt grid is ready.
- [x] Applying a different Asset profile date range jumps the rebuilt timeline to today.
- [x] Division code 200 is returned with display name USA.
- [x] The maintained inactive warehouse codes are excluded regardless of case or surrounding whitespace.
- [x] The exact inactive warehouse list and its repository-maintained basis are documented.

## 6. Verification

- Focused tests: Agreement/Asset controller contracts, lookup/repository exclusions, table columns and notes indicators, saved-view repair, Gantt timescale behaviour, Asset profile period behaviour, and locales.
- Build/lint/format: `npm test`, `npm run build`, `npm run lint`, `npm run format`, targeted .NET tests, and `git diff --check`.
- Manual checks: column chooser/table alignment, notes tooltip/accessibility, timeline timescale/apply-period focus, division option naming, and print layout.

## 7. Decisions

| Item                | Decision                                                                                                                                                                                                                   | Basis                                                                                                                                     |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Inactive warehouses | Remove `EA0`, `EC0`, `EH0`, `EK0`, `EL0`, `EU0`, `EW0`, `EX0`, `FC0`, `FD0`, `FE0`, `FF0`, `FG0`, `FH0`, `FK0`, `FL0`, `FM0`, `FV0`, `FU0`, `GR0`, `JK0`, `HK0`, `YG0`, `GA0`, and `GB0` from warehouse reference results. | These are the only codes explicitly identified as excluded/inactive by `Constants.Warehouses.Excludes`; no additional codes are inferred. |
| Warehouse records   | Preserve historical Agreement/Asset rows that reference inactive warehouses.                                                                                                                                               | Removing reference choices should not erase operational history or hide records.                                                          |
| Division 200        | Force the display name to `USA` at the shared API response boundary used by division, warehouse, admin, and availability results.                                                                                          | Keeps the code stable while correcting all current consumers of the display name.                                                         |
