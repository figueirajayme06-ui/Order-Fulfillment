# Feature specification: Configurable table columns v2

## 1. Objective

**User and job:** Operations users reviewing Agreements and Assets need to scan the most relevant data quickly and arrange each table for their workflow.

**Problem:** Column widths are currently fixed, so wide screens waste space while narrower screens can expose multiple horizontal scroll paths. Reordering relies on repeated left/right actions that become slow for long moves.

**Outcome:** Visible columns fit the available table width predictably, overflow through one horizontal scrollbar only when their minimum widths cannot fit, and can be reordered with a simple drag interaction backed by complete keyboard controls.

## 2. Scope and boundaries

### In scope

- Apply deterministic, weighted column fitting to the Agreements and Assets tables.
- Preserve user-selected widths as preferred widths while deriving rendered widths from the available space.
- Keep one horizontal scroll path when the visible columns cannot fit at their minimum widths.
- Simplify the column panel into visible and available sections with drag reordering, move up/down controls, a completion action, and movement announcements.
- Place the Columns action beside the existing table filter actions and reduce inactive sort-icon noise.
- Cover layout calculation, reorder behavior, persistence compatibility, keyboard behavior, and the two table integrations with automated tests.

### Out of scope

- Sticky or frozen columns.
- Inspecting cell content to calculate widths.
- New saved-view dirty-state indicators or a new saved-view workflow.
- Mobile card layouts, virtualisation, server-side table preferences, or changes to the legacy UI.

### Constraints and known rules

- Existing saved-view state version 3 and its `columns` payload remain unchanged. Stored widths are treated as preferred widths.
- Required and locked columns remain visible and cannot be reordered.
- Column keys remain stable and unknown or obsolete saved settings continue to be repaired safely.
- No new runtime dependency is introduced.
- Controls and announcements are localised in all supported languages.
- Printed tables continue to use proportional widths derived from the active preferred layout.
- Resizing and viewport changes must not cause row-content measurement or persist transient derived widths.

## 3. User experience

### Primary flow

1. The table distributes spare width toward high-value text columns and shrinks flexible columns toward their minimum widths as space decreases.
2. When minimum widths no longer fit, the whole table uses its existing outer horizontal scrollbar; header and body do not expose separate horizontal scrollbars.
3. The user opens Columns from the table action area.
4. The user drags a visible, unlocked column to a new position, or uses labelled move up/down buttons from the keyboard.
5. The user shows an available column, hides an optional visible column, resets the layout, or selects Done to return to the table.

### Information hierarchy

- Required columns remain at the top and are labelled as always shown.
- Visible, reorderable columns are the primary panel content.
- Hidden columns are grouped under Available columns.
- The trigger communicates how many columns are visible.

### States and edge cases

- Loading: existing page and table loading behavior is unchanged.
- Empty: fitting and column configuration remain available on empty result sets.
- Error: existing page error handling is unchanged.
- Success/confirmation: a live region announces completed column moves; closing the panel returns focus to its trigger.
- Disabled/no-permission: no new permissions are introduced.

### Accessibility and responsive behaviour

- Keyboard/focus behaviour: the panel supports Escape, outside click, explicit Done, checkbox visibility changes, and move up/down controls. Dragging is an enhancement rather than the only reorder path.
- Accessible names/status treatment: resize and reorder controls include column-specific names; movement is announced politely.
- Mobile fallback, if affected: the desktop-first table retains one horizontal scrollbar below the minimum layout width.
- Print impact, if affected: configuration controls remain hidden and printed column proportions remain deterministic.

## 4. Technical plan

- Affected routes/pages/components: `/agreements`, `/assets`, their table and filter-control components, shared TableColumns components, column-layout helpers, CSS modules, tests, and translations.
- API/data changes: none. No controller, database, saved-view schema, or state-version change.
- Existing patterns/components to reuse: current column catalogues and repair helpers, CSS modules and design tokens, React Icons, saved-view state flow, ResizeObserver, and existing accessible menu behavior.
- Proposed tasks, each independently testable:
  1. Add a pure width-fitting function and catalogue growth weights, with unit coverage for grow, shrink, minimum-overflow, caps, hidden columns, and rounding.
  2. Measure each table viewport and apply derived widths consistently to header and body while retaining preferred widths for persistence and print.
  3. Remove nested horizontal scrolling and keep the outer table container as the sole overflow owner.
  4. Add a pure move-to-position helper and update the Columns panel with drag/drop plus keyboard movement.
  5. Integrate the trigger into each page's table actions, refine sort-indicator visibility, localise new labels, and add focused component tests.

## 5. Acceptance criteria

- [x] On a wide table, visible columns fill usable width up to their configured maximums, prioritising descriptive text columns.
- [x] As the viewport narrows, columns shrink no smaller than their minimums; below that point exactly one horizontal scrollbar is available.
- [x] Agreements and Assets header and body columns stay aligned after resizing, reordering, visibility changes, filtering, and viewport changes.
- [x] Derived responsive widths are not written into saved views; user resize, visibility, order, and reset changes still persist through the existing page-state contract.
- [x] Optional visible columns can be reordered by drag/drop and by keyboard-accessible move controls.
- [x] Required/locked columns cannot be hidden or moved.
- [x] Opening, closing, Escape, outside click, focus return, and movement announcements work as expected.
- [x] Existing print proportions and saved-view compatibility tests continue to pass.
- [x] All new user-facing text is translated in the supported locale files.

## 6. Verification

- Focused tests: `npm test -- --run src/lib/tableColumnLayout.test.ts src/components/assets/AssetsTable.test.tsx src/components/agreements/AgreementsTable.test.tsx`
- Build/lint/format commands: `npm test -- --run`, `npm run build`, `npm run lint`, and `npm run format` from `src/OF.Frontend`.
- Manual visual checks: Agreements and Assets at wide and narrow desktop widths; single-scroll behavior; open/close/focus; pointer and keyboard reorder; resize; hide/restore/reset; saved-view restore; dark mode; and print styling.
- Data or migration checks, if applicable: none; verify a version 3 saved view round-trips without a payload change.

Verification result: 410 frontend tests passed; production build and lint passed. Manual browser checks covered Agreements and Assets in light and dark themes, wide-screen fitting, action placement, hide/restore, panel scrolling, and focus return. Narrow minimum-width behavior is covered by deterministic layout and integration tests.

## 7. Decisions and open questions

| Item           | Decision or question                                                                            | Owner / resolution                                                     |
| -------------- | ----------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| Width model    | Use catalogued growth weights and min/preferred/max bounds; never inspect row content.          | Engineering decision for predictable performance and layout stability. |
| Persistence    | Keep stored widths as preferred widths and derive responsive widths only in the client.         | Preserves the v3 saved-view contract.                                  |
| Reorder input  | Use drag/drop as the fast path and move up/down buttons as the complete keyboard path.          | Meets KISS and accessibility goals without a new dependency.           |
| Sticky columns | Defer until real workflow evidence justifies the added responsive and accessibility complexity. | Out of scope for v2.                                                   |
