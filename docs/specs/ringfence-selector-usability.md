# Feature specification: Ringfence warehouse and target-picker usability

> **ID:** FE-04  
> **Status:** Complete — implemented, merged, and manually UAT-validated on 2 September 2026.
> **Priority:** P1  
> **Builds on:** [Ringfence management workspace](ringfence-management-workspace.md)

## 1. Objective

**User and job:** Planners creating a Ringfence need to choose a valid local warehouse quickly, and users adding Assets
need to find the target Ringfence by name without scanning a long select list.

**Problem:** The create form lists every configured warehouse suffix even though new Ringfences should use `0`
warehouses. The Asset-to-Ringfence target is a long native select with no name search.

**Outcome:** New Ringfences offer only valid suffix-`0` warehouses, existing legacy values remain safely editable, and
every Asset-to-Ringfence target picker can be narrowed by Ringfence name.

## 2. Scope and boundaries

### In scope

- In create mode, list only warehouses whose normalised code ends in `0` and belongs to the selected authorised
  division scope.
- Enforce the same rule in the Ringfence create API so a stale or direct client cannot submit another suffix.
- Preserve an existing non-`0` or blank warehouse during metadata-only edits; a replacement must be a valid suffix-`0`
  warehouse.
- Add case-insensitive, trimmed Ringfence-title search to the target picker on the Assets workflow.
- Keep the selected target understandable when the search term changes and support contextual return-to-Ringfence flow.
- Retain and test the existing Ringfence register search, which already includes title.

### Out of scope

- Migrating or rewriting existing Ringfence warehouse values.
- Searching Assets inside the Ringfence inspector; it already has a separate Asset search.
- Server-side Ringfence search or pagination. The accessible Ringfence list is already loaded for the picker.
- Fuzzy matching, recent targets, favourites, or a new generic combobox dependency.
- Changing division-visible Ringfence collaboration permissions.

### Constraints and known rules

- Normalise a warehouse code with `trim().toUpperCase()` before suffix classification.
- The rule is exact final-character suffix `0`; a name containing zero elsewhere does not qualify.
- Create still requires the warehouse to be in the selected divisions returned by the authorised lookup.
- Edit compatibility follows the existing unavailable-legacy-value pattern: unchanged values survive, but choosing a new
  value uses the current valid catalogue.
- The picker searches Ringfence title only. Other Ringfence register filters continue to search broader metadata.
- A contextual target locked by the Ringfence-to-Assets handoff remains selected and does not need a search control.

## 3. User experience

### Primary flow

1. In **Create Ringfence**, the user selects divisions and sees only their configured suffix-`0` warehouses.
2. In Assets, the user types part of a Ringfence name above the target select.
3. The options narrow immediately; selecting a result establishes the target and enables Add when Assets are selected.
4. Clearing the search restores all accessible Ringfences without clearing the selected target.

### Information hierarchy

- Create form option label retains division, facility, warehouse code, and warehouse name.
- Asset action bar keeps **Add selected Assets to**, then name search, target result, and Add action in scan order.
- The selected target and selected Asset count remain visible in the selection summary.

### States and edge cases

- No suffix-`0` warehouse: show an actionable empty message and prevent create; do not fall back to an invalid warehouse.
- Existing legacy value: show it as the current retained value in edit mode with a compatibility label; valid replacements
  contain only suffix `0`.
- No matching Ringfence: show **No Ringfences match this name** without losing the current selection.
- Deleted/inaccessible target after refresh: clear it and explain that it is no longer available.
- Locked contextual target: show the target and return link; hide/disable unnecessary search.

### Accessibility and responsive behaviour

- Use a labelled `type="search"` input associated with the target select/results.
- Preserve native select keyboard behaviour; announce result count/no-match changes politely.
- Clearing the search has an accessible name and predictable focus.
- Controls wrap on narrow layouts without separating the target from Add.
- Ringfence actions remain hidden in print.

## 4. Technical plan

- Affected areas: `RingfencePage`, `AssetRingfenceActions`, Ringfence controller validation, translations/CSS, and focused
  tests.
- API/data changes: no response-shape change; create/update validation distinguishes retained legacy warehouse from a
  new valid suffix-`0` selection.
- Existing patterns to reuse: authorised warehouse lookup, Ringfence list already loaded in Assets, current register
  search matching, field-level `application/problem+json` errors, and contextual target lock.

### Proposed agent tasks

1. Add a pure normalised suffix-`0` predicate and use it for create/edit option construction with legacy-value tests.
2. Enforce the create/replacement rule in Ringfence API validation without invalidating unchanged legacy records.
3. Add the title search and no-result/selection-preservation behaviour to the Asset target picker.
4. Add localisation, focused tests, build, and visual/keyboard review.

## 5. Acceptance criteria

- [x] Create Ringfence lists only authorised configured warehouses whose trimmed code ends in `0`.
- [x] The server rejects a new Ringfence whose warehouse does not end in `0` or is outside the caller's selected division
      access.
- [x] Editing unrelated fields preserves an existing blank/non-`0` warehouse; changing that value requires a valid
      suffix-`0` warehouse.
- [x] The Assets Ringfence target list can be filtered by a case-insensitive partial title.
- [x] Clearing/changing search does not silently change a selected target; no-match state is explicit.
- [x] Contextual target locking and return navigation still work.
- [x] Ringfence register search continues matching titles.
- [x] No dependency is added for the search interaction.

## 6. Verification

- Focused tests: warehouse normalisation/suffix; create rejection; legacy edit preservation; picker match/no-match/clear;
  target selection and contextual lock; register title search regression.
- Commands: targeted Ringfence controller tests; `npm test -- RingfencePage AssetRingfenceActions`; `npm run build`;
  `git diff --check`.
- Manual checks: multi-division warehouse labels, no valid warehouse, legacy edit, long Ringfence list, keyboard search and
  selection, wrapped/narrow action bar.

## 7. Decisions and open questions

| Item            | Decision or question                                                            | Owner / resolution                              |
| --------------- | ------------------------------------------------------------------------------- | ----------------------------------------------- |
| “0 warehouse”   | Interpret as a normalised warehouse code whose final character is `0`.          | Recommended from current warehouse conventions. |
| Search location | Add to the Assets target picker; the Ringfence register already searches title. | Code inspection, 2 September 2026.              |
| Enforcement     | Validate on the API as well as filtering the dropdown.                          | Recommended business-rule boundary.             |
| Legacy records  | Preserve unchanged non-`0`/blank values; only replacements follow the new rule. | Recommended compatibility decision.             |
