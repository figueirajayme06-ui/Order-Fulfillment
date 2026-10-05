# Feature specification: Last NOF access audit timestamp

> **ID:** FE-05  
> **Status:** Complete — implemented, merged, and manually UAT-validated on 2 September 2026.
> **Priority:** P2

## 1. Objective

**User and job:** Administrators need a simple indication of when each configured user last accessed the new NOF
frontend for audit review and future lifecycle features.

**Problem:** The Users record and Administration table contain identity, access, and preferences but no access
timestamp. The application also does not receive an authoritative Microsoft Entra sign-in event.

**Outcome:** NOF records the UTC time of the most recent successful frontend identity bootstrap and shows it to
administrators as **Last NOF access**.

## 2. Scope and boundaries

### In scope

- Add nullable `LastLoginAtUtc` to `dbo.Users` through `OF.Data.Design`.
- Update it after a configured identity successfully resolves in `GET /api/auth/me`.
- Return it in the Admin user list and show a locale-formatted **Last NOF access** column.
- Distinguish users who have never accessed the new frontend.
- Add migration/model/repository/controller/frontend tests.

### Out of scope

- An immutable login history, session table, login count, active-session tracking, retention policy, or audit export.
- Querying Microsoft Entra sign-in logs or claiming this is the authoritative Entra authentication time.
- Updating the timestamp on every API request or on Admin list reads.
- Backfilling historical access from logs or the legacy UI.
- Using the timestamp to disable/delete dormant accounts in this increment.

### Constraints and known rules

- Store UTC as nullable `DATETIME2`; existing users migrate to null.
- The observable event is a successful `/api/auth/me` request made once during normal SPA bootstrap. A browser refresh may
  therefore update the value even if Entra did not ask for credentials again.
- Unknown/unauthenticated users do not create a user or update any timestamp.
- Complete the audit update before returning success, but an audit-write failure should be logged and should not block
  otherwise valid read access unless the product owner explicitly chooses fail-closed behaviour.
- Concurrent bootstraps are last-write-wins and must not move the timestamp backwards.
- Display uses the viewer's locale/date-time formatting; storage and API remain UTC ISO timestamps.
- The schema definition and deployment script live only in `src/OF.Data.Design`.

## 3. User experience

### Primary flow

1. A configured user opens or refreshes the new NOF frontend and `/api/auth/me` resolves them.
2. The server records the current UTC access time.
3. An administrator opens Administration and sees the user's Last NOF access value.

### Information hierarchy

- Keep Login, Name, access fields, and actions primary.
- Add Last NOF access as a compact sortable/display column; use `Never` for null.
- Edit forms do not expose or allow modification of the audit field.

### States and edge cases

- Existing/new user before first access: display **Never**.
- Successful access: subsequent Admin reads show the new value.
- Audit write failure: log with user/trace context, return normal identity data, and leave the previous value.
- Deleted or unknown user: authentication retains its current failure behaviour and creates no audit record.

### Accessibility and responsive behaviour

- Use a real table header and an unambiguous full date/time accessible label.
- A compact visual timestamp may use a tooltip/title, but assistive text must contain the full value and timezone.
- At narrow widths the Admin table continues horizontal scrolling; the field is not added to the edit form.
- Print impact is not required for Administration.

## 4. Technical plan

- Affected areas: Users SQL project definition/migration, generated EF User model, repository update method,
  `AuthController`, `AdminController` DTO, Admin service/types/page/translations/tests, and API contract.
- API/data changes: additive nullable Admin list field `lastLoginAtUtc`; no requirement to return it from `/api/auth/me`.
- Existing patterns to reuse: `IUserIdentity`, repository unit-of-work conventions, Admin table, locale formatting, and
  application logging.

### Proposed agent tasks

1. Add the nullable schema field and safe migration; update/scaffold the EF model and a narrow monotonic repository write.
2. Update `AuthController` after identity resolution, with success, unknown-user, concurrency, and write-failure tests.
3. Extend the Admin list contract and render Never/localised UTC timestamp without making it editable.
4. Update the Web API contract and run schema, backend, frontend, and visual checks.

## 5. Acceptance criteria

- [x] Existing Users migrate successfully with `LastLoginAtUtc = NULL`.
- [x] A successful `/api/auth/me` for a configured user records the current UTC time.
- [x] Failed/unknown authentication does not insert or update a User.
- [x] Concurrent/out-of-order writes cannot replace a newer timestamp with an older one.
- [x] An audit-write failure is logged and does not deny otherwise valid NOF read access.
- [x] Admin list responses include the nullable UTC value and the Admin table displays a localised timestamp or Never.
- [x] The field cannot be changed through Admin create/update requests.
- [x] Merely listing users or calling unrelated APIs does not update the value.
- [x] The UI and documentation call the field Last NOF access and explain its session-bootstrap meaning.

## 6. Verification

- Focused tests: SQL migration/idempotence as supported; repository monotonic update; Auth success/unknown/write-failure;
  Admin DTO and Never/timestamp rendering.
- Commands: targeted .NET and Admin frontend tests; `dotnet build`; `npm run build`; `git diff --check`.
- Manual checks: pre-migration user, first access, refresh, UTC-to-local display, and Admin horizontal layout.
- Data check: verify the deployed column is nullable `DATETIME2` and owned only by `OF.Data.Design`.

## 7. Decisions and open questions

| Item              | Decision or question                                                          | Owner / resolution                                        |
| ----------------- | ----------------------------------------------------------------------------- | --------------------------------------------------------- |
| Audit meaning     | Last successful new-frontend `/api/auth/me`, not authoritative Entra sign-in. | Architecture constraint; product owner to accept wording. |
| User-facing label | Last NOF access.                                                              | Recommended accurate label.                               |
| Failure mode      | Log and preserve access if the audit write fails.                             | Recommended availability-first decision.                  |
| History           | Store one nullable timestamp only.                                            | Requested KISS boundary.                                  |
