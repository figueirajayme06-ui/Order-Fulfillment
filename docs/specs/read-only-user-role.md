# Feature specification: Read-only user role

> **ID:** FE-01  
> **Status:** Complete — implemented, merged, and manually UAT-validated on 2 September 2026
> **Priority:** P0 (security-sensitive)

## 1. Objective

**User and job:** Colleagues who are not operational NOF users need to inspect authorised Agreements, Assets,
availability, schedules, and Ringfences without being able to change operational data.

**Problem:** Access is currently primarily constrained by division and endpoint-specific rules. There is no single user
setting that guarantees a person can inspect NOF but cannot reserve, fulfil, activate, edit, delete, or administer data.

**Outcome:** An administrator can assign a `ReadOnly` role. A read-only user retains normal division-scoped visibility
but every operational write is blocked by the API and unavailable in the frontend.

## 2. Scope and boundaries

### In scope

- Add `ReadOnly` to the existing user feature-role catalogue and Admin add/edit workflow.
- Expose an explicit read-only capability in `/api/auth/me` so the frontend can render the correct action state.
- Hide or disable mutation controls throughout the React application.
- Enforce the restriction server-side for every operational mutation, regardless of frontend visibility.
- Allow read-only users to use read operations, including the existing POST-based Asset event query.
- Preserve the caller's existing division restrictions on every read.
- Add a controller-level mutation inventory and tests proving that all classified writes return `403 Forbidden`.

### Out of scope

- Automatic provisioning of people who do not have a configured NOF `Users` record.
- A new division or row-level access model.
- Anonymous/public access, impersonation, approval workflows, or field-level masking.
- Replacing the existing roles CSV with a new roles schema.
- Allowing read-only users to administer users or share saved views.

### Constraints and known rules

- `ReadOnly` uses the existing `Users.Roles` value; no database field is required for this feature.
- The role must be available even when Change Order and frontend-preview feature flags are disabled. It is a security
  role, not an optional feature toggle.
- A read-only user still requires at least one assigned division unless they are a deliberately configured Super Admin.
- Admin/Super Admin and ReadOnly are mutually exclusive in the Admin UI and API. For defence in depth, ReadOnly wins if
  inconsistent legacy data contains both.
- Server authorisation is the boundary. Hiding buttons is usability, not enforcement.
- Do not block solely by HTTP verb. `POST /api/event/events` is a read-only query and must remain available.
- Updating the user's last-access audit timestamp during authentication is an allowed system audit write and is not an
  operational mutation.
- Read-only users may apply a shared/saved view and change filters/columns in memory. They may not create, update,
  delete, or share a server-persisted view. Browser-local preferences may remain available if the existing fallback is
  active.
- An authenticated Aggreko account that has not been added to NOF receives a dedicated access-required page rather than
  a generic authentication failure or repeated reloads. This does not automatically provision the account.

## 3. User experience

### Primary flow

1. An administrator creates or edits a configured user, assigns divisions, and selects **Read only**.
2. The user opens NOF and sees the same division-authorised pages and record detail as a normal user.
3. Actions that would change operational or server-persisted data are absent or clearly disabled; ordinary navigation,
   filters, saved-view selection, availability inspection, timelines, print, and export remain available.
4. If the user calls a mutation endpoint directly or from a stale client, the API returns `403` without side effects.

### Information hierarchy

- Show a compact **Read only** badge with the user identity or page shell so the restriction is understandable.
- Do not fill pages with repeated permission warnings.
- Where removing an action would make a section ambiguous, show one concise read-only explanation at section level.

### States and edge cases

- Loading: do not briefly render mutation controls before identity is known.
- Error: authentication errors remain distinct from a read-only restriction.
- Not provisioned: explain that the signed-in account needs to be added by a NOF administrator and provide a single
  **Try again** action for use after access is granted.
- Disabled/no-permission: stale or direct mutation attempts return `403`; the client shows a stable permission message.
- Conflicting roles: the Admin API rejects new ReadOnly + Admin/Super Admin combinations; existing conflicts are treated
  as read-only until corrected.
- Deep links: a read-only user can open an authorised Agreement, Asset profile, or Ringfence directly but cannot mutate
  it.

### Accessibility and responsive behaviour

- The read-only status is text, not colour alone.
- Removed actions must not leave broken focus order or unnamed empty toolbars.
- Permission feedback is announced once through the established alert/status pattern.
- No special mobile or print restriction is required; mutation controls remain excluded from print as today.

## 4. Technical plan

- Affected areas: user role constants/catalogue, Admin controller/page, Auth response/types/context, shared mutation access
  helper, every WebApp mutation controller, relevant React action surfaces, API contract, and focused tests.
- API/data changes: additive `isReadOnly` field on `/api/auth/me`; Admin roles include `ReadOnly`; operational mutations
  return `403` before validation or writes.
- Existing patterns to reuse: `User.ActiveRoles`, current Admin role controls, `IUserIdentity`, division access helpers,
  and established permission feedback.
- Do not use a blanket “all POSTs are writes” middleware. Implement one reusable read-only decision and apply it to an
  explicit mutation policy/attribute or explicit action guards, with a reviewed allow-list for query-style POSTs.

### Proposed agent tasks

1. Inventory every WebApp endpoint as read/query, operational mutation, saved-view mutation, admin mutation, or system
   audit. Add the inventory to controller tests so future writes cannot bypass classification silently.
2. Add and test role parsing, contradictory-role validation, Admin role selection, and `/api/auth/me` capability output.
3. Apply the API guard to reservations, fulfilment, equipment, activation/cancellation, pull/import, Ringfence, saved
   views, and Administration mutations. Prove each denied request has no side effect.
4. Centralise the frontend capability check and remove/disable all matching actions without changing read routes.
5. Update the Web API contract and run focused backend/frontend security and visual checks.

## 5. Acceptance criteria

- [x] An Admin or Super Admin can assign and remove the `ReadOnly` role from a configured non-admin user.
- [x] The API rejects ReadOnly combined with Admin or Super Admin, and ReadOnly takes precedence for inconsistent stored
      data.
- [x] A read-only user can list and open authorised Agreements, Assets, availability results, schedules, Ringfences,
      and shared views, subject to unchanged division access.
- [x] Reservation create/delete, depot fulfilment, rehire, equipment add/delete, activation/cancel, pull/import,
      Ringfence writes, Admin writes, and server saved-view writes all return `403` for a read-only user.
- [x] `POST /api/event/events` and any other explicitly classified query endpoint still work for a read-only user.
- [x] Denied requests perform no database, queue, or downstream side effect.
- [x] The React UI does not expose an enabled operational mutation action while authentication is loading or after a
      read-only identity is resolved.
- [x] Direct/deep-linked read pages continue to work and display an accessible Read only status.
- [x] Normal, Admin, and Super Admin behaviour is unchanged when `ReadOnly` is absent.
- [x] Controller tests cover every classified mutation endpoint and focused UI tests cover the major action surfaces.
- [x] An authenticated account missing from NOF receives the coded `user_not_provisioned` `403` response and a friendly,
      localised access-required page without entering a reload loop.
- [x] A missing or expired authenticated principal remains distinct and other API `401` responses retain the existing
      EasyAuth reauthentication behaviour.

## 6. Verification

- Focused tests: role parsing/Admin validation/Auth response; one test per mutation action; React shell, Agreement detail,
  Assets/Ringfence, Admin, and saved-view action visibility.
- Commands: targeted `dotnet test`; targeted `npm test`; `npm run build`; `git diff --check`.
- Manual checks: read-only navigation and deep links, absence of action flashes during auth loading, `403` feedback from a
  stale client, and regression checks with normal/Admin/Super Admin users.
- Security review: compare the final action inventory with every `[HttpPost]`, `[HttpPut]`, `[HttpPatch]`, and
  `[HttpDelete]` action in `src/OF.WebApp/Controllers`.

## 7. Decisions and open questions

| Item                     | Decision or question                                                                                                                               | Owner / resolution             |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------ |
| Persisted personal views | Block create/update/delete for read-only users regardless of persistence source; applying existing views and in-session filters remains available. | Implemented conservatively.    |
| Role storage             | Use the existing roles CSV; do not add an `IsReadOnly` field.                                                                                      | Recommended KISS decision.     |
| Conflicting roles        | ReadOnly denies writes and the Admin workflow prevents the combination.                                                                            | Recommended security decision. |
| Non-NOF users            | They must still be explicitly configured with divisions; no just-in-time provisioning.                                                             | Recommended scope boundary.    |
