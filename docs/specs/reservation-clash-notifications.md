# Feature specification: reservation clash notifications

> **Status:** Planned. Business decisions confirmed 17 August 2026.

## 1. Objective

Fleet planners need to see when the same serialized asset is committed to
overlapping bookings, regardless of whether the overlap originated in NOF, M3,
or a Ringfence.

The current notification flow is user-specific, contains limited free text, and
does not consistently detect clashes from the current React/WebApp workflow or
integration updates.

The outcome is a clear, division-visible warning before an avoidable NOF clash
and an active notification that remains available until the overlap is resolved.

## 2. Scope and confirmed decisions

### In scope

- Detect overlaps for Quotes, T Agreements, A Agreements, and Ringfences.
- Include effective booking and allocation changes received from M3.
- Allow a user to create an overlapping reservation in NOF, but warn them before
  they confirm it.
- Treat booking start and end dates as inclusive. Two bookings sharing an end
  date and start date therefore overlap.
- Make a cross-division clash visible to users in both affected divisions.
- Treat `new` as unseen by the current user. Seen state is per user and must not
  resolve or hide the clash for anyone else.
- Record the alert date in Order Fulfillment when the clash is first detected.
- Automatically remove a notification from the active queue when the underlying
  overlap no longer exists.

### Out of scope

- Change Orders. The feature is to be removed separately under
  [Remove Change Orders](remove-change-orders.md).
- Preventing or silently displacing an existing reservation.
- A generic notification framework for unrelated event types.
- Real-time push technology; the existing polling approach is sufficient for
  the first release.

## 3. User experience

### Creating a reservation

1. NOF checks the selected serialized asset and inclusive booking period.
2. If it overlaps another commitment, NOF displays the asset, conflicting
   booking reference, and overlap period.
3. The user can cancel or continue with the reservation.
4. If they continue, the reservation is saved and the active clash notification
   is created or updated.

### Notification dropdown

- Show a notification indicator only when unseen clashes exist.
- Opening the dropdown marks the successfully displayed items as seen for that
  user, but they remain in the active queue until resolved.
- Show the OF alert date, job or Ringfence reference, asset ID, overlap period,
  and conflicting booking.
- Use the message pattern:
  `Reservation clash: <job number>. Asset <asset ID> is double-booked`.
- Show the conflicting booking as supporting information rather than hiding it
  in the message.
- Link to the asset profile and open a period that includes the overlap. The
  asset profile already provides the schedule and links to affected agreements.
- Display the complete active queue in the dropdown, or provide an explicit
  `View all` action when server-side paging is required. Do not silently
  truncate the list.

## 4. Technical plan

1. Add a shared commitment and clash evaluator in `OF.Common` for Reservations,
   M3 allocations, and Ringfences. Reuse the established asset-schedule concepts
   while defining one canonical inclusive date rule.
2. Persist one structured, idempotent clash occurrence for each asset and
   booking pair. Store both source references, affected divisions, overlap
   dates, `DetectedAtUtc`, and resolution state.
3. Store seen state separately per clash and user. Do not create one clash row
   per user or use seen state as business resolution.
4. Reconcile affected assets after successful reservation, Ringfence, M3 line,
   M3 asset, and Salesforce quote commits. Add a periodic reconciliation as a
   recovery path for missed events or older data.
5. Expose an explicit API response filtered by the caller's allowed divisions.
   `GET` must not change clash lifecycle state; use a separate operation to mark
   displayed notifications as seen.
6. Update the frontend service and dropdown to use structured fields, retain the
   current polling pattern, preserve cached results on refresh failure, localise
   visible copy, and support keyboard closing and focus restoration.
7. Link notifications to the asset profile, with overlap dates supplied as query
   parameters.

## 5. Acceptance criteria

- [ ] NOF warns about an inclusive overlap and still allows the user to continue.
- [ ] Repeating the same operation or integration message does not create a
  duplicate active clash.
- [ ] Effective M3 date or asset-allocation changes create and resolve clashes.
- [ ] Quote, T Agreement, A Agreement, and Ringfence commitments participate in
  the same overlap rules.
- [ ] A cross-division clash is returned to authorised users in both divisions,
  without exposing inaccessible booking details.
- [ ] The notification shows the job or Ringfence reference, OF alert date,
  asset ID, overlap period, and conflicting booking.
- [ ] Opening the dropdown marks notifications seen only for the current user.
- [ ] Seen notifications remain active until their overlap is resolved.
- [ ] Resolved clashes disappear on the next successful refresh.
- [ ] The badge, accessible name, loading, empty, stale-data, and error states are
  accurate.

## 6. Verification

- Evaluator tests for every supported booking pair, inclusive boundaries,
  asset reassignment, date extension, deletion, resolution, and idempotency.
- API tests for both affected divisions, cross-division data protection,
  super-admin access, per-user seen state, and read-only GET behaviour.
- Integration tests for NOF, M3, Salesforce quote, and Ringfence mutation paths.
- Frontend tests for the warning flow, dropdown states, complete-list behaviour,
  polling, keyboard interaction, seen state, and asset-profile navigation.
- Focused .NET and frontend tests, followed by `npm run build` and a desktop and
  mobile visual review.

## 7. Remaining questions

- Should two overlapping Ringfences create a notification, or only clashes where
  at least one side is a reservation?
- How long should resolved clash records and per-user seen records be retained?
- Which M3 allocation representation is authoritative when the same confirmed
  booking is also present in Reservations?
