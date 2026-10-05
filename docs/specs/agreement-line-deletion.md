# T-agreement line deletion

## Objective and scope
Fleet planners can delete an eligible line from a temporary (T) agreement, including its reservations, without leaving stale fulfilment. This delivers DL-01 of the OF UI parity roadmap. Adding equipment remains available under its existing T/A rules; all removal actions are T-only.

## Rules and experience
- The persisted header agreement number must start with T, the header and target line must be in TODO (0), and the header/line must be live. Failed/requested/activated/unknown states cannot be deleted. Division access and ReadOnly denial are enforced by the API.
- A line must belong to the supplied header. The last live line cannot be deleted, including when other lines are already soft-deleted. Parents with live children are rejected; delete their children first. Child identity uses the existing agreement-line-number dot hierarchy.
- Confirmation identifies the line and reservation count. Cancel changes nothing. Busy disables duplicate actions. Success clears deleted selection/availability and refreshes the complete detail and reservations; errors retain context.
- Soft deletion stamps UTC date/user, clears the deleted line's fulfilled quantity/status and removes every target reservation (serialized, quantity, depot and rehire). Activation instance IDs and actual-allocation metadata also block deletion. Confirmed reservations conflict because they may represent an external activation that this action cannot cancel.
- Header status follows existing fulfilment rules: live fulfilment lines excluding CPQ services; non-quote lines take precedence; fully fulfilled is code 3 only.
- The old pending-equipment removal API keeps its narrower local-subline/no-reservations rule but now rejects A headers. Equipment creation is unchanged.

## Contract and implementation
`DELETE /api/agreements/{headerId}/lines/{lineId}` has no body. Success: `200 { lineId, removedReservationCount, headerStatus }`. Missing/inaccessible header, foreign or deleted line: 404. State, last-line, children, confirmed-reservation and concurrent-write conflicts: 409 problem JSON with `code` and `message`. ReadOnly: 403; missing identity: 401.

A dedicated repository uses a serializable relational transaction, SQL Server agreement application lock shared with reset/equipment operations, refreshed persisted state, a single save and commit. Reservation deletion, line audit and header recalculation participate in that transaction. No schema, dependency or deployment changes. Existing activation/reservation writers rely on the serializable row/range locks; SQL deadlock/concurrency victims return a retryable conflict without claiming success.

## Acceptance and verification
- Controller coverage: identity/division, T-only state matrix, ownership failure mapping, conflict responses, ReadOnly and mutation inventory.
- Persistence coverage: reservation variants, last-live-line, parent/child, audit and header recalculation, repeated deletion, foreign IDs and refreshed persisted state. Relational rollback/concurrency coverage uses the existing SQL Server test environment where available; in-memory coverage alone does not prove transaction isolation.
- Integrator verifies confirmation cancellation, keyboard/focus restoration, selection cleanup, localised status, reservation refresh, frontend tests/build and desktop workflow.

## Boundaries
Reuse shared components and localisation. No cascade deletion, external activation cancellation, legacy UI changes, database migrations or vendor/dependency changes. User instruction to implement all roadmap tasks authorizes this bounded API addition.

## Verification result (8 September 2026)
- Combined deletion, equipment, reset and ReadOnly backend suites: **142 passed, 0 failed, 0 skipped**, final rerun after all backend adjustments. Command: `dotnet test src/OF.Tests/OF.Tests.csproj --no-restore -p:BaseOutputPath=bin/parity-tests/ --filter "FullyQualifiedName~AgreementLineDeletion|FullyQualifiedName~AgreementLinesController|FullyQualifiedName~AgreementEquipment|FullyQualifiedName~AgreementReset|FullyQualifiedName~ReadOnlyEndpointAttributesTests"`.
- SQL Server LocalDB tests proved that injected failure after SQL writes rolls back line/reservation/header updates, and concurrent requests cannot delete both remaining live lines.
- Deletion service tests: **2 passed**. Final page integration, frontend build and visual review belong to the combined delivery verification.
- Isolated test output avoids DLL locks from the existing local application server; no server process was interrupted.
