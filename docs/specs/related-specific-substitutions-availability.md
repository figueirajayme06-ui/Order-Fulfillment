# Feature specification: related specific substitutions in availability

## 1. Objective

**User and job:** A fulfilment planner needs to identify stock that is a specifically approved substitute while choosing stock for an agreement line.

**Problem:** The current React availability inspector only includes generic substitutions. It neither requests the line item required to identify a related specific substitution nor marks one when returned.

**Outcome:** The availability inspector includes related specific substitutions under the same matching rules as master and marks each with the familiar related-substitute icon and accessible label.

## 2. Scope and boundaries

### In scope

- Apply master's line-based related-specific matching rule to `GetFulfilmentAvailabilitySummary`: derive the primary generic from the quoted item, include exact-item and primary-generic relationships, and include the line's stored generic only when an active non-depot reservation exists.
- Add the selected line ID, selected line item, and substitution reason to the availability API contract.
- Render the compact related-substitute indicator in the React availability grid.

### Out of scope

- Changes to generic substitution rules, conversion ratios, reservation workflows, or the legacy UI.

### Constraints and known rules

- Related substitutions must still satisfy an active attribute filter.
- A related item already represented by a generic substitution must not be duplicated or relabelled.
- Division access remains enforced server-side; the availability inspector remains excluded from print.

## 3. User experience

### Primary flow

1. A planner opens availability for an agreement line.
2. The inspector requests availability with the line ID plus its generic and item numbers.
3. Related specific stock appears after ordinary and generic-substitute stock with the legacy ↔ marker and a translated “Related substitute” label.

### Information hierarchy

- Keep item number and availability visible in the grid.
- Use the existing compact marker and tooltip rather than adding a new column or control.

### States and edge cases

- Loading, empty, error, reservation, responsive, and print behaviours are unchanged.
- Callers without a line ID retain a backward-compatible item/generic fallback, but agreement detail always supplies the line ID.

### Accessibility and responsive behaviour

- The marker has a translated accessible name; it does not rely on colour alone.
- No keyboard interaction or responsive layout changes are introduced.
- The availability inspector remains excluded from print.

## 4. Technical plan

- **Affected routes/pages/components:** `GET /api/availability/summary`, `AvailabilityPanel`, and the agreement detail availability inspector.
- **API/data changes:** Add optional `lineId` and `itemNumber` query inputs and nullable `substitutionReason` availability output; extend the NOF availability procedure and repository mapping.
- **Existing patterns/components to reuse:** Current availability service, grid model, translated strings, CSS Modules, and master’s `RELATED` reason.
- **Proposed tasks:**
  1. Extend the availability procedure and API mapping with the related-specific selection and reason.
  2. Pass the line item from agreement detail and render/test the marker in the grid.

## 5. Acceptance criteria

- [ ] The primary generic is derived from the quoted item exactly as in `FulfilNonSerialized`; related children configured for that generic or exact item are returned.
- [ ] The line's stored generic contributes related children only while the line has an active non-depot reservation.
- [ ] Related rows respect selected attributes and are not duplicated by generic substitutions.
- [ ] The API exposes `RELATED` and the grid displays the translated legacy-style marker.

## 6. Verification

- **Focused tests:** Availability controller, related-substitution database, availability grid, availability panel, and availability service tests.
- **Build/lint/format commands:** Relevant `dotnet test`, frontend focused tests, `npm run build`, lint, and format checks.
- **Manual visual checks:** Open fulfilment availability for a line with a related child and confirm the marker, tooltip, availability cell, and reservation action.
- **Data or migration checks:** Deploy the `OF.Data.Design` DACPAC before the WebApp because it owns `GetFulfilmentAvailabilitySummary`.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| Matching scope | Mirror `FulfilNonSerialized`: derive the primary generic from `Lines.ItemNumber`; match its parent items and the exact item; add `Lines.GenericItemNumber` only for an active non-depot reservation. | Confirmed against the master stored procedure. |
