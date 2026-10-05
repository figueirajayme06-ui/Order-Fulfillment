# Feature specification: fulfilment-status mapping and agreement-detail authorisation

## 1. Objective

A fulfilment user must see an accurate, stable Agreement status and must not be
able to retrieve agreement detail outside their allowed divisions.

The React application previously declared a different numeric fulfilment scale
and guessed the source scale from each loaded result set. This mislabels results
that do not contain code `0`. Agreement detail also authenticated callers
without checking the agreement division.

## 2. Scope and decisions

### In scope

- Replace the zero-detection heuristic with one typed frontend mapper.
- Preserve the existing raw API codes:

  | Raw code | API/database meaning | Agreement UI treatment |
  | --- | --- | --- |
  | `0` | Unfulfilled | Unfulfilled |
  | `1` | Partially fulfilled | Partially fulfilled |
  | `2` | Overfulfilled | Unsupported; render neutral `Unknown` |
  | `3` | Fully fulfilled | Fully fulfilled |

- Do not expose code `2` as an Agreement filter, badge, icon state, timeline
  legend/bar, translation, or saved-view filter. It remains in the API type for
  compatibility and is deliberately not relabelled as fully fulfilled.
- Enforce case-insensitive division access for `GET /api/agreements/{headerId}`
  and its `/api/orders/{headerId}` alias. Return `404` for missing or
  inaccessible agreements before reading lines.
- Normal users with no allowed divisions receive no Agreement list/detail data;
  super-admins remain unrestricted.
- Preserve saved-view filters `0`, `1`, and `3`; clear unsupported `2` and
  obsolete `4` on decode.

### Out of scope

- Changing fulfilment calculations or database values. The original
  status/access slice did not otherwise reshape the API; the later,
  explicitly scoped scalar response-boundary refinement is recorded below.
- A general authorisation review of reservation, activation, or bulk-action
  routes.
- Introducing a user-facing `Finished` Agreement status.

## 3. Implementation plan

1. Keep `ApiFulfilmentStatus` aligned with the raw API and map only codes `0`,
   `1`, and `3` to visible status kinds. Map `2` and unknown values to neutral
   `unknown`.
2. Use this mapper in Agreements list/table/timeline and Agreement detail;
   remove all per-page scale detection and incompatible `Finished` handling.
3. Restrict Agreement filters and decoded saved views to `0`, `1`, and `3`.
4. Centralise Agreement division access in the WebApp and apply it to both the
   list and detail route.
5. Add frontend mapper/saved-view regression tests and controller tests for
   unauthenticated, missing, same-division, cross-division, no-division, and
   super-admin detail requests.
6. Keep the API contract aligned with this final treatment.

### Completed scalar response-boundary refinement

A later behaviour-preserving maintainability slice replaced direct EF entity
serialization for Agreement detail with explicit response DTOs. The top-level
`{ header, lines }` shape remains unchanged. `header` preserves its 36
scalar/computed fields and every line preserves its 43 scalar/computed fields,
with the established values, types, nulls, camel-case names, and property order.

The response now deliberately omits six EF navigation graph properties:
`header.currentChangeOrder`, `header.changeOrderHeaders`,
`header.changeOrders`, `header.lines`, `line.header`, and
`line.changeOrderLines`. The top-level `lines` collection remains present. This
refinement prevents persistence graph expansion; it does not change fulfilment
status mapping, division access, frontend routes, or frontend navigation.

## 4. Acceptance criteria

- [x] A response containing only codes `1`, `2`, and `3` renders as Partially
  fulfilled, Unknown, and Fully fulfilled respectively.
- [x] Visible status filters send only `0`, `1`, or `3`; no visible filter sends
  `status=2`.
- [x] A saved view with `2` or `4` status filtering is safely cleared.
- [x] No Agreement screen changes mapping based on result-set contents.
- [x] An unauthenticated detail request returns `401`; missing or inaccessible
  detail returns `404`; a matching division or super-admin can retrieve detail.

## 5. Verification

Completed automated evidence includes:

- Frontend mapper, Agreement list/table/timeline/detail, query, and saved-view
  tests covering stable `0`/`1`/`2`/`3` treatment and supported filter values.
- `AgreementsControllerTests` covering authentication, division matching,
  missing/inaccessible resources, no-division users, super-admin access, access
  before line reads, the concrete detail DTOs, and exact Web JSON for all 36
  Header plus 43 Line fields and the six omitted navigation properties.
- The service-free filtered `OF.Tests.WebApp` suite, including production-shaped
  host routing, passed 324 tests at the 2026-08-14 documentation tranche.

Manual release checks remain the three visible filters, an unsupported code-`2`
response, saved-view migration, and cross-division/super-admin direct links.
