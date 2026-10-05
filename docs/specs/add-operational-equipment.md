# Feature specification: add operational equipment

> **Status:** Implemented in the working tree; operational and visual release validation pending. Business job confirmed 26 August 2026.

## 1. Objective

**User and job:** A fleet planner needs to add operational equipment that is missing from a T or A agreement before that new demand has been activated in M3.

**Problem:** The React agreement detail page can fulfil existing demand but cannot create missing demand. The legacy `OF.UI` action called this a sub-line, but that name obscures the result: it creates an additional, independently fulfilled equipment line rather than splitting the selected line.

**Outcome:** From an eligible root line, an authorised planner can select a generic or an exact item, enter a quantity, and add a pending equipment line. The page then reloads the line into the normal reservation, availability, fulfilment, and activation workflow.

## 2. Scope and boundaries

### In scope

- A row-scoped `Add equipment` action on an eligible root agreement line.
- Selection of product line, generic, optional attributes, optional exact item, and a whole-number quantity from 1 to 1,000.
- One primary `Add equipment` action for both generic-only and exact-item demand.
- Creation of an independent, unfulfilled line with a temporary dotted agreement-line number below the selected source line.
- Inheritance of the source line's operational and logistics context on the server.
- Refresh of agreement and reservation data after creation so the new line uses the existing fulfilment workflow.
- Safe removal of the pending line before its activation, after the user has removed any reservations.
- Agreement-scoped, division-authorised catalogue, create, and remove API operations.

### Out of scope

- Salesforce records whose agreement number begins with `Q`, even if users refer to the wider workflow as adding equipment to an agreement or quote.
- Splitting, reducing, replacing, or otherwise editing demand on the source line.
- Editing or removing upstream or M3-confirmed lines.
- Adding equipment while activation is requested or failed, where downstream state may be uncertain.
- Bulk addition, copying one addition to several lines, templates, kits, and package configuration.
- Price, rate, discount, approval, or commercial quote amendment authoring.
- Change Orders and changes to the legacy `OF.UI` workflow.
- A permanent parent/child hierarchy, database schema change solely for lineage, or preserving the source relationship after M3 assigns a real line number.

### Constraints and known rules

- The API is the authorisation and eligibility boundary. Hiding or disabling a frontend action is guidance only.
- An eligible header is non-deleted and in one of two stable states:
  - a `T`-prefixed header with `ActivationStatus.TODO`; or
  - an `A`-prefixed header with `ActivationStatus.Activated`.
- `A`-prefixed headers are already activated according to `Header.IsActivated`. For an A agreement, it is the newly added equipment line that is local and unactivated, not the header. Headers or source lines in `Requested` or `Failed` state are not editable in this MVP.
- An eligible source is a non-deleted, non-dotted root line on that header, requires fulfilment, contains the logistics fields needed by activation, and is in the corresponding stable state: `TODO` for T or `Activated` for A.
- The server must enforce the caller's access to the header division before returning catalogue data or reading/mutating a line. Missing and inaccessible headers or lines return `404`, consistent with the current agreement boundary.
- Product data is inferred from the header division. The server must validate that the generic is active for that division; that every submitted attribute belongs to the generic; and, when supplied, that the exact item is active for the division, belongs to the generic, and matches the selected attributes.
- Product line is a catalogue navigation choice, not trusted mutation data. `genericId` determines it on the server.
- Quantity must be an integer from 1 through 1,000. Client validation supplements but does not replace server validation.
- The new line adds demand; it does not consume or reduce the source line quantity. It starts with `QuantityFulfilled = 0`, unfulfilled status, `RequiresFulfilment = true`, and `ActivationStatus.TODO`.
- The server copies the source line's dates, division, warehouse, facility, agreement line type/status, package group, shifts, rate type, order references/indexes, source identifiers, and change sequence required by the existing activation path. The client cannot override inherited values.
- Temporary numbering uses `<source agreement line number>.<next integer>`. Allocation and insertion must be atomic, must consider existing or previously removed dotted numbers, and must not create duplicate suffixes under concurrent requests.
- `parentLineId` identifies the source for validation, inheritance, and temporary numbering only. The MVP does not store or promise a durable parent relationship. M3 acknowledgement may replace the dotted number with a real line number, after which the line is an ordinary independent line.
- Removal is local-only and requires the target to belong to the route header, remain dotted, non-deleted, and `ActivationStatus.TODO`, and have no reservations. The API must reject removal rather than cascade-delete reservations or contact M3.
- Mutations recalculate line/header fulfilment status and record the established last-updated audit identity.
- New visible copy is localised, controls use established shared components and tokens, and interactive-only controls are excluded from print.

## 3. User experience

### Primary flow

1. On an eligible root row, choose `Add equipment`.
2. A focused dialog identifies the source line and shows the inherited warehouse and hire period so the user understands where the new demand will apply.
3. Select a product line and then a generic. Changing either parent choice clears incompatible downstream choices.
4. Optionally choose attributes to narrow the available exact items, then optionally select an exact item. Generic-only demand remains valid when no exact item is selected.
5. Enter a whole-number quantity from 1 to 1,000.
6. Choose the single `Add equipment` action. The action is disabled during submission to prevent a duplicate concurrent request.
7. On success, close the dialog, reload agreement and reservation data, and announce the newly created pending line. It appears with the normal line controls and can be fulfilled immediately.

### Removal flow

1. A local dotted line in `TODO` state exposes `Remove equipment`; normal root and activated lines do not.
2. If it has reservations, the action explains that the user must remove them through the existing reservation management flow first.
3. With no reservations, the user confirms removal in a destructive-action dialog.
4. On success, refresh agreement and reservation data, remove the line from the active grid, and announce the result.

### Information hierarchy

- Keep the source line number, inherited warehouse, and hire period visible at the top of the add dialog.
- Keep product line, generic, and quantity as the primary form fields. Reveal attribute choices and the filtered exact-item choice after a generic is selected.
- State plainly that the operation adds demand and does not split the source quantity.
- While the number remains dotted, place the line after its source in line-number order and show a textual `Pending new equipment` status. Do not imply that this grouping will survive activation.
- Keep reservation, availability, and line-fulfilment information in their existing locations after the reload.

### States and edge cases

- Loading: opening the dialog loads the agreement-scoped catalogue; disable dependent controls and show a labelled loading state without blocking the underlying page.
- Empty: if no product lines or generics are eligible for the agreement division, explain that no equipment is available to add and disable submission. An empty exact-item result does not prevent valid generic-only demand.
- Validation: show field-specific messages for missing product line/generic, stale attribute or item choices, and quantity outside 1–1,000.
- Error: retain valid form input after a failed request and show the server's actionable validation/conflict outcome. Do not convert `400`, `404`, `409`, or `503` into an empty successful result.
- Stale eligibility: if activation or another mutation makes the header or line ineligible, the API returns `409 Conflict`; close or disable the form after refreshing the agreement and explain that equipment can no longer be added or removed in the current state.
- Success/confirmation: announce creation/removal in a live region, refresh rather than cloning a row locally, and restore focus to a sensible row action or the Lines heading.
- Duplicate submission: disable the primary action while the POST is in flight. The client does not automatically retry a mutation whose outcome is unknown.
- Disabled/no-permission: do not render add/remove actions when detail data shows that the record is ineligible. Direct API requests remain protected by the same server rules.

### Accessibility and responsive behaviour

- Use native labelled selects, checkbox/radio controls as appropriate for attributes, and a numeric input with an accessible description of the 1–1,000 range.
- Give every row action an accessible name containing its line number, for example `Add equipment from line T123-1`.
- Use an accessible modal pattern with a programmatic title, logical focus order, `Escape` cancellation, visible focus, error association, and focus restoration. Confirmation must not rely on colour alone.
- Announce catalogue loading, submission, validation, success, and error states without moving focus unexpectedly.
- Preserve the existing horizontally scrolling table on narrow layouts; make dialog content scroll within the viewport without hiding its actions.
- Hide add/remove controls and open dialog content in print. Print the added line and its textual pending status as operational demand; do not depend on indentation or colour to identify it.
- Put all labels, help, validation, confirmation, status, and result copy in every supported `react-i18next` locale.

## 4. Technical plan

### Affected areas and existing patterns

- Frontend route/page: `/agreements/:headerId`, `AgreementDetailPage`, its CSS module, agreement types, and `agreementsService`.
- Web API: a focused agreement-equipment controller/feature in `OF.WebApp`, using `AgreementDivisionAccess` and the established agreement `404` boundary.
- Domain/data: existing product line, generic, item, attribute, line creation, fulfilment recalculation, and audit facilities. Reuse `ICoreFulfilmentEngine` where appropriate rather than the legacy controller action.
- Presentation: shared `Button`, `Alert`, `Spinner`, form/modal patterns, theme tokens, print data attributes, and `react-i18next`.
- Agreement detail already returns `isSubline`; add it to the React `AgreementLine` type and use it as presentation data, while retaining server-side checks for every mutation.

### Proposed API contract

All routes require a resolved identity. Division comes from the authorised header; none of these requests accepts a caller-supplied division.

| Method and route | Purpose and minimum response |
| --- | --- |
| `GET /api/agreements/{headerId}/equipment/catalog` | Return active product lines and generics available to the header division, for example `productLines: [{ id, name, familyName }]` and `generics: [{ id, productLineId, code, description }]`. |
| `GET /api/agreements/{headerId}/equipment/catalog/generics/{genericId}?attributes=...` | Return the selected generic, grouped allowed attribute values, and exact items matching the selected attributes in the header division. Accept zero or more selections as repeated `attributes=Name%3AValue` query values or one semicolon-delimited value. |
| `POST /api/agreements/{headerId}/equipment` | Validate and create one pending equipment line atomically. Return `201 Created` with the created agreement-line detail. |
| `DELETE /api/agreements/{headerId}/equipment/{lineId}` | Remove one eligible local pending line and return `204 No Content`. It does not remove reservations or send an M3 delete. |

The POST body is deliberately small; inherited and derived values are not accepted from the client:

```json
{
  "parentLineId": 123,
  "genericId": 456,
  "itemNumber": "OPTIONAL-EXACT-ITEM",
  "attributes": ["Telemetry:Yes", "Voltage:240V"],
  "quantity": 2
}
```

- Omit `itemNumber` or send `null` for generic-only demand. Send `attributes: []` when no attributes are selected.
- Trim and canonicalise item/attribute values before validation and persistence; reject unknown, duplicate, contradictory, or mismatched selections with `400 Bad Request`.
- Return `404 Not Found` for a missing/inaccessible header, a source/target line outside that header, or a catalogue entity unavailable in the authorised division.
- Return `409 Conflict` when the header, source, or target existed but is no longer in an eligible activation/reservation state.
- Re-evaluate access, state, catalogue membership, reservation count, and line ownership inside each mutation; do not rely on an earlier GET.

### Proposed tasks

1. Add a shared eligibility validator and agreement-scoped catalogue queries, with controller tests for access, T/A state, Q exclusion, and division filtering.
2. Add an atomic create operation that validates the product selection, allocates a never-reused dotted suffix, copies the approved source fields, creates independent demand, recalculates status, and returns the mapped line.
3. Add the constrained local removal operation with confirmation-friendly conflict responses, no-reservation enforcement, status recalculation, and tests proving no downstream delete occurs.
4. Add frontend types and service functions for the four endpoints, including repeated attribute query encoding and meaningful error propagation.
5. Add the row actions and accessible add/remove dialogs, reload behaviour, pending status, localisation, print treatment, and focused component tests.
6. Regression-test reservation, fulfilment, activation, and M3 acknowledgement behaviour for the created line, including the transition from dotted to real line number.

## 5. Acceptance criteria

- [ ] `Add equipment` is available only on eligible, non-deleted root equipment lines on stable T/TODO or A/Activated headers.
- [ ] The action is unavailable for Q headers, dotted source lines, non-fulfillable lines, and Requested, Failed, deleted, or otherwise ineligible records.
- [ ] Direct API requests enforce the same header/line eligibility and agreement division access even when the UI action is absent.
- [ ] The catalogue contains only active product lines, generics, attributes, and items valid for the authorised header division.
- [ ] Changing product line, generic, or attributes clears any now-invalid dependent selection.
- [ ] A user can add generic-only demand or optionally select an exact item matching the chosen generic and attributes.
- [ ] Only an integer quantity from 1 through 1,000 is accepted by both client and server.
- [ ] One primary `Add equipment` action creates exactly one independent line for one submitted request and cannot be double-submitted while in flight.
- [ ] The created line has a unique dotted number beneath the selected source, inherits server-controlled logistics, starts unfulfilled and `TODO`, and does not change the source quantity.
- [ ] Concurrent additions under the same source cannot receive the same dotted number, and removed suffixes are not reused.
- [ ] After creation, the page reloads agreement and reservation data, announces success, and exposes the normal availability, reservation, fulfilment, and activation controls for the new line.
- [ ] The pending line has a textual status and remains understandable without colour or indentation; no durable parent relationship is implied after its dotted number changes.
- [ ] `Remove equipment` is available only for a local dotted line still in `TODO` state.
- [ ] Removal requires confirmation and succeeds only when the line has no reservations; otherwise the user is told to remove reservations first.
- [ ] The remove API rejects root, activated, requested, failed, deleted, foreign-header, inaccessible, and reserved lines without deleting reservations or contacting M3.
- [ ] Add/remove success recalculates fulfilment state and records the current audit identity.
- [ ] Loading, empty, validation, conflict, service-error, success, and duplicate-submission states are covered and usable by keyboard.
- [ ] All new copy is localised, row actions have line-specific accessible names, focus is restored after dialogs, and state is not conveyed by colour alone.
- [ ] Interactive add/remove UI is absent from print while the pending demand and its textual status remain readable.

## 6. Verification

- Focused server tests: proposed `EquipmentControllerTests` and creation/removal service tests covering identity, division access, T/TODO, A/Activated, Q, Requested/Failed, parent/line ownership, catalogue validation, 1/1,000 boundaries, inherited fields, independent quantity, atomic suffix allocation, reservations, audit, status recalculation, and response codes.
- Integration/regression tests: fulfil and activate a created generic and exact-item line; confirm M3 acknowledgement replaces the dotted number without relying on a stored parent; confirm removal never invokes downstream deletion.
- Focused frontend tests: `AgreementDetailPage.test.tsx` and `agreementsService.test.ts` for action eligibility, selection resets, attributes query encoding, quantity validation, submit locking, refresh, accessible dialog/focus behaviour, pending status, removal confirmation, reserved-line guidance, and error/conflict states.
- Commands: `dotnet test src/OF.Tests/OF.Tests.csproj`, then from `src/OF.Frontend` run `npm test`, `npm run build`, `npm run lint`, and `npm run format`.
- Manual visual checks: stable T and A records, excluded Q/Requested/Failed records, generic-only and exact-item additions, quantity boundaries, long catalogue labels, empty/loading/error states, reservation-first removal, keyboard-only operation, visible focus, narrow viewport fallback, and desktop print preview.
- Operational-scale check: verify row actions, dotted ordering, refresh, and scanning on an agreement with hundreds of lines and quantities up to 1,000.
- Data/migration check: no schema migration is expected for the MVP. Validate catalogue results against representative division data and test concurrent suffix allocation against the production database engine.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| Business meaning | This adds missing operational equipment as independent demand; it is not a quantity split. | Confirmed |
| Record types | MVP supports stable T/TODO and A/Activated headers. Literal Q records are excluded. | Confirmed |
| A-header activation nuance | `Header.IsActivated` treats A-prefixed headers as activated. Eligibility therefore concerns a new local `TODO` line on the stable A agreement, not an unactivated A header. | Confirmed from current domain model |
| In-flight/failed activation | Block both Requested and Failed states to avoid mutating records with uncertain downstream state. | Safest MVP decision |
| Removal | Require zero reservations and local dotted `TODO` state; users remove reservations through the existing flow first. | Confirmed |
| Lineage | The dotted prefix is temporary presentation/activation state. `parentLineId` is not a durable relationship after creation or M3 acknowledgement. | Confirmed MVP limitation |
| Commercial effect | Does the existing inherited rate context and downstream M3 behaviour provide sufficient commercial governance, or is a reason/approval required before release? Pricing UI remains out of scope. | Product/Commercial Operations |
| Eligible source types | Is `RequiresFulfilment`, stable activation state, and complete logistics sufficient, or must particular agreement-line types be excluded explicitly? | Product/Integration review |
| Attribute rules | Confirm whether more than one value from the same attribute group is ever valid; otherwise the server and UI will enforce one value per group. | Product Data owner |
| Retry safety | The client will not retry automatically and disables duplicate submission. Confirm whether a durable idempotency key is required for ambiguous network outcomes before implementation. | Architecture review |
