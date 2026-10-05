# Feature specification: asset-list server pagination

## 1. Objective

**User and job:** Fleet planners and warehouse users need to search, filter, and return to the Assets register without waiting for the complete authorised asset population to download.

**Problem:** `GET /api/assets` returns the complete list and the frontend applies most grid filtering, sorting, and pagination locally. In the development environment this response is approximately 10.7 MB and takes 8–11 seconds before the first page can be used.

**Outcome:** The Assets table loads one page of authorised assets and its total count, while preserving the existing filter, sort, saved-view, and paging workflow. The existing short-lived client cache remains a return-navigation optimisation.

## 2. Scope and boundaries

### In scope

- Add a paged Assets-list API contract without changing the array response used by existing availability callers.
- Move Assets-table filtering, sorting, total counting, and page selection to the server.
- Update the Assets table to request only its current page and retain its existing loading, error, saved-view, and selection behaviours.
- Authorise every result and count using the existing division-access rules.

### Out of scope

- Changing the existing `/api/assets` array response or callers such as fulfilment availability.
- Asset profile, enrichment, ringfence, or notes contracts.
- Asset timeline event-query reduction. Timeline loading must be assessed separately because it currently needs a complete range of assets/events.
- Cross-session or shared browser caching.

### Constraints and known rules

- The new endpoint must be additive because `AvailabilityPanel` currently consumes `fetchAssets` as an unpaged `Asset[]` response.
- Page size is user-selectable at 50, 250, 500, or 1,000; the server must enforce a sensible maximum.
- Saved views persist filters, sort order, page size, and page number. A page that becomes invalid after filtering must clamp to the final available page.
- Division access remains server-side; a supplied division is an additional restriction, never an override.
- The user-facing grid must keep its accessible controls and print behaviour.

## 3. User experience

### Primary flow

1. User opens Assets and receives the first page and total count for their active filters.
2. User changes a filter, sort, or page size; the table requests page one of the revised result set.
3. User changes page; the table requests that page without downloading the preceding rows.
4. User returns to the same query shortly afterwards; the client cache restores the page immediately.

### Information hierarchy

- Existing table columns, filters, count, and paging controls remain visible.
- The asset count represents the server-authorised filtered total, rather than the number of rows currently loaded.

### States and edge cases

- Loading: keep prior rows visible while a revised page loads, with existing loading feedback.
- Empty: show the existing no-assets explanation only after a successful zero-count response.
- Error: retain previous rows and show the existing retry action.
- Page beyond range: retry once using the final valid page returned by the server.
- Disabled/no permission: preserve current 401/403 handling and do not expose unauthorised counts.

### Accessibility and responsive behaviour

- Existing semantic paging controls, labels, focus behaviour, and desktop/mobile layout remain unchanged.
- Print prints the loaded page, as it does today; a full-register export is not introduced by this change.

## 4. Technical plan

- **Affected routes/pages/components:** new `GET /api/assets/paged`; `AssetsController`; `assetsService`; `AssetsPage`; Assets table/query tests; API contract documentation.
- **API/data changes:** additive envelope `{ items: AssetListItemResponse[], totalCount: number, page: number, pageSize: number }`. Query parameters reuse current asset filters and add `page`, `pageSize`, `sortField`, and `sortDirection`. Server supports the current Assets grid's committed text/date column filters so filtering occurs before count and paging.
- **Existing patterns/components to reuse:** existing `AssetFilterParams`, server division filtering, `AssetsTable` paging controls, `AssetsPage` request-id race protection, and the in-memory per-query cache.
- **Proposed tasks, each independently testable:**
  1. Define the additive request/response DTOs, validation limits, and controller tests for filtering, sorting, page boundaries, total counts, and division access.
  2. Implement the authorised SQL query/projection so filtering, count, sorting, and `Skip`/`Take` occur before materialising rows.
  3. Add a frontend paged-assets service and migrate the table mode to it, preserving prior rows during a request and resetting/clamping pagination correctly.
  4. Keep timeline mode on the existing unpaged route until its data requirements have a separate design.
  5. Update the API contract and run focused, full frontend/backend, and visual verification.

## 5. Acceptance criteria

- [ ] Opening the default Assets table transfers at most one configured page of rows, not the full authorised list.
- [ ] Table filtering and sorting return the same visible results and ordering as the current workflow for representative data.
- [ ] Total count, page navigation, saved views, Refresh, error recovery, and division authorisation work correctly.
- [ ] Existing availability callers continue receiving the current unpaged array contract.
- [ ] Returning to the same table page/query within the client cache TTL does not issue a duplicate request.

## 6. Verification

- Focused tests: `AssetsControllerTests`, paged-assets service tests, `AssetsPage.test.tsx`, query-model tests.
- Build/lint/format commands: backend test project, `npm test`, `npm run build`, `npm run lint`, and `npm run format`.
- Manual visual checks: 50/250/500/1,000 page sizes; filter/sort/page transitions; saved-view restore; retry/empty states; keyboard paging; normal desktop and print.
- Data checks: measure first-response payload and duration in the development environment before and after rollout.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| Route shape | Use additive `GET /api/assets/paged` rather than breaking current `/api/assets` array consumers. | Proposed; approval required. |
| Timeline | Leave timeline on the existing list route for this phase; design its bounded query separately. | Proposed; approval required. |
| Sort/filter parameters | Define explicit, allow-listed fields rather than accepting arbitrary database field names. | Proposed; implementation detail. |
| Target | Confirm an acceptable first-page payload and response-time target for dev and production. | Product/engineering decision. |
