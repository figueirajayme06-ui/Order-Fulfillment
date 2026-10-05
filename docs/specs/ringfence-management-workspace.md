# Feature specification: Ringfence management workspace

## 1. Objective

**User and job:** Fleet planners need to create, inspect, and maintain ringfences while retaining context on their protected assets.

**Problem:** The React Ringfence page only supports basic creation, listing, and deletion. Editing records, inspecting the asset list, removing assets, owner selection, and overlap awareness remain in the legacy OF.UI workflow.

**Outcome:** The React workspace supports the legacy Ringfence lifecycle without requiring a return to OF.UI: create and edit the record, inspect its assets, add/remove assets, and receive clear confirmation before an overlapping asset assignment.

## 2. Scope and boundaries

### In scope

- Ringfence register with title, period, separate division and warehouse columns, owner, status, asset count, client-side filtering, sorting, and bounded paging.
- A selected-record inspector for edit, asset inspection, remove, and add actions.
- Existing legacy rules: required title/division and create-time warehouse, owner defaulting, date range validation, no past periods, unique name, division access, and overlap warning before adding assets.
- Additive WebApp contract additions needed to expose selected-ringfence asset details and preserve owner/validation/overlap behaviour.
- Focused React and controller contract coverage.

### Out of scope

- Database schema changes.
- A new generic asset selection framework or an independent Ringfence route per record.
- A cross-Ringfence schedule/timeline view. It should be considered later as an alternate register view, not embedded in a single-record inspector.
- Changing the current division-visible collaboration permissions.
- Bulk removal or export/print of Ringfence assets.

### Constraints and known rules

- Ringfence permissions remain division-based: owner/creator are metadata rather than mutation ACLs.
- A normal user may create or change the division scope only when assigned every selected division; a super-admin is unrestricted. A user who can see a shared Ringfence through one of its divisions may still update its metadata while the unchanged scope is retained.
- Creation defaults an omitted owner to the authenticated user. An existing owner or warehouse that is no longer present in a lookup remains intact until the user changes the scope or chooses a replacement; a legacy blank warehouse remains editable.
- Warehouse lookup results must be constrained to the selected divisions in both the API and the editor. Multi-division results are grouped by division.
- Adding an asset requires asset access but does not require its division/warehouse to equal the Ringfence's values. Removing an item remains Ringfence-scoped so historical assets can be released.
- Asset detail rendering must remain bounded and must not fetch one asset at a time for large Ringfences.
- Follow the React UX guide: a persistent selected-record inspector, native controls, visible focus, confirmation for destructive actions, localised copy, and desktop-first density.

## 3. User experience

### Primary flow

1. The planner scans or filters the Ringfence list and selects a record.
2. The workspace shows the record's period, ownership/location context, and protected assets in a bounded right-side inspector.
3. The planner chooses **Edit details** to change title, dates, division, owner, or warehouse, then saves validated changes. If their access covers only part of a shared scope, the retained divisions are visible but not editable.
4. The planner pastes asset IDs or uses **Choose from Assets**. The latter carries the selected Ringfence in the URL and returns to its inspector after a successful add.
5. The server preflights a bounded batch, clearly separating unavailable IDs and existing assignments. If an add would overlap another Ringfence, the planner reviews the affected record/period details and explicitly continues or cancels.

### Information hierarchy

- List: the fields needed to choose a Ringfence, including period/state, asset count, and distinct division/warehouse cells. At narrow register widths, the column headings compact to `Div` and `WHS`.
- Inspector: selected record title, period, action controls, then a bounded asset table with asset ID, division, warehouse, description, item number, status, and warehouse location.
- Editor: takes over the inspector while the selected record remains fixed in the master register; its title, period, divisions, owner, and warehouse fields follow a predictable scan order.

### States and edge cases

- Loading: list/selection skeleton or clear loading label without losing the selected record.
- Empty: explain that no accessible Ringfences exist and provide Create.
- Error: retain the current selection/form values and name the failed action.
- Success/confirmation: announce saves, adds, and removals; confirm deletion, removal, discard, and overlap acknowledgement before mutation.
- Disabled/no-permission: prevent invalid form saves; surface the API access result without suggesting owner-based permission rules.

### Accessibility and responsive behaviour

- Keyboard/focus behaviour: selectable list rows use buttons or links; panels and confirmations retain/restore focus; destructive controls have explicit names.
- Accessible names/status treatment: active/expired state contains text; loading and mutations announce through a status region.
- Mobile fallback: list and inspector stack while retaining the selection and its asset context.
- Print impact: the workspace is not a required print workflow; interactive controls remain hidden in print.

## 4. Technical plan

- Affected routes/pages/components: `RingfencePage`, ringfence/asset services and types, existing Assets add-to-Ringfence action, `RingfenceController`, Ringfence response/request models, translation resources, and focused tests.
- API/data changes: additive detail response with displayable asset rows/count; owner lookup scoped by selected division; `items/preflight` and `items/batch` endpoints with server-side rechecks; server-side legacy validations expressed through `application/problem+json` field errors.
- Existing patterns/components to reuse: `Button`, `Card`, `Badge`, `Alert`, `Spinner`, table patterns from Assets, and the existing asset selection action.
- Proposed tasks, each independently testable:
  1. Add controller DTOs/validation and controller tests for full Ringfence parity.
  2. Type the frontend Ringfence API and implement the selected-record workspace.
  3. Integrate contextual Assets handoff, batch preflight/overlap feedback, and write component/service tests.
  4. Build and inspect selection, editing, errors, and asset-management states.

## 5. Acceptance criteria

- [x] A user can select an accessible Ringfence and see its complete asset table without visiting OF.UI.
- [x] A user can edit title, dates, divisions, owner, and warehouse, and sees validation before an invalid save.
- [x] A user can remove one displayed asset after confirmation and the list refreshes correctly.
- [x] A user can add assets through the current Assets workflow or direct asset-ID entry; an overlap requires an explicit acknowledgement before adding.
- [x] Creation/edit preserve legacy-required fields and reject duplicate names, past periods, invalid date ranges, and inaccessible divisions.
- [x] Existing division-visible collaboration access is preserved.
- [x] Creation defaults the owner to the signed-in user; owner/warehouse lookups are filtered to the selected division scope and unavailable legacy values are preserved on metadata-only edits.
- [x] A collaborator with partial division access cannot accidentally replace a shared Ringfence scope while editing its other details.

## 6. Verification

- Focused tests: Ringfence controller contract/validation tests; Ringfence and Assets service/page interaction tests.
- Build/lint/format commands: focused `dotnet test`; `npm test -- Ringfence AssetsPage AssetRingfenceActions ringfenceService`; `npm run build`; and the relevant lint/format checks.
- Manual visual checks: desktop register/inspector selection, bounded asset table, editor, overlap confirmation, empty/error states, keyboard focus, and mobile stacking.
- Data or migration checks: no schema migration; verify against a legacy Ringfence with at least one protected asset.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| API compatibility | Add fields/routes without removing current Ringfence endpoints or changing division permissions. | Approved and implemented. |
| Owner source | Reuse a division-scoped WebApp user lookup. | Implemented as `GET /api/lookups/users?division=...`. |
| Timeline | Do not add a timeline to the individual Ringfence inspector. A future schedule view should compare multiple records across time. | Deferred intentionally. |
| Asset display | Return asset display rows in the selected Ringfence response instead of issuing one request per item. | Approved and implemented. |
| Assignment safety | Add a server-validated preflight and batch mutation rather than chaining one asset mutation per ID. | Implemented with explicit overlap acknowledgement. |
