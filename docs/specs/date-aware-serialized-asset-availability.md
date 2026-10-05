# Feature specification: date-aware serialized asset availability

## 1. Objective

**User and job:** A fulfilment planner needs to know whether a serialized asset can cover the selected agreement
line's hire period, including assets that are currently on hire but will be released before that period begins.

**Problem:** The availability summary already excludes overlapping reservations and Ringfences by date, but it also
requires the asset's current imported status to be `Available`. An asset that is still reported as `OnHire` is
therefore unavailable for every future period even when its recorded agreement release date is before the requested
period.

**Outcome:** A dated `OnHire` asset is counted as available after its current agreement releases it, while genuinely
overlapping, undated, operationally unavailable, and out-of-fleet assets remain unavailable.

## 2. Scope and boundaries

### In scope

- Make the NOF serialized availability count use the requested inclusive period and the current asset agreement's
  known release date.
- Prefer `CollectionDate`, then `TerminationDate`, then `AgreementLineValidToDate` as the release date.
- Preserve reservation and Ringfence overlap checks and quantity-stock behaviour.

### Out of scope

- Predict readiness for `Collection`, `Repair`, `Service`, `Assess`, or `InTransit` assets.
- Add turnaround or transport buffer days after an agreement release.
- Change the selectable repair-asset disclosure or reservation clash workflow.
- Change the CPQ Next availability procedure.

### Constraints and known rules

- Booking dates are inclusive. An asset released on a date is not available for another booking on that same date.
- An `OnHire` asset without a reliable release date remains unavailable.
- `OnHire` without an agreement number represents on-hold/unknown context and remains unavailable.
- `src/OF.Data.Design` remains the sole deployable source for `dbo.GetFulfilmentAvailabilitySummary`.

## 3. User experience

1. The planner opens availability for an agreement line.
2. The existing summary uses that line's requested period.
3. An asset currently `OnHire` contributes to the available count only when its known release date is earlier than
   the requested start date and it has no overlapping reservation or Ringfence.

There is no layout, copy, responsive, accessibility, or print change. The raw current asset status and existing broad
repair-asset candidate disclosure are unchanged.

## 4. Technical plan

- Affected data contract: the calculation behind `GET /api/availability/summary`; its request and response shapes do
  not change.
- Update `dbo.GetFulfilmentAvailabilitySummary` only. Do not update the CPQ compatibility procedure.
- Add SQL integration coverage for a disjoint future period, inclusive boundary, release-date precedence, missing
  release dates, and non-hire operational states.
- Update the Web API availability caveat with the serialized rule.

## 5. Acceptance criteria

- [x] An `OnHire` asset released on 1 February is available for a requested period beginning 2 February.
- [x] The same asset is unavailable for a requested period that includes 1 February.
- [x] Collection, termination, and valid-to dates use the documented precedence.
- [x] Missing release dates and non-`OnHire` unavailable states do not overstate availability.
- [x] Overlapping reservations and Ringfences still make the asset unavailable.
- [x] Quantity-stock and CPQ Next availability calculations are unchanged.

## 6. Verification

- Focused test: `FulfilmentAvailabilitySummaryIntegrationTests`.
- Build: use Visual Studio MSBuild with SSDT for `src/OF.Data.Design/OF.Data.Design.sqlproj`, and `dotnet test` for the
  relevant .NET test project.
- Data check: deploy the updated `OF.Data.Design` DACPAC before relying on the new WebApp behaviour.
