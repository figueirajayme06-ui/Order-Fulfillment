# Feature specification: Configurable Agreement and Asset table columns

> **ID:** FE-06  
> **Status:** Complete — implemented, merged, and manually UAT-validated on 2 September 2026.
> **Priority:** P2

## 1. Objective

**User and job:** Planners with different responsibilities need to arrange Agreement and Asset tables around the fields
they compare most often.

**Problem:** The React tables have fixed visible columns and widths. The legacy Infragistics grid allowed people to move,
resize, hide, and restore columns, but reproducing a complete commercial-grid feature set would add disproportionate
complexity.

**Outcome:** Users can show/hide approved columns, reorder them, and adjust widths using a bounded React implementation.
The layout can be stored in existing saved views without replacing the table stack or exposing arbitrary data fields.

## 2. Scope and boundaries

### In scope

- Agreement and Asset **table** modes only.
- A page-specific approved column catalogue based on fields already present in the list API response.
- Show/hide optional columns and restore hidden columns (“add columns”).
- Reorder columns with accessible Move left/Move right controls. Pointer drag is optional, not required for acceptance.
- Resize resizable columns within documented minimum/maximum widths using a pointer handle and keyboard arrows.
- Reset to the product default layout.
- Persist column order, visibility, and widths in personal/shared saved-view state.
- Preserve required identity/action columns and existing saved views.
- Reflect the visible order in table print output.

### Out of scope

- A new third-party/commercial grid or drag-and-drop dependency.
- Arbitrary database fields, calculated-column builders, frozen/pinned user columns, grouping, aggregation, or Excel-style
  formulas.
- Per-cell formatting rules or user-created column labels.
- Applying table columns to Timeline, Ringfence, Admin, Agreement line, Availability, or Asset-profile layouts.
- Synchronising unsaved layout preferences across browsers. Cross-browser persistence comes from explicitly saving a view.

### Constraints and known rules

- **Add columns** means selecting from the approved client catalogue; a new business field remains a separate API/product
  request.
- Agreement status and agreement number are required/non-hideable. Asset selection/action identity and Asset ID are
  required/non-hideable. An implementation spike must confirm the exact fixed cells before coding.
- The existing tables render separate header and body tables with shared `colgroup` geometry. A single layout model must
  drive both so scroll, sort, filter, row, and print alignment cannot diverge.
- Date/filter/sort state remains attached to stable column keys, never positional indexes.
- Hidden columns retain their filter state only if the active-filter summary makes that fact obvious; recommended KISS
  behaviour is to clear a column's filter when the user hides it after confirmation/notice.
- Width values are clamped and validated; invalid/unknown persisted keys fall back individually, not by rejecting the
  whole view.
- Do not render decision-critical fields only because a saved view from another user contains an unknown future column.
- Use `react-i18next`, current table semantics, visible focus, and existing print hooks.

## 3. User experience

### Primary flow

1. The user opens **Columns** above an Agreement or Asset table.
2. They check/uncheck optional fields, move a selected field left/right, or reset the layout.
3. They resize a visible header from its trailing edge with pointer drag or focused-handle Arrow keys.
4. The table updates without refetching data. The user can save the arrangement in a new/existing saved view.
5. Applying a saved view restores filters, sorting, column order, visibility, and widths together.

### Information hierarchy

- Keep search, primary filters, and Saved views in the established toolbar order; Columns is one secondary control.
- The Columns panel lists visible/hidden status, order controls, and Reset. Do not expose raw pixel values in the normal UI.
- Resize feedback may show a compact width while adjusting, then disappear.

### States and edge cases

- Default: current production column order/widths remain the default unless product review changes them explicitly.
- Invalid saved layout: apply valid entries and repair missing/unknown/duplicate entries from the current catalogue.
- Newly released column: starts at its product-default visibility and deterministic position for old saved views.
- Required column: visible and immovable past required structural cells; hide is unavailable with a reason.
- Hidden active filter: clear with a concise notice (recommended) so the table cannot appear mysteriously empty.
- Narrow viewport: horizontal scroll remains available; minimum widths prevent unusable content.

### Accessibility and responsive behaviour

- Columns opens from a named button and closes with Escape, restoring focus.
- Native checkboxes control visibility; named Move left/right buttons provide complete reorder functionality without drag.
- Resize handles use `role="separator"`, report current/min/max values, and support Left/Right arrows plus larger Shift steps.
- Pointer-only drag is never the only path. Focus remains visible and row navigation/click targets remain intact.
- Print uses visible columns in visible order, drops interactive resize/filter controls, and remains readable in landscape.

## 4. Technical plan

- Affected areas: Agreement/Asset table components and CSS, page state, shared table-column controls, saved-view types and
  decoders, print styles, translations, and tests.
- API/data changes: no list response change. Saved-view state gains a validated `columns` layout and a new page-state
  version; the persisted envelope remains backward compatible.
- Existing patterns to reuse: stable sort-field keys, saved-view decoders, CSS variables/`colgroup`, current table print
  hooks, and Agreement timeline's accessible separator behaviour where applicable.

### Mandatory feasibility spike

Before full implementation, an agent must prototype three representative columns in one table and prove:

1. one state model controls header/body/print order and width without alignment drift;
2. pointer and keyboard resize work with horizontal scrolling and pinned header; and
3. sorting/filtering/row click remain reliable after reorder.

If the spike succeeds, implement the full bounded scope. If free pixel resizing causes persistent alignment or
accessibility defects, stop and present the documented fallback: show/hide + Move left/right + three width presets
(Narrow/Standard/Wide). Do not add a grid dependency without explicit approval.

### Proposed agent tasks

1. Run the spike and record its result in this decision table.
2. Define Agreement/Asset column catalogues and a pure layout repair/migration model with tests.
3. Drive header, filters, rows, `colgroup`, empty-state colspan, and print from that catalogue/layout.
4. Add Columns panel, accessible reorder/reset, and accepted resize interaction.
5. Extend saved views, translations, focused tests, builds, and visual/print/keyboard review.

## 5. Acceptance criteria

- [x] The feasibility spike is documented before the implementation path is selected.
- [x] Users can hide and restore every optional approved Agreement/Asset column without a data refetch.
- [x] Users can move optional columns left/right with keyboard-operable controls; headers, filters, and row cells remain
      aligned.
- [x] Users can resize allowed columns by pointer and keyboard, or the approved preset-width fallback is implemented.
- [x] Required identity/action columns cannot be hidden or moved into an invalid structural position.
- [x] Reset restores the current product-default order, visibility, and widths.
- [x] Saving/applying a view round-trips order, visibility, widths, filters, sorting, and view mode.
- [x] Version-1 saved views load with the product-default column layout and retain their existing state.
- [x] Unknown, duplicated, missing, or out-of-range persisted column entries are repaired safely.
- [x] Hiding a filtered column cannot leave an invisible unexplained filter.
- [x] Print follows the visible column order and excludes configuration controls.
- [x] No grid or drag-and-drop dependency is added.

## 6. Verification

- Focused tests: layout repair/migration; show/hide/reorder/reset; width clamping; header/body/filter alignment model;
  required columns; hidden-filter behaviour; saved-view round trip; unknown future column; print order.
- Commands: targeted `npm test`; `npm run build`; `npm run lint`; `npm run format`; `git diff --check`.
- Manual checks: both tables with short/long values, resize at min/max, reorder with active sort/filter, multiple pages,
  horizontal scroll, keyboard-only operation, narrow window, shared view, and landscape print.

## 7. Decisions and open questions

| Item                | Decision or question                                                                     | Owner / resolution                              |
| ------------------- | ---------------------------------------------------------------------------------------- | ----------------------------------------------- |
| Grid replacement    | Do not replace the current tables or add a dependency for phase one.                     | KISS requirement.                               |
| “Add columns”       | Restore approved fields already present in the list response.                            | Recommended scope definition.                   |
| Reorder interaction | Move left/right is required; direct header drag is optional.                             | Recommended accessible KISS decision.           |
| Resize fallback     | Width presets are acceptable only if the spike demonstrates free resizing is unreliable. | Product owner to approve after spike if needed. |
| Hidden filters      | Clear the filter with notice when its column is hidden.                                  | Accepted by product owner on 2 September 2026.  |
| Spike result        | Passed on 2 September 2026; use bounded free-pixel resizing.                             | See feasibility evidence below.                 |

### Feasibility spike result

The Assets table prototype used Status, Asset ID, and Description as representative compact-status, linked-identity,
and wide text/filter columns. One repaired layout supplied the visible order and widths to the header and body
`colgroup` elements, header cells, filter cells, row cells, empty-state span, and printed DOM order.
The spike confirmed Agreement Status/Agreement number and Asset Status/Asset ID as the fixed required catalogue cells;
the Asset selection checkbox remains a fixed structural cell outside the saved column layout.

Focused component tests proved that:

- both `colgroup` elements remained identical after moving Description before Item number;
- Arrow keys and pointer movement updated the same clamped width used by both tables while the existing horizontally
  scrollable surface and separate pinned header wrapper remained in place; and
- Description sorting/filtering and the Asset profile link still invoked their stable field/action callbacks after the
  reorder.

The spike passed without a dependency or persistent alignment/accessibility defect. The accepted implementation path is
show/hide, Move left/Move right, and clamped pointer/keyboard pixel resizing; the three-width preset fallback is not
required.

The default Agreement widths total 1324 pixels, matching the previous compact table geometry. Narrower viewports keep
intentional horizontal scrolling rather than silently shrinking below catalogue minimums. For print, each visible pixel
width is converted to the same percentage custom property in both header and body `colgroup` elements; print CSS uses
those shared fractions with a fixed 100% table so the columns remain aligned without clipping at screen widths.
