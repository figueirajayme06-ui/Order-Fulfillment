# Agreement bulk unfulfilment

## Objective and scope

Planners can reset all local fulfilment for an agreement through one confirmed header action. The operation preserves every line, clears every reservation including reservations on deleted lines, and recalculates persisted line/header fulfilment. Filters and selection never limit its scope. This implements UF-01 in the approved UI parity roadmap.

External activation cancellation, changes to database schemas, and clearing upstream confirmed commitments are excluded. The user authorised roadmap implementation on 8 September 2026, including this necessary additive mutation contract.

## Workflow and states

The header action identifies the agreement and confirms the complete line/reservation scope. Cancellation makes no request. Submission disables duplicate and conflicting actions. Success refreshes detail, reservations, selected-line availability and action eligibility; timelines fetch current state when revisited. Failures retain the page and show the server conflict/error. The control and confirmation use existing semantic, localised UI patterns and are hidden in print.

Only a writable, non-deleted header with activation status TODO (0) and no activation instance is eligible. A/T/Q identifiers are allowed subject to those persisted rules. Failed (1), Requested (2), Activated (3), unknown activation states, and any line with an activation state other than TODO or an activation instance are rejected. Any confirmed reservation or actual asset/item/quantity data rejects the entire reset. Reset never implicitly cancels external work. The server is authoritative when the frontend has incomplete context.

## API and persistence

`POST /api/agreements/{headerId}/unfulfil` takes no selection or body. It returns `{ linesReset, reservationsRemoved, headerStatus }`; linesReset counts persisted line states actually changed, including deleted lines. Missing identity returns 401, ReadOnly returns 403, missing/inaccessible headers return 404, and changed division or ineligible operational state is rechecked in the operation. Conflicts return 409 problem JSON with a stable code and explanatory detail/message. Database concurrency conflicts return 409; other persistence failures propagate as failures, never success.

A dedicated reset repository uses the existing EF context and a serializable transaction, with the shared `OF:AgreementMutation:{headerId}` SQL Server transaction lock. It reloads any previously tracked header, lines and reservations before making eligibility decisions. Reads cover the entire agreement, including deleted lines. Reservation removal, quantity/status updates, actor/date metadata and reservation audit triggers commit together. No existing repository method that saves internally is called.

After removing reservations, QuantityFulfilled is zero. A line requiring fulfilment with positive quantity becomes Unfulfilled (0); non-requiring and zero/negative-quantity lines are FullyFulfilled (3), matching CoreFulfilmentEngine. Header recalculation excludes deleted, service and non-requiring lines and prefers non-quote lines when present. An unchanged repeated reset succeeds with zero counts and does not rewrite audit timestamps.

## Acceptance and verification

- Entire agreement reset across serialized, quantity, depot, rehire and deleted-line reservations; unrelated agreements unchanged.
- Persisted quantities, domain statuses and audit metadata correct; normal reservations can be created again.
- Identity/division/ReadOnly and all activation/confirmed-state guards exercised directly.
- Repeated reset is a no-op; stale tracked objects are refreshed before eligibility decisions.
- SQL persistence tests cover rollback on write failure and concurrent reservation updates before claiming transactional consistency. In-memory tests alone do not establish these guarantees.
- Integrator verifies confirmation/cancel, busy/error/success, full-scope counts and refresh through focused frontend tests and build, plus desktop review.

## Boundaries

Always reuse current domain rules, shared controls, localisation and authorization conventions. Ask before additional dependencies, schema or deployment changes. Never edit generated/vendor files, commit local runtime state or remove a failing test to pass verification.

## Implementation verification (8 September 2026)

The coordinated backend suite passed 142 tests without failures or skips. Reset coverage includes API identity/division/ReadOnly/conflict responses; complete-scope removal, deleted lines, domain recalculation, stale cached division and repeat behavior; and three real LocalDB persistence checks. Those SQL checks verified reservation deletion/status/audit commit, rollback under an injected database trigger failure, and waiting for a concurrent external confirmation before rejecting the reset. Frontend confirmation, refresh and visual verification are integrated with the other parity actions by the roadmap integrator.