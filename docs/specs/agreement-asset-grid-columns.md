# Feature specification: agreement and asset grid columns

## 1. Objective

**User and job:** Fleet planners and sales users need to compare the legacy grid context without leaving the Agreements or Assets lists.

**Problem:** Several legacy columns are unavailable in the React lists, even though their data is already stored.

**Outcome:** Users can add the confirmed legacy-equivalent fields through each table's column selector.

## 2. Scope and boundaries

### In scope

- Add the confirmed fields as default-hidden, sortable and filterable table columns.
- Add the required additive list-response fields and frontend types.
- Define Assets `Size` as `USSizeRating`.

### Out of scope

- Changing the timeline column sets, database schema, or default visible columns.

### Constraints and known rules

- Retain server-side division authorization.
- Keep API additions backward-compatible and localise visible labels.
- Preserve existing saved views by repairing their column layouts with the new optional columns.

## 3. User experience

### Primary flow

1. A user opens the Agreements or Assets table.
2. They choose Columns and enable an additional field.
3. They sort or filter that visible field as needed.

### Information hierarchy

- Existing default columns remain unchanged.
- Added fields are available on demand through Columns.

### Accessibility and responsive behaviour

- Reuse the established table header, filter, resizing, keyboard, and print behaviours.

## 4. Technical plan

- Affected routes/pages/components: AgreementsPage/AgreementsTable and AssetsPage/AssetsTable.
- API/data changes: additive response fields on `GET /api/agreements` and `GET /api/assets`; no migration.
- Existing patterns/components to reuse: column catalogues, DataGrid headers, date filters, saved-view layout repair.
- Proposed tasks:
  1. Expand the list API response projections and frontend list types.
  2. Add default-hidden catalogue entries, rendering, filtering, sorting, saved-view support, and locale labels.
  3. Add focused API/frontend tests and run build verification.

## 5. Acceptance criteria

- [ ] Every confirmed legacy field is available through the corresponding table's Columns control and is hidden by default.
- [ ] Asset Size displays the value sourced from `USSizeRating`.
- [ ] API additions do not change authorization, existing fields, or default column layouts.
- [ ] Added fields can be sorted and filtered with the established controls.

## 6. Verification

- Focused controller, list-model, table, and saved-view tests.
- Frontend build and applicable backend tests.
- Manual table column-selector, filter, horizontal-scroll, and print check.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| Asset Size source | Use `Asset.UsSizeRating`; it is the legacy `VwAssetItem.Size` source. | Confirmed by requester |
