# Feature specification: Share saved views with named users

> **ID:** FE-07  
> **Status:** Complete — implemented and UAT-validated on 2 September 2026; reusable UX hardening added on 3 September 2026.
> **Priority:** P2  
> **Recommended predecessor:** [Configurable Agreement and Asset table columns](configurable-table-columns.md)

## 1. Objective

**User and job:** A planner who has built a useful Agreement or Asset view needs to share it with selected colleagues
without publishing it to an entire division or the whole application.

**Problem:** Saved views currently support personal, division, and global scopes. Division/global publishing requires an
Admin, and there is no named-recipient scope.

**Outcome:** A write-enabled view owner can share a saved view with selected configured NOF users. Recipients can apply
the view but cannot change the owner's view or gain access to underlying records outside their divisions.

## 2. Scope and boundaries

### In scope

- Add a **Specific users** saved-view scope for Agreement and Asset views.
- Let the owner search/select one or more configured NOF users visible through authorised user lookup rules.
- Persist recipients in a normalised `ViewRecipients` table owned by `OF.Data.Design`.
- Include received views in list results and group them as **Shared with me**.
- Let the owner (or an Admin under current manage rules) add/remove recipients, rename/update, unshare, or delete the view.
- Keep recipient access read-only and recheck it on every list/read.
- Preserve personal, division, global, local-fallback, and old saved-view behaviour.

### Out of scope

- Sharing with email addresses that are not configured NOF users.
- Distribution lists, teams/groups, public links, notifications, ownership transfer, co-editing, or per-recipient edit
  permission.
- Copy-on-write/fork UI; a recipient may manually save the applied state as a new personal view if otherwise permitted.
- Broadening Agreement/Asset/division access or exposing records through saved state.
- Sharing browser-local fallback views until they have been saved successfully to the server.

### Constraints and known rules

- A recipient sees only the view definition. Every data request still intersects with their normal division access; a
  shared filter may therefore produce fewer or zero rows.
- Normal users may share with configured users whose divisions intersect their own authorised divisions. Super Admins
  may choose any configured user. The owner cannot be duplicated as a recipient.
- Read-only-role users can apply received views but cannot create, update, delete, or share a server view.
- Recipients have `canEdit = false` and `canDelete = false`. Existing owner/Admin manage rules remain authoritative.
- Request recipients are normalised, deduplicated case-insensitively, and validated in one server operation.
- A Specific users view requires at least one valid recipient. Removing the final recipient requires changing scope to
  Personal or adding another recipient.
- User deletion removes or safely orphans recipient links according to the chosen FK policy; recommended behaviour is an
  FK with cascade delete on recipient rows, never deletion of the view itself.
- Persist sharing metadata separately from `ViewJson`; the frontend state envelope/version does not encode identities.
- Existing `ForEveryone` values keep their meanings. Recommended compatibility representation is `ForEveryone = 0` plus
  recipient rows, with the React API mapping the presence of recipients to scope `users`.

## 3. User experience

### Primary flow

1. The owner chooses **Specific users** while saving a new or existing view.
2. A labelled user search/selector appears. The owner selects one or more people and saves.
3. A recipient opens the same page and finds the view under **Shared with me**, labelled with its owner.
4. Applying it restores the saved page state. Update/Delete remain unavailable to the recipient.
5. The owner can edit recipients or change the view back to Personal, which removes recipient access immediately.

### Information hierarchy

- Keep the main saved-view picker compact and group Personal, Shared with me, Division, and Global views.
- The editor shows scope first, then recipients only for Specific users.
- Display recipient chips with full name and login where needed to disambiguate; owner is visible on received views.

### States and edge cases

- Loading users: keep existing view state intact and disable save with a clear status.
- No eligible users: explain the division/configuration boundary; do not offer directory search.
- Invalid/deleted recipient at save: reject atomically with a useful validation message and retain the draft.
- Recipient removed while viewing: the next list refresh removes the view; an already applied in-memory state may remain
  until another view/reset is chosen, but no unauthorised data is exposed.
- API/network failure: do not silently convert a shared view into a local personal view.
- Empty data after apply: explain that the view may target data outside the recipient's accessible divisions and offer
  Reset filters.

### Accessibility and responsive behaviour

- The user selector is fully keyboard operable with a labelled search input, result count, selectable results, and
  removable recipient chips/buttons.
- Scope and ownership are expressed in text, not colour.
- Focus moves predictably when adding/removing recipients and errors are associated with the selector.
- Controls wrap on narrow widths; saved views remain excluded from print while their applied table state prints normally.

## 4. Technical plan

- Affected areas: `Views` persistence/repository, new `ViewRecipients` schema/model, Views controller/mapper/contracts,
  user lookup, frontend views service/types/hooks/controls, Agreement/Asset pages, translations, tests, and API contract.
- API/data changes: support scope `users` and `recipients: string[]` on create/update; return owner plus recipient metadata
  needed to manage/display. Listing includes a candidate when caller is owner, global/division-authorised, or a current
  recipient.
- Existing patterns to reuse: configured division-scoped user lookup, view owner/Admin management, page-state decoder,
  no-response-only local fallback, and Admin/Ringfence user-selector patterns where suitable.

### Proposed agent tasks

1. Add `ViewRecipients` with keys/indexes/FK deletion behaviour; extend repository methods and atomic upsert/delete tests.
2. Extend Views list/create/update/delete authorisation and DTO validation for named recipients without changing existing
   scopes.
3. Extend the frontend service and saved-view controls with recipient selection, Shared with me grouping, ownership,
   failure, and local-fallback rules.
4. Test recipient removal/deletion, division data isolation, read-only users, backwards compatibility, and both pages.
5. Update the Web API contract and run database/backend/frontend/visual/keyboard verification.

## 5. Acceptance criteria

- [x] A write-enabled user can save an Agreement or Asset view for one or more eligible configured users.
- [x] Recipient identifiers are validated and persisted atomically, case-insensitively deduplicated, and never include the
      owner redundantly.
- [x] A recipient sees the view under Shared with me with its owner identified and can apply but not edit/delete it.
- [x] Sharing a view does not broaden Agreement, Asset, timeline, availability, or division access.
- [x] Owners/Admins can change recipients, change scope, update, or delete according to existing manage rules.
- [x] Removing a recipient revokes future list access; deleting a user removes recipient links without deleting the view.
- [x] Specific users requires at least one recipient and a failed save retains the user's draft selections.
- [x] Shared mutations never fall back silently to browser-local storage.
- [x] Existing personal/division/global views and version-1 page states continue to load and behave unchanged.
- [x] Read-only-role users can apply received views but cannot share or mutate server views.
- [x] Agreement and Asset controls provide equivalent sharing behaviour and accessible keyboard operation.

## 6. Verification

- Focused tests: schema/repository recipient lifecycle; owner/recipient/Admin/outsider/read-only API matrix; invalid and
  duplicate recipients; division data access; frontend selection/grouping/manage flags; API failure/local fallback; old
  views.
- Commands: targeted .NET and frontend saved-view tests; `dotnet build`; `npm run build`; `npm run lint`; `npm run format`;
  `git diff --check`.
- Manual checks: share to several users, recipient apply, revoke while open, deleted recipient, no eligible users, network
  error, long names, keyboard selector, narrow toolbar, Agreement and Asset parity.
- Data check: recipient table indexes/FKs deploy from `OF.Data.Design` only and do not alter existing `ForEveryone` data.

## 7. Decisions and open questions

| Item                 | Decision or question                                                                | Owner / resolution                                     |
| -------------------- | ----------------------------------------------------------------------------------- | ------------------------------------------------------ |
| Who may share        | Any non-read-only view owner; division/global scopes remain Admin-only.             | Recommended interpretation of the request.             |
| Eligible recipients  | Configured users intersecting caller divisions; Super Admin may select all.         | Accepted by product owner on 2 September 2026.         |
| Recipient permission | Apply only; no co-editing.                                                          | Recommended KISS decision.                             |
| Storage              | Normalised `ViewRecipients`; keep identities out of `ViewJson`.                     | Recommended data design.                               |
| Compatibility value  | Keep `ForEveryone = 0`; recipient rows distinguish Specific users in the React API. | Agent to validate against legacy repository behaviour. |

## 8. Reusable UX hardening amendment

The Agreement and Asset implementations use one controlled saved-view toolbar so future data grids can adopt the same
interaction without coupling their page-state shapes. Page controllers continue to own capture/apply logic.

### Interaction decisions

- Editing recipients is a sharing-only transaction. **Save sharing** preserves the stored name and page state.
- An existing view uses **Save changes** as its primary action; **Save as new** is an explicit secondary copy path.
- Save actions reflect dirty state, mutations have progress states, and switching views warns before discarding changes.
- Delete confirmation identifies the view and explains loss of recipient access for a shared view.
- Collapsed sharing shows the recipient count. The picker focuses on open, supports Escape and arrow-key selection, returns
  focus to its trigger, and shows neutral guidance until validation has actually failed.

### Additional acceptance criteria

- [x] Sharing changes cannot overwrite unrelated saved filters, sorting, columns, name, or scope.
- [x] Existing-view actions distinguish updating from creating a copy and are disabled when there is nothing to save.
- [x] Switching a dirty view, repeated mutation submission, and accidental deletion are guarded.
- [x] Sharing ownership/count remains understandable while the picker is collapsed.
- [x] Recipient selection is operable by keyboard with predictable open/close focus.
- [x] Agreement, Asset, and future grid consumers use the same controlled toolbar contract.
- [x] Save as new and copy actions require a unique trimmed, case-insensitive name; renaming cannot collide with another visible view.
