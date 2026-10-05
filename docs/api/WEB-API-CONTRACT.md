# Web API and persisted views contract

## Purpose and scope

This document describes the current React-facing contract between `src/OF.Frontend` and `src/OF.WebApp`. It is a concise compatibility contract, not a hand-written replacement for OpenAPI or the controller source.

Use it before changing a frontend service, controller route, filter, response field, saved-view state, authentication behaviour, or numeric status mapping.

## General rules

- The canonical client is the React frontend. Its service modules live in `src/OF.Frontend/src/services`.
- API routes are relative `/api/*` routes. In development, Vite proxies them to `https://localhost:7200`; in production the SPA and API share the same origin.
- JSON uses camel-case property names.
- Endpoints require a resolved Order Fulfillment identity unless explicitly documented otherwise. `GET /api/auth/me` returns `401` when no authenticated EasyAuth principal is available. When EasyAuth has authenticated the person but their login is absent from the NOF `Users` table, it returns `403 application/problem+json` with `code: "user_not_provisioned"`; the response does not disclose the login name. The frontend reloads on other API `401` responses so EasyAuth can re-establish an expired session, but renders an authentication state instead of reloading when the current-user probe itself returns `401`.
- Production identity comes from Azure App Service EasyAuth. Development fallback is `LocalUserEmail`; see the [local development runbook](../development/LOCAL-DEVELOPMENT.md).
- `400`, `403`, `404`, `409`, and `503` are meaningful contract outcomes. Do not silently turn them into an empty successful result in the UI.
- Date query values use ISO date form (`YYYY-MM-DD`). Current date-range filters are inclusive where the controller applies `>=` and `<=`.
- Division access is enforced server-side for Agreements, Assets, Asset timeline events, and availability. The client may present a division selector but must not treat it as the security boundary.
- A configured `ReadOnly` role is an API-enforced deny on operational, Administration, and saved-view mutations. The frontend action state is not the security boundary.

## Canonical endpoints

Warehouse lookups include additive `facilityName`, sourced from the warehouse catalogue. The existing `facility` field remains the facility code (or its legacy fallback), preserving selection and grouping keys.

| Area | Canonical route(s) | Contract notes |
| --- | --- | --- |
| Current user | `GET /api/auth/me` | Returns `loginName`, `displayName`, division CSV, `isAdmin`, `isSuperAdmin`, additive `isReadOnly`, and language. A successful configured-user bootstrap records Last NOF access; the timestamp is not returned here. Returns `401` without an authenticated principal and a coded `403` when the authenticated account is not configured for NOF. |
| Fleet Planner work | `GET /api/my-work` | Returns personalised dashboard totals, breakdowns, a bounded Agreement/alert register, and the disclosed ownership basis. Optional `changedSinceUtc` is a UTC instant used only for change labelling. |
| Planner assistant | `POST /api/planner-assistant/query` | Accepts a question, optional change baseline, and bounded conversation history. Returns read-only, tool-grounded prose, mode, legacy Agreement links, and typed server-approved Agreement, Asset, or event sources; it exposes no mutation command. |
| Runtime configuration | `GET /api/app-config` | Returns non-secret environment label, preview-banner visibility, a validated legacy-frontend URL, and (only when explicitly enabled outside Live) current release/commit metadata. Values are supplied at runtime and responses are not cached. |
| Agreements | `GET /api/agreements`, `GET /api/agreements/{headerId}` | Main agreements list/detail contract. Its additive list fields include `fromDate`, `toDate`, `lastUpdatedDate`, `opportunityStage`, and `probability`. `GET /api/orders` and `/api/orders/{headerId}` remain legacy-compatible aliases. |
| Agreement equipment | `GET /api/agreements/{headerId}/equipment/catalog`, `GET /api/agreements/{headerId}/equipment/catalog/generics/{genericId}`, `POST /api/agreements/{headerId}/equipment`, `DELETE /api/agreements/{headerId}/equipment/{lineId}` | Division-authorised catalogue browsing and guarded creation/removal of local pending equipment demand. See [Adding operational equipment](#adding-operational-equipment). |
| Assets | `GET /api/assets`, `GET /api/assets/{id}`, `GET /api/assets/{id}/profile`, `GET /api/assets/{id}/enrichment` | Asset list, profile, schedule/ringfence context, and independently loaded MDP location, service, retrofit, and rental-history enrichment. Its additive grid fields include warehouse/customer/product context, telemetry, remarks, and `size`, sourced from `USSizeRating`. Profile accepts `fromDate` and `toDate`. Enrichment accepts `serviceLimit` from 1 to 50 (default 20), authorises the NOF asset before querying its individual-item number, and can return `503` without making the core profile unavailable. Rental history is bounded to the 100 most recent agreement rows and includes customer and validity dates. |
| Record notes | `GET/POST /api/notes/assets/{assetId}`, `PUT /api/notes/assets/{assetId}/{noteId}`, and equivalent `/api/notes/agreements/{headerId}` routes | Retrieves all plain-text notes for an accessible asset or agreement, adds a note, or updates a note belonging to that record. Writes accept `{ notes }` and are available to any authenticated user with access to the parent record; updates verify both note type and parent ID. |
| Division, owner, and warehouse lookups | `GET /api/lookups/divisions`, `GET /api/lookups/users?division=110%2C150`, `GET /api/lookups/warehouses?division=110%2C150` | Returns displayable division data, division-scoped owners (`loginName`, `fullName`), and configured warehouse metadata. Owner and warehouse lookups require an explicit comma-separated division selection and retain server-side division access. |
| Saved views | `GET/POST /api/views`, `GET /api/views/recipient-candidates`, `PUT/DELETE /api/views/{id}` | See [Persisted saved views](#persisted-saved-views). |
| Reservations | `GET /api/reservations/header/{headerId}`, `GET /api/reservations/{id}`, `POST /api/reservations`, `DELETE /api/reservations/{id}` | Reservation create/delete and header context. |
| Quantity stock reservations | `POST /api/fulfilment/stock/nonserialized/reservations` | Accepts line ID, item number, warehouse, and the stock quantity selected by the user. The server validates period availability and stores the entered quantity. |
| Availability | `GET /api/availability/summary` | Requires `genericCode`; accepts optional `lineId`, `itemNumber`, `attributes`, `startDate`, `endDate`, and one or more comma-separated division codes in `division`. Agreement detail supplies `lineId` so substitution selection mirrors the legacy line and active-reservation rules. Results include warehouse, facility, division, and substitution metadata. |
| Bulk fulfilment | `POST /api/bulkactions/depot-fulfil`, `POST /api/bulkactions/rehire` | Request includes `headerId`, `lineIds`, optional `warehouse`, and `includeAlreadyFulfilled`; returns `{ processed }`. Depot fulfil creates a reservation for each processed line's outstanding quantity (or its full quantity when replacing existing fulfilment). |
| Activation | `POST /api/activation/{headerId}`, `POST /api/activation/{headerId}/cancel` | Activation can return `503` when Service Bus is unavailable. |
| Asset timeline events | `POST /api/event/events` | Body: `startDate`, optional `endDate`, `divisions`, and `assetIds`; response groups events by asset ID. |
| Ringfences | `GET/POST /api/ringfence`, `GET/PUT/DELETE /api/ringfence/{id}`, `POST /api/ringfence/{id}/overlaps`, `POST /api/ringfence/{id}/items/preflight`, `POST /api/ringfence/{id}/items/batch`, and legacy item add/remove routes | The selected-record response includes its item audit rows and displayable asset rows. Create/update preserve title, period, divisions, owner, and warehouse. The overlap route previews existing overlapping Ringfences before an asset add; it does not mutate data. |
| Administration | `GET/POST /api/admin/users`, `PUT/DELETE /api/admin/users/{loginName}`, `GET /api/admin/options`, `GET /api/admin/people?search=...` | Admin-only user management, controlled setting options, server-side company-directory lookup, and the nullable UTC `lastLoginAtUtc` audit value in user responses. |

`GET /health/live` and `GET /health/ready` are unauthenticated hosting probes. They do not expose application data or replace authenticated API verification.

The exact request/response shape for each operation remains defined by its controller and the matching frontend service/type. Change both deliberately and update this document if the compatibility contract changes.

### Ringfence write and asset-assignment outcomes

- Ringfence create and update require a non-blank `title` and `divisions`, plus an ordered date range. Create also requires a configured warehouse for one of the selected divisions whose trimmed code ends in `0`; an omitted or blank owner defaults to the authenticated user. On update, omitted or blank owner and warehouse values retain the stored values (a blank legacy owner falls back to the authenticated user, and a blank legacy warehouse remains blank). A replacement warehouse must be configured for one of the selected divisions and have a trimmed code ending in `0`; unchanged legacy owner/warehouse values remain valid while the normalized division selection is unchanged. Changing either reference or the division selection revalidates both. Creating a Ringfence, or changing its normalized division selection, requires every requested division to be assigned to the caller; a collaborator may otherwise edit metadata on a shared Ringfence. Creation rejects dates before the current UTC date. Existing historical Ringfences remain editable when their historical dates are preserved, but a date cannot be changed to a past date.
- Validation responses use `application/problem+json` with a clear `title`, `detail`, legacy-compatible `message`, and field-level `errors`. Keys are `title`, `fromDate`, `toDate`, `divisions`, `owner`, and `warehouse`; a duplicate title is a `409` problem with an error on `title`. Invalid fields return `400`; rejected division assignments remain `403`.
- Ringfence list rows include additive `assetCount` (a number). `POST /api/ringfence/{id}/items` remains available for compatibility and is idempotent: re-adding an assigned Asset returns the existing item without a second write.
- `POST /api/ringfence/{id}/items/preflight` and `POST /api/ringfence/{id}/items/batch` accept `{ assetIds: string[], acknowledgeOverlaps?: boolean }`. Both normalise, de-duplicate, and limit the request to 250 asset IDs. They return `{ readyAssetIds, alreadyAssignedAssetIds, unavailableAssetIds, addedAssetIds, overlaps, requiresOverlapAcknowledgement }`.
- Preflight does not mutate data. Batch re-evaluates availability and overlaps immediately before it writes: unavailable IDs produce `400` with the result body, while overlaps produce `409` with the result body unless `acknowledgeOverlaps` is explicitly `true`. On success, batch returns `200` and populates `addedAssetIds`. New Ringfence and Assets flows should use batch rather than treating the legacy single-item endpoint as an overlap acknowledgement boundary.

### SPA routing boundary

Known controller routes take precedence. An unmatched `GET` or `HEAD` under the `/api` path segment returns `404` and is never served as SPA HTML. Wrong-method API requests retain ASP.NET's native `404` or `405`, but also never use the SPA fallback. Unmatched non-API extensionless paths continue to serve `index.html` so React deep links work, while extension-bearing unknown paths do not match the SPA fallback.

### Availability caveat

Each availability summary row includes `warehouseCode`, `warehouse`, `facility`, `divisionCode`, and `divisionName` as location metadata alongside the item and availability fields. `reservationMode` is `asset`, `quantity`, or `generic`, allowing the frontend to select individual assets or enter a stock quantity as appropriate. `substitutionReason` is `RELATED` for a specifically related substitute and `null` otherwise. Related-specific matching mirrors the legacy fulfilment procedure: the optional `itemNumber` identifies exact parent relationships, while the selected generic also includes relationships configured against any parent item in that generic. If the stored-procedure row omits hierarchy metadata, the controller backfills it from the authorised warehouse reference using `warehouseCode` when that reference is available. A normal user's comma-separated `division` selection is intersected with their assigned divisions before the repository query; the response can therefore be grouped by division, facility, and warehouse without weakening the API authorization boundary.

The endpoint is backed by `dbo.GetFulfilmentAvailabilitySummary`, which calls `dbo.GetFulfilmentGenericsWithSubstitutions`. These are NOF-specific contracts: `reservationMode`, all configured generic substitution purposes, and NOF quantity-availability rules belong there. `dbo.GetCPQAvailabilitySummary` and `dbo.GetGenericsWithSubstitutions` remain CPQ Next-only compatibility contracts; do not add NOF fields or business rules to them.

For a `quantity` row, `available` is the non-negative whole-unit result of `ProductItems.StockQuantity - ProductItems.AllocatedQuantity`, aggregated for the item and warehouse, less the peak quantity of simultaneously active unconfirmed reservations that overlap the inclusive requested period. Confirmed reservations are not subtracted again because upstream `AllocatedQuantity` is expected to include them. Disjoint pending reservations therefore reduce availability by their peak overlap, not by their total across the entire period.

For an `asset` row, serialized reservations and Ringfences continue to block the asset when they overlap the inclusive requested period. The current asset state is also period-aware for `OnHire`: an asset with a non-blank agreement number and a known release date is counted as available in the summary after that release date, using `CollectionDate`, then `TerminationDate`, then `AgreementLineValidToDate`. A release on the requested start date still overlaps, so availability begins the following day. An `OnHire` asset without a known release, an on-hold asset without an agreement number, and other non-`Available` operational states remain unavailable. This rule uses the asset's current M3 agreement dates as a fallback commitment; it does not change quantity-stock availability or the CPQ Next procedure.

If `startDate` or `endDate` is omitted, the missing side of the availability period is treated as open-ended. If `endDate` is earlier than `startDate`, the procedure uses `startDate` for both bounds. Agreement-driven requests normally supply both dates.

`src/OF.Data.Design` is the sole deployable source of truth for these shared-database procedure and function definitions. Its DACPAC owns their deployment; duplicate SQL definitions in an application project or another repository are reference-only and must not be deployed independently. Common calculations may be extracted into internal functions later, but the CPQ and NOF caller-facing procedure contracts remain separate. Deploy the DACPAC before the NOF application change and ensure the application principal has `EXECUTE` permission on `dbo.GetFulfilmentAvailabilitySummary`.

`GET /api/lookups/warehouses` is the additive configured-column source for availability. It requires a non-blank `division` query and returns a flat array containing only `warehouseCode`, `warehouse`, `facility`, `divisionCode`, and `divisionName`. A normal caller's explicit selection is intersected with their assigned divisions; a super-admin may request any explicit divisions. No authorised overlap returns `200 []` without querying warehouse data. Codes and metadata are trimmed, blank codes are omitted, and duplicate warehouse codes are collapsed case-insensitively. The endpoint retains the repository's excluded-warehouse rules and does not grant reservation access by itself.

The frontend unions these configured locations with warehouses present in the summary. A configured warehouse may therefore have a column even when an item has no summary row there; that cell is missing data and renders as a non-interactive em dash, never as a synthetic zero. Warehouse codes are not filtered by suffix; all locations follow the same stock, facility and warehouse visibility filters. If the configured lookup fails, the frontend keeps usable summary-derived columns and reports the lookup problem separately.

The current availability endpoint logs SQL permission-denied error `229` and returns an empty list. Treat an unexpected empty availability result as a possible connection/permission problem, not proof that no inventory is available. This is an existing compatibility behaviour and should be revisited as part of a dedicated availability/error-handling change.

## Agreements and Assets list queries

### Agreements

`GET /api/agreements` supports core query fields including:

- `division` — one or more comma-separated division codes.
- `search` — agreement number, customer name, or customer number.
- `orderType` — singular compatibility filter: `quote`, `temporaryAgreement`, or `agreement`.
- `orderTypes` — one or more comma-separated order types. When supplied, it takes precedence over `orderType` and matches any selected value.
- `status` — singular compatibility filter using a **raw fulfilment status code; see the important mapping below.**
- `statuses` — one or more comma-separated raw fulfilment status codes. When supplied, it takes precedence over `status` and matches any selected value.
- `hideFulfilled`, `showHistorical`, `take`.
- Field filters for agreement/customer/warehouse and on-hire, off-hire, delivery, valid, termination, collection, customer-address, and last-updated values.

The list exposes data used by the current grid/timeline. In addition to the established fields, its additive legacy-grid
surface includes `fromDate`, `toDate`, `lastUpdatedDate`, `opportunityStage`, `probability`,
`minFulfilmentStatus`, and `maxFulfilmentStatus`. `noteCount` is the count of `agreement` notes for that row and is
zero when none exist. Keep field additions backward compatible where possible; a new high-value grid field may also
affect saved views and print.

#### Agreement detail scalar boundary

`GET /api/agreements/{headerId}` and its `/api/orders/{headerId}` alias return an object with `header` and `lines`. The explicit response contract preserves the intended scalar and computed surface in its established camel-case order:

| Response object                | Preserved fields | Intentionally omitted EF navigation graph properties                |
| ------------------------------ | ---------------: | ------------------------------------------------------------------- |
| `header`                       |               36 | `currentChangeOrder`, `changeOrderHeaders`, `changeOrders`, `lines` |
| Each item in top-level `lines` |               43 | `header`, `changeOrderLines`                                        |

The top-level `lines` collection remains part of the response. Only the six persistence navigation properties above were pruned; all 79 established scalar/computed Header and Line fields, including their values, nulls, types, casing, and order, remain covered by the Agreement controller contract tests. This is an API response-graph boundary and did not remove or alter frontend navigation.

#### Adding operational equipment

The agreement-equipment routes support missing operational demand; they do not split or reduce an existing line. They require a resolved identity and the same Agreement division access as detail. Missing or inaccessible headers and foreign source/target lines return `404`.

- The header must be non-deleted and stable: a `T` agreement in activation status `TODO`, or an `A` agreement in activation status `Activated`. An add source must be a non-deleted, fulfilment-required root line in the corresponding line state. Literal `Q` records, dotted sources, and Requested or Failed states are excluded. Stale or otherwise ineligible existing state returns `409`.
- `GET /api/agreements/{headerId}/equipment/catalog` returns `{ productLines, generics }` for active equipment available in the authorised header division. Product lines contain `id`, `description`, and `familyDescription`; generics contain `id`, `productLineId`, `code`, and `description`.
- `GET /api/agreements/{headerId}/equipment/catalog/generics/{genericId}` returns `{ generic, attributes, items }`. Attribute entries contain `name` and `values`; item entries contain `itemNumber`, `description`, and `genericId`. The optional `attributes` query accepts either repeated `Name:Value` values or a semicolon-delimited `Name:Value;Name:Value` value. An unavailable generic returns `404`; malformed, duplicate-group, or unavailable attribute selections return `400`.
- `POST /api/agreements/{headerId}/equipment` accepts only `{ parentLineId, genericId, itemNumber?, attributes, quantity }`. `itemNumber` is optional for generic-only demand, `attributes` is an array of `Name:Value` strings, and `quantity` is a whole number from 1 through 1,000. The server revalidates product/division membership and inherits logistics from the source; the client cannot submit inherited fields. Success returns `201` with the created local dotted line summary. Invalid item, attribute, or quantity data returns `400`; an unavailable generic or foreign source returns `404`; stale source/header eligibility returns `409`.
- The created line is independent additional demand. It starts unfulfilled with `ActivationStatus.TODO`, receives an atomic never-reused `<source line>.<integer>` temporary number, and does not alter source quantity. M3 acknowledgement may later replace that dotted number; no durable parent relationship is promised.
- `DELETE /api/agreements/{headerId}/equipment/{lineId}` only soft-deletes a non-deleted dotted line that remains local in `TODO` and has no reservations. It never deletes reservations or sends a downstream M3 delete. Root/activated/in-flight lines and lines with reservations return `409`; missing or foreign lines return `404`; success returns `204`.

The fuller business, UX, safety, and verification contract is in [Add operational equipment](../specs/add-operational-equipment.md).

### Assets

`GET /api/assets` supports core query fields including:

- `division`, `warehouse`, singular compatibility `status`, `excludeStatuses`, `take`.
- `statuses` accepts one or more comma-separated Asset statuses. When supplied, it takes precedence over `status` and matches any selected value.
- `exactMatch=true` makes supplied `warehouse` and `itemNumber` filters use case-insensitive equality; the default remains substring matching for search workflows. Availability cell lookups use the exact mode.
- `search` across asset ID, item number, status, warehouse/location, division, facility, description, agreement, and customer context.
- The default substring `warehouse` filter matches warehouse code or warehouse location; `exactMatch=true` remains warehouse-code equality only.
- Field filters for facility, item/individual item, description, agreement, warehouse location, delivery/valid/termination/collection/estimated-ready dates.

The asset list includes `daysOffHire` and the agreement/customer/warehouse context used by the current React grid.
Its additive legacy-grid surface includes `warehouseName`, `customerNumber`, `productGroup`, `productCategory`,
`runHours`, `size`, `telemetryStatus`, and `remark`. `noteCount` is the count of `asset` notes for that row and is zero
when none exist. Asset access is checked for detail/profile routes as well as constrained through the list query.

Warehouse lookup responses omit the repository-maintained inactive warehouse set in
`Constants.Warehouses.Excludes`; this affects selectable reference data only and does not hide historical Agreement or
Asset rows. Division lookup code `200` is always displayed as `USA`, regardless of the source country description.

## Authorisation and visibility

### Read-only role

- `ReadOnly` is stored in the existing comma-separated `Users.Roles` value and is matched case-insensitively. No separate database flag is used.
- `GET /api/auth/me` returns `isReadOnly: true` when the active identity contains that role.
- `ReadOnly` is mutually exclusive with Admin and Super Admin when a user is created or updated. The Admin API returns `400` for a conflicting request. If inconsistent stored data contains both, read-only restrictions take precedence.
- Read-only users retain their normal division-authorised reads. `POST /api/event/events`, `POST /api/ringfence/{id}/items/preflight`, and `POST /api/ringfence/{id}/overlaps` are explicitly classified read-only queries and remain available.
- Reservation creation/deletion, quantity-stock reservation, bulk fulfilment/rehire, equipment creation/deletion, activation/cancellation, pull/import, Ringfence writes, Administration routes, and saved-view writes return `403` before their action executes.
- Every controller action using a non-GET HTTP method must carry exactly one explicit classification: `DenyReadOnly` for a mutation or `ReadOnlyQuery` for a permitted query. The controller inventory test fails when a new non-GET action is left unclassified.

- Agreement and Asset **list** requests apply the caller's allowed divisions; an explicit selection can only narrow a normal user's result. A super-admin can query all divisions or explicitly select a subset.
- Asset, Asset timeline-event, availability, and configured warehouse lookup requests intersect an explicit division selection with a normal user's assigned divisions. Asset list, availability, and the warehouse lookup use comma-separated selections; Asset timeline events accept comma- or semicolon-separated selections.
- With no explicit selection, Asset and Asset timeline-event requests use all of a normal user's assigned divisions; availability preserves its legacy behavior of querying the first assigned division.
- A normal user with no assigned division, or whose explicit selection has no intersection with their assigned divisions, receives an empty successful Asset list, event map, availability list, or configured warehouse list. Asset detail and profile instead return `404` when the Asset is missing or inaccessible.
- A super-admin can request all divisions or explicitly select a subset for these read operations.
- `GET /api/agreements/{headerId}` and its legacy `/api/orders/{headerId}` alias enforce the caller's Agreement division access before returning header or line data. A missing or inaccessible agreement returns `404`; a normal user with no configured division cannot access agreement detail.
- Agreement-equipment catalogue/create/remove routes inherit the parent Agreement's division access and return `404` for a missing, inaccessible, or foreign resource. The API rechecks stable T/TODO or A/Activated state and line eligibility during each mutation; frontend action visibility is not an authorisation boundary.
- Reservation list/detail/create/delete routes inherit their parent Agreement's division access. Any caller who can access the Agreement may manage its reservations; reservation creator ownership is not an additional restriction. Missing, orphaned, or inaccessible reservation resources return `404`.
- Creating a reservation also checks the submitted `assetId` against persisted Assets. If it identifies a real Asset, that Asset must be within the caller's allowed divisions; multi-division users may reserve from any division they can access, and super-admins remain unrestricted. A missing Asset with distinct `assetId` and `itemNumber` returns `404`. Equal identifiers retain the established non-serialized, depot-fulfilment, and rehire item convention when no Asset exists. Existing reservation reads/deletes remain Agreement-scoped so historical reservations do not become stranded.
- Bulk depot-fulfilment and rehire routes inherit the parent Agreement's division access and return `404` when that Agreement is missing or inaccessible. After that check, processing remains header-scoped and subset-based: only eligible lines returned for the header and selected by `lineIds` are processed. Unknown, foreign, empty, or duplicate selections do not turn the request into an atomic validation failure.
- Activation and activation-cancellation routes inherit the parent Agreement's division access and return `404` when that Agreement is missing or inaccessible. This is an access boundary only: it does not add controller-level fulfilment, activation-status, ownership, or retry eligibility rules, and it preserves the existing local-state-then-queue ordering and `503` behavior.
- Ringfences retain OF.UI's division-visible collaboration model. Normal users list and manage a Ringfence when any of its comma-separated divisions intersects their assigned divisions; Owner and CreatedBy are metadata, not mutation ACLs. Super-admins are unrestricted. Missing or inaccessible Ringfences return `404` before child reads or writes.
- Creating a Ringfence, or replacing its divisions, requires every requested division to be assigned to that normal user; a rejected assignment returns `403`. Adding an item also requires an existing Asset within the caller's Asset access, but the Asset need not match the Ringfence division or warehouse. Removal remains parent-Ringfence scoped so historical or deleted Asset references can still be removed.
- Administration list/create/update, options, and directory-search routes accept a non-read-only admin or super-admin; delete remains super-admin-only. User responses include divisions, language, date format, Admin/Super Admin status, optional feature-role CSV, and nullable `lastLoginAtUtc`. Last NOF access is the most recent successful configured-user `GET /api/auth/me` bootstrap, not an authoritative Microsoft Entra sign-in time; failed or unknown authentication does not update it, writes are monotonic, and an audit-write failure is logged without denying otherwise valid access. Admin create/update requests cannot set the audit field. Options always include `ReadOnly`; optional roles are included only when the existing role feature is enabled, as indicated by `rolesEnabled`. Directory search requires at least two characters and calls Microsoft Graph from the server so credentials are never exposed to the browser; an unavailable directory returns `503`. Only an existing super-admin may create a user with `IsSuperAdmin = true` or change an existing user's `IsSuperAdmin` value. Ordinary admins may continue changing non-super-admin fields when that value is preserved. A rejected privilege transition returns `403` before any write; ReadOnly combined with Admin/Super Admin returns `400`. This boundary does not add self-demotion, last-super-admin, or `IsAdmin`/`IsSuperAdmin` coupling rules beyond ReadOnly exclusivity.
- Saved-view global/division scopes can only be created or updated by an admin. Any write-enabled owner may use the `users` scope with eligible configured recipients. A view owner or admin may edit/delete a view; receiving a view alone does not grant either permission. Read-only users can list and apply visible views but cannot create, update, or delete server views.
- A successful list response does not grant permission to perform a mutation; endpoint-specific business and authorisation checks must be verified when changing a mutation. Reservation item/warehouse/status/availability validation, bulk-action business parity, activation eligibility/retry semantics, and Ringfence validation/parity remain under review.

## Fulfilment status

`GET /api/agreements`, agreement detail, and the `status` query parameter use
the following raw database values directly:

| Code | Meaning                                                                         |
| ---- | ------------------------------------------------------------------------------- |
| `0`  | Unfulfilled                                                                     |
| `1`  | Partially fulfilled                                                             |
| `2`  | Legacy `Overfulfilled` enum value; not emitted by the current fulfilment engine |
| `3`  | Fully fulfilled                                                                 |

This is the authoritative API contract. The React client represents these values
as `ApiFulfilmentStatus` and maps them once to presentation states; it never
infers a scheme from the result set. Raw code `2` is retained for API compatibility because it remains in the database enum,
but is not emitted by the current fulfilment engine or supported as a user-facing
Agreement state or filter; it renders neutrally
as `Unknown` rather than being relabelled as fully fulfilled. There is no raw API
code or Agreement UI option for `Finished`.

## Persisted saved views

Saved views are a user workflow contract: changes must not silently make a person’s saved grid/timeline unusable.

### Server persistence

The server stores a frontend envelope in `ViewJson`:

```json
{
  "app": "nof-frontend",
  "version": 2,
  "page": "agreements",
  "state": {}
}
```

- `app` must be `nof-frontend`.
- Current writes use envelope `version: 2`; reads accept compatible version `1` or later.
- Supported pages are `agreements` and `assets`. `orders` is accepted and normalised to `agreements` for migration compatibility.
- `state` must be a JSON object.
- API DTO IDs are numeric; the frontend exposes them as strings for a common local/API interface.
- Create/update bodies accept additive `recipients: string[]`. The field is used only with `scope: "users"`; other scopes
  clear any existing recipient links. Recipient identifiers are trimmed and case-insensitively deduplicated, the owner
  is excluded, and the entire write is rejected before persistence if no valid recipient remains or any submitted login
  is not eligible. Recipient-set replacement is serialized per view and committed with the view update so concurrent
  editors cannot merge two stale recipient selections. If a recipient is deleted between validation and persistence,
  the FK failure is translated to a clear atomic `400` response.
- List/create/update responses include additive `isOwner` and `recipients` fields. `recipients` contains
  `{ loginName, fullName }` entries for callers allowed to manage that view and is empty for apply-only recipients.
- Recipient links are stored in `ViewRecipients`, never in `ViewJson`. Deleting either the recipient user or the view
  cascades only the corresponding link rows.

### Scopes and access

| Scope      | Stored value                                  | Who can read it                                    | Who can create/change it                  |
| ---------- | --------------------------------------------- | -------------------------------------------------- | ----------------------------------------- |
| `personal` | `ForEveryone = 0`, no recipient rows          | Owner                                              | Owner or admin                            |
| `users`    | `ForEveryone = 0`, one or more recipient rows | Owner and current named recipients                 | Owner or admin; recipients are apply-only |
| `global`   | `ForEveryone = 1`                             | All users                                          | Admin                                     |
| `division` | `ForEveryone = 2`                             | Users whose divisions intersect the view divisions | Admin                                     |

`GET /api/views/recipient-candidates?search=alex&limit=10` returns configured `{ loginName, fullName }` users eligible
for the caller. Search is case-insensitive across full name and login. Blank or omitted search returns an empty list so
the endpoint does not expose an initial directory page; the optional limit defaults to `10` and must be between `1`
and `25`. Results are ordered by match quality (exact, name prefix, login prefix, name-word prefix, then contains),
with name/login tie-breakers. A normal caller sees users sharing at least one authorised division; a Super Admin sees
all configured users. The caller is excluded. Create/update rechecks the complete, unbounded eligible set in the server
operation before the atomic recipient write, and a `users` view requires at least one recipient. Read-only users receive
`403` from the candidate endpoint because they cannot create or change sharing.
The normal saved-view list includes a view only when the caller is its owner, a current recipient, globally authorised,
or within its division scope; Admin status alone does not make an unrelated personal or named-recipient view visible.

`ReadOnly` overrides the write permissions in this table: a read-only user can apply a visible view but all server view mutations return `403`.

The API identifies its legacy default view as ID `1`; do not introduce a separate default-view meaning without a deliberate contract change.

### Frontend state and local fallback

- Current Agreement and Asset page writes use `stateVersion: 5`, `viewMode`, core filters, `advancedFilters`,
  `columnFilters`, timeline-column filters, date-column filters, sort field/direction, pagination, and independently
  validated `columns` and `timelineColumns` layouts containing stable keys, visibility, order, and clamped widths.
  Free-text column filters are arrays with OR semantics; identifier fields use case-insensitive exact matching and
  descriptive fields use case-insensitive contains matching.
- The frontend validates saved state with page-specific decoders. Version-1 through version-4 states remain readable;
  legacy string free-text filters become one-value arrays and missing timeline layout/filter state receives product
  defaults. Unknown, duplicated, missing, or out-of-range
  column entries are repaired individually; an invalid future column never makes the whole saved view unusable.
- Automatic working context uses guarded, versioned `sessionStorage` entries scoped by normalized login and page.
  It restores only when there is no explicit saved-view selection or route hand-off context and never persists loaded
  rows, selected assets, transient feedback, or dialogs. Named saved views remain the durable cross-session contract.
- Local fallback uses browser local storage key `nof.savedViews.v1` with schema version `1`. Local views are always personal.
- Listing falls back to local views when the API cannot be read. Creating falls back to local only for a network/no-response failure; update/delete use their selected source and do not silently convert an existing server view into a local view.

### Migration rule

When changing saved-view state, filter/sort key names, scope behaviour, or supported modes:

1. Update the relevant page decoder and tests.
2. Maintain backward compatibility or write an explicit migration path.
3. Update this contract and the affected feature specification.
4. Manually verify an existing saved Agreement and Asset view before release.
