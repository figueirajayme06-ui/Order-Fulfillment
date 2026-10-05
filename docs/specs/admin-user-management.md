# Feature specification: Admin user management parity

## 1. Objective

**User and job:** Administrators need to add people from the company directory and maintain each user's access and display preferences.

**Problem:** The React Administration page can create a manually entered user and delete users, but it does not expose the legacy page's directory lookup, editing, controlled division/language/date-format choices, or optional roles.

**Outcome:** Administrators can complete the supported legacy user-management workflow in the React application without returning to the legacy UI.

## 2. Scope and boundaries

### In scope

- Search Microsoft Graph for a person and use the selected directory identity when creating a user.
- List and edit existing users.
- Select one or more known divisions, a language, a date format, Admin status, Super Admin status where authorised, and optional feature roles.
- Delete users using the current API permission boundary.
- Provide loading, empty, validation, success, and recoverable error states.

### Out of scope

- Database schema changes, bulk import/export, password or Microsoft Entra account administration, and changes to authentication.
- Literal reproduction of the Infragistics grid or its visual design.

### Constraints and known rules

- List/create/update require Admin or Super Admin. Delete and Super Admin changes remain Super Admin-only.
- Roles are shown and persisted only when the existing feature configuration enables them.
- Directory credentials stay server-side and reuse the existing Graph configuration keys.
- Existing machine-specific configuration and runtime data are not changed.

## 3. User experience

### Primary flow

1. An administrator opens Administration and sees the configured users.
2. Add user opens a form whose first control searches the company directory.
3. Selecting a person fixes their login/name and reveals controlled access and preference fields.
4. Saving creates the user and refreshes the table.
5. Edit opens the same controlled settings for an existing row; save updates it in place.
6. A Super Admin can confirm deletion from the user row.

### Information hierarchy

- Login, name, divisions, language, access level, and actions remain visible in the table.
- Date format and optional feature roles are visible while adding or editing.

### States and edge cases

- Loading: skeleton/spinner while users or options load.
- Empty: explain that no users are configured and offer Add user.
- Error: retain the current data and show a retryable message.
- Success/confirmation: announce saved and deleted outcomes.
- Disabled/no-permission: hide Super Admin mutation controls from ordinary admins and deny the route to non-admins.

### Accessibility and responsive behaviour

- Native labelled inputs, selects, checkboxes, buttons, and table semantics.
- Directory results are keyboard-operable buttons; status feedback uses a live region.
- The table scrolls horizontally on narrow layouts and interactive controls are excluded from print.

## 4. Technical plan

- Affected route/page: `/admin`, `AdminPage`, its CSS module and tests.
- API/data changes: additive Admin user fields plus admin options and directory-search endpoints.
- Existing patterns/components: shared Button, Badge, Card, Spinner, Alert, Axios service, and existing Admin permission rules.
- Proposed tasks:
  1. Extend and test the Admin API contracts and directory client.
  2. Extend and test the frontend service contract.
  3. Implement and test add/edit/delete workflows and states.
  4. Update the API contract and verify builds, tests, lint, formatting, and desktop layout.

## 5. Acceptance criteria

- [x] An admin can search the directory and create the selected person with controlled settings.
- [x] Existing users can be edited without changing their login identity.
- [x] Multi-division, language, date-format, Admin, authorised Super Admin, and enabled role settings persist.
- [x] Duplicate directory users are identified before create.
- [x] Delete retains the current Super Admin-only confirmation and permission boundary.
- [x] Loading, empty, validation, API error, and success states are clear and accessible.
- [x] Focused frontend/backend tests and production builds pass.

## 6. Verification

- Focused Admin controller, directory service, frontend service, and Admin page tests.
- `dotnet test`, `npm test`, `npm run build`, `npm run lint`, and `npm run format` as proportional checks.
- Desktop visual review of add, edit, table, error, empty, and narrow-window states.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| API expansion | Additive Admin API changes are approved. | User, 31 August 2026 |
| Legacy grid | Preserve business capabilities using React patterns rather than Infragistics. | Product guidance |
