# OF UI parity roadmap

Status: planned. Repository inspected 8 September 2026; no feature implementation included.

## Objective

Restore six capabilities in the React OF UI with bounded work packages that agents can deliver in parallel:

1. Display reservation dates in timeline views, matching the original UI.
2. Disable Activate until the order is fulfilled.
3. Link to the existing order summary sheet.
4. Unfulfil all lines and remove all reservations with one agreement-level action.
5. Stack reservations in timeline views, matching the original UI.
6. Allow line deletion from T agreements only.

Use `src/OF.Frontend` and the React-facing `src/OF.WebApp`. The legacy `src/OF.UI` is reference material. Rebuilding the summary report, replacing the timeline library, database migrations and CI/deployment changes are outside the initial scope.

Read [the documentation hub](../README.md), [frontend instructions](../../src/OF.Frontend/AGENTS.md), [UX guide](../frontend/UX-DESIGN-GUIDE.md), [API contract](../api/WEB-API-CONTRACT.md) and [verification guide](../frontend/TESTING.md). Cross-page, new-workflow and API work needs a focused specification using [the feature template](FEATURE-SPEC-TEMPLATE.md) before implementation.

## Repository findings

Frontend paths below are relative to `src/OF.Frontend/src`; other paths are repository-relative.

| Area | Observed implementation | Planning implication |
| --- | --- | --- |
| Agreement detail timeline | `pages/timeline/TimelinePage.tsx` renders line tasks followed by reservation tasks. Reservation bars use the parent line's valid-from/to dates. | Verify original date precedence and group reservations with their owning line. |
| Asset timeline | `pages/assets/assetsTimelineModel.ts` creates one row per event; `AssetsTimeline.tsx` aligns context rows with Gantt tasks. | Stacking affects event geometry and context-row alignment. |
| Shared timeline | `components/timeline/FrappeGantt.tsx` has no grouping/lane contract and is shared by Agreement-list, Asset and detail timelines. | Give renderer changes one owner and check all consumers. |
| Reservation dates | `src/OF.WebApp/Controllers/ReservationsController.cs` returns no reservation start/end fields; Asset events have dates. | Trace authoritative dates before proposing API additions. |
| Activate | `pages/agreements/AgreementDetailPage.tsx` disables Activate for raw activation status 3, without a fulfilment gate. | Add fulfilment eligibility and in-flight protection while preserving permission/state constraints. |
| Legacy activation | `src/OF.UI/ViewModels/Fulfilment/FulfilmentViewModel.cs` permits a fulfilled header OR fully fulfilled non-quote fulfilment lines, together with `IsActivatable`. It also accepts fulfilment code 2. | Resolve the exception explicitly. The current API contract treats only 3 as fully fulfilled and 2 as unknown. |
| Summary sheet | Legacy `OrderSummarySheetController.cs` serves `/report/{agreementNumber}/display`, optionally with `quoteId`. Legacy visibility is fulfilled A agreements. Current `/api/app-config` exposes validated `legacyFrontendUrl`. | Reuse the report and configuration; verify the destination host rather than assuming the SPA serves it. |
| Bulk reset | Current bulk APIs provide depot fulfilment and rehire; individual reservation deletion recalculates fulfilment. | Define an agreement-level reset contract and persistence operation. |
| Legacy line deletion | `FulfilmentEngineController.DeleteLine` rejects deleting the last line. `src/OF.UI.Shared/Database/DataRepository.cs` removes reservations and soft-deletes the line. | Preserve these behaviors and enforce T eligibility server-side. |
| Current equipment removal | `AgreementEquipmentController.cs` removes pending local equipment on eligible T or A headers and rejects lines with reservations. | General T-line deletion is a different operation. Resolve whether T-only must also restrict the existing A-equipment action. |

Legacy timeline date presentation and stacking semantics have not yet been verified against a running original UI. That comparison is an explicit discovery deliverable.

## Agent ownership and parallel lanes

Keep dates and stacking together because they change the same timeline model. Keep activation and the summary link together because they change the same header actions.

| Lane / owner | Tasks in order | Owned implementation surface | Prerequisite | Relative effort |
| --- | --- | --- | --- | --- |
| A: Timeline agent | TL-01 dates, then TL-02 stacking | `pages/timeline/*`, `pages/assets/AssetsTimeline*`, `pages/assets/assetsTimelineModel*`, `components/timeline/*`; Agreement-list timeline where needed | Verified date/stacking rules; renderer interface | Large |
| B: Header-actions agent | HA-01 activation, then HA-02 summary link | Feature-specific eligibility/link helpers and focused tests | Activation exception and summary visibility decisions | Small-medium |
| C: Reset agent | UF-01 bulk unfulfilment | Dedicated reset UI/service and server operation, persistence implementation and tests | Reset eligibility, scope and mutation contract | Medium-large |
| D: Deletion agent | DL-01 T-line deletion | Dedicated deletion UI/service and server operation, persistence implementation and tests | Equipment exception and parent/child policy | Medium |
| Integrator | Shared wiring, incremental integration and combined verification | `AgreementDetailPage.tsx`, its CSS/tests, shared services/types, locale JSON, API contract and documentation hub | Feature handoffs | Medium |

During parallel implementation the integrator is the only editor of shared files. Agents supply narrow integration patches/instructions and translation additions. C and D must also coordinate changes to `IDataRepository.cs` and `DataRepository.cs`; either the integrator applies them or explicitly transfers ownership for a sequential edit. Do not add a generic action framework merely to split this work.

## TL-01: Reservation dates

Outcome: a planner can identify each reservation's planning period from the timeline.

Starting points: `TimelinePage.tsx`, `assetsTimelineModel.ts`, `AssetsTimeline.tsx`, `FrappeGantt.tsx`, `ReservationsController.cs` and `src/OF.WebApp/Features/Events/AssetTimelineFallbackSynthesizer.cs`.

- Compare representative original-UI reservations with current data. Record the date fields driving bar geometry and displayed labels, including delivery/termination versus valid-from/to precedence.
- Inventory `/agreements/:headerId/timeline`, Asset timeline and Agreement-list timeline. Apply reservation behavior where reservations are actually displayed; preserve aggregate agreement bars on the list.
- Specify date-only handling, missing/open-ended dates, inclusive endpoints and formatting. Keep real dates available in metadata when bars are clipped to the visible period.
- Reuse current data where sufficient. Specify and obtain repository-required approval for concrete API additions before implementing them.

Acceptance:

- [ ] Geometry and visible/discoverable dates match verified original behavior, including depot fulfilment and rehire where applicable.
- [ ] Dates are discoverable by keyboard as well as pointer, including on narrow bars.
- [ ] Missing dates use an explicit fallback; fabricated dates are not presented as authoritative.
- [ ] Same-day, open-ended, clipped and timezone-boundary cases have focused coverage.
- [ ] Period navigation and print remain usable.

## TL-02: Reservation stacking

Outcome: multiple reservations remain identifiable and selectable within their owning line/asset group.

- First capture original grouping and overlap behavior. Proposed default pending verification: group by line on detail timelines and by asset on Asset timelines; overlaps occupy separate vertical lanes, while non-overlapping events may reuse a lane.
- Define stable event identity, deterministic order, interval-boundary rules, group height and context-row alignment in the timeline spec.
- Implement layout in application-owned code. If the wrapper cannot support it, document the smallest alternative; do not edit vendor output or add a replacement dependency without approval.
- Preserve TL-01 date presentation and check all shared-renderer consumers.

Acceptance:

- [ ] Overlapping, nested, identical-period and adjacent reservations follow the recorded rule without hiding events.
- [ ] Context rows stay aligned during scroll and resize; hover, click and keyboard focus identify the correct reservation.
- [ ] Empty assets remain visible without placeholder event bars.
- [ ] Filtering, sorting, navigation, saved views and print remain coherent.
- [ ] Representative hundreds-of-lines and high-reservation-count data remains usable; observed limits are documented.

## HA-01: Fulfilment-gated activation

Outcome: Activate remains disabled until the order meets the agreed fulfilment rule.

Starting points: `AgreementDetailPage.tsx`, `types/fulfilmentStatus.ts`, `services/activationService.ts`, `src/OF.WebApp/Controllers/ActivationController.cs`, legacy `FulfilmentViewModel.cs` and their tests.

- Recommended initial rule from the request: require header `ApiFulfilmentStatus.FullyFulfilled` (3). Resolve whether to retain the legacy non-quote-line exception before implementation. Never reinterpret raw code 2 as fulfilled.
- Retain permission and activation-state constraints, prevent duplicate submissions and provide a localised explanation for disabled activation.
- Re-evaluate after reserve/remove, reset and line deletion. Do not derive readiness from filtered or selected lines.
- Current API documentation explicitly says activation does not enforce fulfilment eligibility at controller level. UI gating is the requested scope; propose server eligibility changes separately with retry/cancellation implications and contract approval.

Acceptance:

- [ ] Unfulfilled, partial and unknown states disable Activate; a fully fulfilled otherwise eligible agreement enables it.
- [ ] Loading, read-only, already activated and in-flight states cannot submit activation.
- [ ] The non-quote exception is documented and tested according to the recorded decision.
- [ ] Removing fulfilment updates the action after refresh; failures preserve existing retry behavior.

## HA-02: Order summary sheet link

Outcome: agreement detail opens the existing summary sheet for the correct order.

Starting points: `services/appConfigurationService.ts`, current app-configuration consumers, legacy `OrderSummarySheetController.cs`, `FulfilmentViewModel.DisplayOrderSummary` and `src/OF.UI/Views/Fulfilment/Index.cshtml`.

- Reuse `legacyFrontendUrl` and `/report/{agreementNumber}/display`; verify URL joining and configured host/path behavior. Preserve any required `quoteId` context.
- Proposed visibility: fulfilled A agreements, matching the legacy view, using current fulfilment code 3. Record any intended expansion separately.
- Use a semantic localised link. Missing configuration must produce an understandable unavailable state instead of a broken URL.

Acceptance:

- [ ] An eligible order opens its existing report with correctly encoded agreement and required quote context.
- [ ] Missing configuration/identifier and ineligible Q/T/unfulfilled orders follow the agreed visibility rule.
- [ ] Authentication and report rendering are checked in a representative environment; the destination is not a SPA fallback page.
- [ ] No hard-coded environment URL or duplicate report implementation is introduced.

## UF-01: Unfulfil all lines and remove all reservations

Outcome: one confirmed action resets the whole agreement independently of line filters and selection.

Starting points: `src/OF.WebApp/Controllers/BulkActionsController.cs`, `ReservationsController.cs`, `src/OF.UI.Shared/Database/IDataRepository.cs`, `DataRepository.cs`, `src/OF.Common/Infrastructure/OF/CoreFulfilmentEngine.cs`, current bulk fulfilment UI and [bulk fulfilment spec](agreement-bulk-fulfilment.md).

- Write a focused reset spec defining allowed agreement/activation states and confirmed-reservation handling. Resetting fulfilment must not implicitly cancel external activation.
- Propose one header-scoped mutation that rechecks division access, ReadOnly denial, current state and complete target scope server-side. Do not issue one browser delete per visible reservation.
- Cover serialized, nonserialized, depot and rehire reservations; explicitly define treatment of reservations attached to soft-deleted lines. Reset/recalculate persisted quantities and statuses, then the header, using domain rules.
- Prefer an atomic transaction including recalculation and audit, clear conflict/failure responses and safe repeat behavior. Inspect repository methods that save internally before claiming transactional consistency.
- Confirmation identifies the agreement, full scope and counts. Preserve the lines. Refresh detail, reservations, availability, action eligibility and timelines when revisited.

Acceptance:

- [ ] Cancelling changes nothing; confirming removes every in-scope reservation, including those hidden by filters.
- [ ] Fulfilment-requiring lines have agreed reset quantities/statuses and the header is recalculated; other lines retain domain-correct behavior.
- [ ] Repeating the action on an already-reset agreement behaves predictably.
- [ ] Read-only, inaccessible agreements, stale state, concurrent changes and persistence failures are covered at API/persistence level.
- [ ] Failure never claims complete success or leaves an undocumented partial reset.
- [ ] Normal fulfilment can be performed again after reset.

## DL-01: T-agreement line deletion

Outcome: eligible lines can be deleted from T agreements only, with reservation cleanup and consistent totals.

Starting points: legacy `FulfilmentEngineController.DeleteLine`, `DataRepository.DeleteLine`, current `AgreementEquipmentController.cs`, `AgreementEquipmentRepository.cs`, `AgreementDetailPage.tsx` and [equipment spec](add-operational-equipment.md).

- Define general line deletion separately from pending-equipment removal. Resolve whether the existing A-agreement equipment exception must be removed to satisfy T-only behavior.
- Enforce persisted parent agreement type, division access, ReadOnly denial, allowed activation state and line ownership server-side. A submitted line-number prefix is not authoritative.
- Preserve last-line protection, soft deletion and audit. Clean up target reservations and recalculate fulfilment consistently.
- Specify root/subline handling: reject parents with children with a clear reason, or define a confirmed cascade. Count remaining live lines correctly and protect against concurrent last-line deletion.
- Confirm deletion and reservation impact; refresh the line list, reservation state, availability selection and header actions. Restore focus sensibly.

Acceptance:

- [ ] Eligible T lines can be deleted; A, Q, unknown types and ineligible states are rejected by the general deletion API even when called directly.
- [ ] Existing equipment removal follows the explicitly agreed T-only/exception policy.
- [ ] The last live line cannot be deleted; parent/subline behavior matches the spec.
- [ ] Reservations are cleaned up, the line is soft-deleted with audit metadata and header fulfilment is recalculated.
- [ ] Cancel, failure, foreign IDs, read-only access, repeated deletion and concurrent state changes have meaningful coverage.
- [ ] Deleting the selected line leaves no stale availability or reservation controls.

## Delivery waves

1. **Wave 0: resolve contracts.** Each lane does read-only discovery and writes its scoped spec/design note. The integrator records date/stacking behavior, activation exception, report visibility, reset state matrix and deletion policy. Obtain approval for concrete API/schema/dependency changes where repository instructions require it; independent preparation continues.
2. **Wave 1: parallel implementation.** A/B/C/D can run concurrently with sufficient capacity in isolated branches/worktrees. With four total agent slots including the integrator, start A/B/C, then start D when B completes. The integrator owns shared wiring throughout.
3. **Wave 2: integrate incrementally.** Integrate HA-01/HA-02 first when ready, then reset/deletion as they pass. Within A, dates precede stacking. Timeline delivery does not wait for mutations, and mutations do not wait for timelines.
4. **Wave 3: combined verification.** Test reserve -> fulfilled -> Activate enabled -> reset -> Activate disabled -> refulfil, plus deletion recalculation, report navigation and timeline refresh on eligible test data. Execute activation only in an appropriate test environment.

## Agent handoff prompt

> Implement lane **[A/B/C/D]**, tasks **[IDs]**, from `docs/specs/of-ui-parity-roadmap.md`. Read its relevant repository guidance and references. Verify unresolved business rules and write the required focused spec/design note first. Work in the agreed isolated branch/worktree and owned files. Coordinate API/shared-file changes with the integrator; supply narrow integration patches and locale additions. Preserve unrelated changes and authorization rules. Return changed files, acceptance results, focused test/build evidence, visual-check evidence and unresolved limitations. Do not implement other lanes or change the legacy UI.

Handoffs must state request/response or component interface, busy/success/failure behavior, translation keys for every supported locale and the required detail/reservation/availability refresh. Classify new mutation routes in the existing authorization inventory tests.

## Verification and completion

- Run relevant focused frontend tests and `npm run build` from `src/OF.Frontend`. Shared component/service changes also run `npm test`; broad integration runs `npm run lint` and `npm run format` as documented.
- Backend work runs relevant `src/OF.Tests` controller/domain/persistence suites and authorization inventory coverage. Prove rollback/concurrency claims with persistence tests rather than controller mocks alone.
- Review localised copy, loading/disabled/error states, confirmation, keyboard/focus and a normal desktop workflow. Timeline work also needs navigation, context resizing, legend, saved-view compatibility, no-event rows and print checks.
- Use representative large agreements and high reservation quantities. Distinguish line count, reservation count and units.
- The testing guide records legacy-route problems in Playwright. Do not claim that suite proves these workflows without updating applicable routes/fixtures; report environment-dependent verification honestly.
- Done means all six task checklists pass, decisions/specs and API documentation reflect the delivered behavior, and any outstanding environment checks are explicitly recorded. This roadmap itself changes documentation only.
