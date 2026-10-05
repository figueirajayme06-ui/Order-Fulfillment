# Feature specification: Assets list usability

## 1. Objective

**User and job:** Fleet planners need to scan, filter, and compare Assets without losing context while moving through large result sets or the timeline.

**Problem:** Location matching and broad search omit visible context, failed requests have no recovery action, table geometry changes with page content, optional filters hide their applied values, and timeline navigation can expose irrelevant history.

**Outcome:** The Assets table stays dense and stable, active filters remain understandable, errors are recoverable, and the timeline opens at today with no more than twelve months of history.

## 2. Scope and boundaries

### In scope

- Location-aware warehouse and column filtering.
- Broad search across the visible text context.
- Compact, fixed Assets columns with a pinned header and a visible delivery date.
- Retry and reset-filter paths.
- Warehouse/location timeline context, today-centred period changes, and a twelve-month history limit.

### Out of scope

- A Service Day table field. No authoritative service-day value exists in the Asset list contract.
- Database schema changes or inference of Service Day from another date.
- Changes to Agreement page behaviour.

### Constraints and known rules

- Preserve exact warehouse matching used by availability lookups.
- Preserve saved Asset views; newly supported sort/filter fields are additive.
- Keep lower-frequency Asset dates in expanded details unless explicitly promoted to the grid.
- Preserve keyboard focus, print markers, and accessible filter names.

## 3. User experience

### Primary flow

1. A planner searches or filters Assets by visible business context, including warehouse location.
2. The table header and filter controls remain visible while rows scroll.
3. The planner can retry a failed request or reset the current filters.
4. In Timeline view, warehouse and location remain visible and changing scale returns the chart to today.

### Information hierarchy

- Visible: status, Asset/item identity, description, warehouse/location, division, customer/agreement, delivery, and DOH.
- Expanded details retain the full date/context set.

### States and edge cases

- Loading: existing skeleton/spinner remains.
- Empty: grid filters remain accessible and Reset filters is available.
- Error: show an error alert with Retry and Reset filters.
- Success: retain the current Asset and Ringfence workflows.

### Accessibility and responsive behaviour

- Header/body scrolling regions are keyboard focusable and named.
- Active optional filters show their field and value when the panel is collapsed.
- Print uses the existing Assets print markers and removes scrolling constraints.

## 4. Technical plan

- Affected areas: Assets controller list query, Assets page/controls/table/timeline, shared Advanced Filters, and Frappe Gantt wrapper.
- API/data changes: broaden non-exact `warehouse` matching and broad `search`; no response-shape or database change.
- Existing patterns: Agreements fixed-column table and shared controls.

## 5. Acceptance criteria

- [x] Warehouse/location searches match either value without changing exact warehouse lookup behaviour.
- [x] Broad search covers every visible text-context column.
- [x] Table widths do not change between pages; rows are compact and the header stays pinned.
- [x] Delivery and DOH values are visible in the table.
- [x] Failed loads provide Retry and Reset filters.
- [x] Timeline shows warehouse and location, recentres on today after a scale change, and excludes history older than twelve months.

## 6. Verification

- Focused frontend Assets and timeline tests plus Assets controller tests.
- `npm test`, `npm run build`, `npm run lint`, and `npm run format`.
- Desktop table/timeline and print review when the authenticated application is available.

## 7. Decisions and open questions

| Item            | Decision or question                                                                                          | Owner / resolution |
| --------------- | ------------------------------------------------------------------------------------------------------------- | ------------------ |
| Service Day     | Unimplemented until an authoritative field and definition are supplied.                                       | Product/data owner |
| Estimated Ready | The ingestion query does not provide an authoritative value; omit it from the list until a source is defined. | Removed from list  |
