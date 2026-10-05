# Feature specification: frontend and WebApp maintainability refactor

## 1. Objective

**User and job:** Order Fulfillment developers need to understand, test, and change the React workflows and their API without loading unrelated implementation detail or accidentally changing operational behaviour.

**Problem:** The Agreements and Assets route components historically combined many independently understandable responsibilities, while several WebApp controllers combined transport, access, query, mapping, and orchestration concerns. The completed slices below reduce those concentrations and add regression evidence, but the Fulfilment response boundary and several behaviour decisions remain deliberately open.

**Outcome:** Feature-local code has clear ownership, controllers expose explicit and tested React-facing contracts, and developers can make focused changes with evidence that routes, workflows, persisted state, API responses, permissions, and side effects remain compatible.

## 2. Scope and boundaries

### In scope

- Add characterization tests for frontend services, persisted browser state, core React workflows, WebApp controllers, JSON responses, authentication/hosting, and mutation side effects.
- Split Agreements and Assets route components into feature-local controls, table, timeline, action, and pure-model modules.
- Introduce explicit API request/response DTOs that initially serialize to the existing wire shape.
- Extract feature-local WebApp query, mapping, codec, and policy functions where this makes controller behaviour independently testable.
- Clarify frontend service and type ownership without changing endpoint contracts.
- Move fulfilment orchestration to `OF.Common` only after its current side effects are characterized.
- Remove demonstrably dead code in a final, separately reviewable cleanup slice.

### Out of scope

- A visual redesign or workflow redesign.
- Database schema or integration changes.
- Route, HTTP method, query-name, request-body, response-shape, status-code, authentication, or saved-view schema changes in mechanical refactoring slices.
- Resolving known authorization, correctness, availability, or error-presentation discrepancies inside file-movement commits.
- Adding frontend state, query, form, grid, API-generation, mediator, or mapping libraries.
- CI, deployment, dependency, or vendor-file changes without separate approval. The approved CI Stage 1 is recorded below; it does not approve the deferred gates.
- New abstractions based only on superficial similarity between Agreements and Assets.

### Compatibility invariants

Unless a separately approved behaviour ticket says otherwise, preserve:

- Current React routes, including `/` and `/agreements`, and current production SPA deep-link behaviour.
- Canonical `/api/agreements` calls and `/api/orders` compatibility aliases, including the existing client fallback trigger.
- Existing API parameter names, inclusive date semantics, defaults, ordering, null fields, camel-case controller JSON, and meaningful `400`, `401`, `403`, `404`, and `503` outcomes.
- Raw fulfilment status codes `0`, `1`, `2`, and `3` at the API boundary.
- EasyAuth and development identity resolution plus the frontend `401` reload behaviour.
- Saved-view app/envelope/state/local-storage versions, page normalization, default ID `1`, scopes, source-specific mutation behaviour, fallback rules, and legacy browser-storage migrations.
- Current filter, sort, pagination, selection, row expansion, timeline navigation, reservation, activation, bulk-action, ringfence, and administration flows.
- Existing local-storage keys, print data attributes, focus treatment, accessible names, and keyboard paths.
- Mutation repository/queue/engine calls, their ordering, and partial-failure results until separately specified.

### Known discrepancies excluded from mechanical refactoring

These are tracked here so characterization work does not silently define them as desired behaviour:

- Raw fulfilment code `2` is a legacy enum value that the current engine does not emit; the agreed UI treatment is neutral `Unknown`.
- Agreement saved views previously rejected the otherwise supported `offHireDate` sort field; D02 restores decoding
  without changing the saved-view schema or version.
- Agreement list requests can complete out of order.
- Several frontend request failures are presented as empty or not-found states.
- Availability exclusion strings and permission-denied empty results are ambiguous.
- Asset list/detail, timeline-event, and availability division checks now share the documented access policy; mutation authorization remains a separate decision.
- Reservation reads and mutations now inherit shared Agreement access, and real Assets are division-checked at reservation creation. Bulk depot-fulfilment and rehire also inherit parent-Agreement access while retaining their existing subset/no-op processing semantics.
- The React bulk-action flow and processing semantics differ materially from OF.UI: React currently targets every loaded line in unfulfilled-only mode, while OF.UI offered checked-line selection and unfulfilled/all modes; quantity, legacy status `2`, rehire failure, and response behavior also differ. Treat parity as a separate behavior change, not a maintainability refactor.
- Activation and cancellation now inherit parent-Agreement access without adding controller-level state or eligibility restrictions. React still differs from OF.UI in activation eligibility, confirmation, retry/stop/stale/error presentation, and cancellation controls; treat that parity work separately from authorization.
- Ringfences now use OF.UI's division-visible collaboration rule: any division overlap grants management access, without owner/admin-only restrictions; new division assignments must all be within caller access, and new Asset items must be caller-accessible.
- React Ringfence management still differs from OF.UI in owner selection, constrained division/warehouse choices, edit/view controls, validation, duplicate-safe item adds, and semantic error responses. Treat parity separately from authorization.
- Reservation item/warehouse validity, bulk-action parity, activation retry/queue semantics, and Ringfence validation/parity still need dedicated decisions.
- Admin create/update now follows the existing React and OF.UI privilege boundary: only an existing super-admin may grant or revoke super-admin status. Self-demotion, last-super-admin protection, and role coupling are intentionally not added by this slice.
- Saved-view envelope casing and blank division-scope behaviour may not match the documented persistence contract.
- Production-shaped routing tests enforce the resolved host boundary: `/api` requests never use the SPA fallback; known correct-method controllers retain precedence; wrong methods retain ASP.NET's native `404` or `405`; non-API extensionless paths continue to serve the SPA.
- Activation state-change, queue-failure, retry, logging, and error-body semantics are not fully specified.
- `GET /api/fulfilment/stock/serialized` and
  `GET /api/fulfilment/stock/nonserialized` still return shared query-result
  types containing persistence models. Their consumer/parity contract must be
  established before introducing response DTOs or moving orchestration.

## 3. User experience

The mechanical refactor must not alter information hierarchy, visible fields, copy, controls, or interaction timing deliberately. Agreements, Assets, fulfilment, timelines, Ringfence, Admin, sidebar, mobile fallback, and print output must remain visually and functionally equivalent.

### States and edge cases to protect

- Loading, empty, error, success, disabled, and no-permission states.
- Table and timeline modes, visible periods, saved filters/sorts, no-event rows, and resizable panes where currently supported.
- Existing saved Agreement and Asset views, including local fallback and legacy `orders` migration.
- Reservation creation/removal, destructive confirmation, availability context, bulk actions, and activation failure.
- Keyboard focus, accessible status labels, mobile navigation fallback, and printable tables/timelines.

## 4. Technical plan

1. Add passing characterization tests and sanitized contract fixtures before moving production code.
2. Split Agreements into a route coordinator, feature-specific controls, saved-view controls, table, timeline, and pure list model. Keep saved-view orchestration feature-local.
3. Split Assets the same way, retaining request-ID protection, timeline semantics, and selection/ringfence behaviour.
4. Add named WebApp DTOs one endpoint family at a time, with golden JSON tests proving wire compatibility.
5. Extract small feature-local query, mapper, codec, and access-policy functions; keep controllers as HTTP adapters.
6. Clarify frontend service/type ownership and introduce only proven shared utilities such as explicit date formatting, safe browser storage, and asset-status interpretation.
7. Move bulk fulfilment orchestration only after side-effect tests pin current behaviour.
8. Deliver correctness, security, error-state, accessibility, localisation, and timeline improvements as separate specified slices.
9. Remove dead surface only after repository-wide consumer checks.

### KISS, DRY, and YAGNI rules

- Do not introduce a generic `useListPage`, `DataGrid`, `CrudPage`, `OperationalPage`, form builder, base controller, generic repository, or universal filter/event engine.
- Agreement and Asset query builders, saved-view orchestration, rows, and timeline mapping remain feature-specific.
- A shared component or utility requires a proven semantic pattern across at least three independently changing places.
- Prefer pure functions and named data contracts over configurable frameworks.
- No new dependency is required for the planned first phases.

### Progress at this documentation tranche

Completed, behaviour-preserving slices include:

- Characterization coverage for frontend services, saved browser state,
  Agreement and Asset page coordination, list/table/timeline models and
  presentation, saved-view orchestration, and the affected operational actions.
- Feature-local Agreement and Asset controls, saved-view controls/orchestration,
  list models, table presentation, and timeline model/presentation. Asset
  Ringfence actions are also separated from the route coordinator.
- Named WebApp response contracts and exact JSON tests for current-user and
  division lookups; Agreement list/detail; Asset list/detail/profile;
  availability and timeline events; reservations; bulk actions; activation;
  Ringfences; administration; and saved views. Agreement detail now explicitly
  preserves 36 Header plus 43 Line scalar/computed fields while omitting its six
  EF navigation graph properties, as documented in the Web API contract.
- Narrow feature-local WebApp code for Agreement/division access, Agreement,
  Asset, Ringfence, and saved-view response mapping, Asset profile schedule
  building, and Asset timeline fallback synthesis. Queryable projections remain
  inline where moving them could alter EF translation or query ordering.
- Separately approved and tested D01-D05 and D07 behaviour decisions: stable
  fulfilment mapping, saved-view `offHireDate` restoration, documented access
  policies, super-admin transition protection, and the API/SPA routing boundary.
- CI Stage 1: relevant WebApp/shared/frontend/workflow changes trigger the
  filtered Release `OF.Tests.WebApp` suite, which includes service-free Kestrel
  routing coverage. The latest local filtered suite at this tranche passed
  324 tests without external services.

The umbrella refactor is not complete. The following remain explicitly open or
deferred:

- Fulfilment stock responses still expose shared query-result/persistence
  models, and fulfilment orchestration has not moved. Consumer and OF.UI parity,
  side effects, and the intended DTO boundary require a dedicated decision and
  characterization slice first.
- D06 saved-view envelope casing/deployed-data migration, D08 activation
  queue-failure/retry/idempotency semantics, and D09 availability permission
  failure remain unresolved.
- Remaining query extractions and dead-code removal require their own consumer
  and EF-behaviour evidence; they are not justified merely to reduce file size.
- A repository-wide formatting gate remains deferred until its existing
  baseline is repaired and proven. Playwright remains deferred while its legacy
  `/orders` routes and authenticated data are unreliable. A PR container gate
  is also deferred; the current Docker job is limited to default-branch runs.

## 5. Acceptance criteria

- [x] Each moved responsibility has passing characterization tests before production code moves.
- [x] Existing frontend service requests and WebApp response fixtures remain equivalent in decision-critical fields, casing, nullability, status, and numeric mappings, except for the separately documented Agreement EF navigation-graph omission.
- [x] Existing Agreement and Asset saved views still decode and apply under the documented contract, including migrations and local fallback. D06 deployed casing evidence remains open.
- [x] Agreements and Assets preserve filters, sorting, pagination, table/timeline modes, selection, navigation, and print behaviour in the completed mechanical slices.
- [x] Completed mutation/access slices preserve characterized repository, queue, and engine call ordering until an approved behaviour change says otherwise. Fulfilment orchestration remains deferred.
- [ ] Controllers no longer expose persistence entities accidentally after equivalent named DTOs are established.
- [x] No speculative shared abstraction or unapproved dependency is introduced.
- [x] Known discrepancies are resolved only through separately accepted behaviour decisions and regression tests; D06, D08, D09, and Fulfilment remain open.

## 6. Verification

For every independently testable slice:

```powershell
# Frontend
npm test -- <focused-pattern>
npm test
npm run build

# Broad frontend slices
npm run lint
npm run format

# WebApp-focused tests
dotnet test src/OF.Tests/OF.Tests.csproj --filter "FullyQualifiedName~OF.Tests.WebApp"
```

- Use isolated .NET artifacts when a supported local WebApp process locks normal output.
- Add host-level publish/container smoke checks before changing production hosting or packaging.
- Compare relevant API requests/responses before and after every contract slice.
- Perform the manual grid, timeline, fulfilment, sidebar, keyboard, error-state, mobile-fallback, saved-view, and print checks defined in `docs/frontend/TESTING.md`.
- Never use production identities or unsanitized production records in fixtures.

## 7. Decisions and open questions

| ID | Decision or question | Status |
| --- | --- | --- |
| D01 | Raw fulfilment code `2` remains API-compatible but is not emitted by the current engine or supported as a visible Agreement state. | Resolved: render neutral `Unknown`; do not expose as a filter or relabel as fulfilled. |
| D02 | Agreement saved views sorted by `offHireDate` must decode and restore without a schema/version change. | Resolved: include `offHireDate` in the existing Agreement sort-field decoder. |
| D03 | Should normal users with no allowed division receive no Asset data, and may explicit Asset/Event/Availability divisions only narrow their access? | Resolved: return empty list/event/availability results (and `404` for inaccessible Asset detail/profile); explicit selections only narrow normal-user access; super-admins remain unrestricted. |
| D04 | What resource/division/ownership rules apply to reservations, bulk actions, activation, and ringfence mutations? | Resolved for authorization: reservation, bulk-action, activation, and cancellation routes inherit shared parent-Agreement access; reservation creation checks real Asset access while preserving equal-ID conventions. Ringfences use division-visible collaboration rather than owner/admin ownership, require every requested division to be assigned to the caller on writes, and check Asset access only when adding an item. Business validation and OF.UI parity remain separate work. |
| D05 | May an admin grant or revoke super-admin, or is that restricted to an existing super-admin? | Resolved: only an existing super-admin may grant or revoke super-admin status; ordinary admins retain non-super-admin management. No self-demotion, last-super-admin, or role-coupling rule is added. |
| D06 | Which saved-view envelope casing exists in deployed data, and how should camel/Pascal forms migrate? | Production-shaped sanitized sample required. |
| D07 | Should unknown `/api/*` routes always return an API `404` rather than SPA HTML? | Resolved: `/api` requests never use the SPA fallback; unknown `GET`/`HEAD` routes return `404`; correct-method controllers retain precedence; wrong methods keep ASP.NET's native `404`/`405`; non-API extensionless routes retain the SPA fallback. |
| D08 | What is the required activation result when local state changes but queueing fails, including retry and idempotency? | Business/integration decision required. |
| D09 | Should availability permission failure remain indistinguishable from genuine zero availability? | Dedicated compatibility decision required. |
| D10 | Are CI backend-test, formatting, Playwright, and PR container gates approved after they are proven locally? | Stage 1 resolved: relevant WebApp/shared/frontend/test/workflow paths trigger the filtered Release WebApp suite, including the service-free Kestrel routing smoke. Repository-wide formatting and Playwright remain deferred until their existing baselines are repaired and proven. The Docker job remains default-branch-only; it is not yet a PR container gate. |
