# Feature specification: asset and agreement notes

## 1. Objective

**User and job:** Authenticated operational users need to record and update concise planning context while viewing an asset profile or agreement.

**Problem:** Asset and agreement pages do not expose the existing typed-notes persistence capability, and an asset profile does not provide the same visible-current-date timeline experience as the agreement timeline.

**Outcome:** Each profile provides a compact Notes action that opens an accessible editable notes dialog which persists across reloads, and the asset schedule opens centred on the current date.

## 2. Scope and boundaries

### In scope

- Asset-profile Gantt display using the established timeline wrapper.
- The existing typed-note collection for each accessible asset and agreement, with add and edit support.
- Authenticated API reads and writes, with existing division checks and no additional ownership restriction.

### Out of scope

- Changes to legacy `src/OF.UI` screens.
- Rich text, mentions, note deletion, or a separate audit-history UI.

### Constraints and known rules

- Keep the existing agreement timeline unchanged; `FrappeGantt` owns its `scroll_to: "today"` behaviour.
- Read-only roles are still authenticated users for this note-only workflow; division access remains the server-side boundary.
- Empty, loading, success, failed-save, and dirty states must be clear and keyboard accessible.

## 3. User experience

### Primary flow

1. Open an asset profile or agreement detail and select **Notes**.
2. Read or edit existing notes, or add a new note, and save it in the dialog.
3. See a saved confirmation or a retryable error while retaining typed text.

### States and edge cases

- Loading: text area and save control are disabled with a labelled loading state.
- Empty: explain that no note has been recorded and allow immediate entry.
- Error: preserve the draft and expose a retryable alert.
- Success: announce confirmation and clear the dirty state.

### Accessibility and responsive behaviour

- Native labelled text area and buttons, keyboard-operable save, and live status messages.
- The compact page action opens a modal dialog with trapped focus, Escape/close behaviour, and focus restoration.
- Closing and reopening the dialog retains an unsaved draft while the record page remains open.
- Notes do not affect print output.

## 4. Technical plan

- Affected routes/pages/components: asset profile, agreement detail, a focused shared notes editor, and a new `/api/notes` controller.
- API/data changes: expose the existing `Notes` table through typed asset/agreement GET, POST, and item PUT routes; no schema change.
- Existing patterns/components to reuse: `FrappeGantt` (including `scroll_to: "today"`), `Button`, `Alert`, `Spinner`, `IDataRepository` typed note methods, and division access helpers.
- Proposed tasks:
  1. Add authenticated, division-authorised notes endpoints and controller tests.
  2. Add the frontend notes service/editor and profile/detail integration tests.
  3. Render asset profile schedule events with `FrappeGantt` while retaining date-range and table context.

## 5. Acceptance criteria

- [ ] Asset profiles open with the current date visible in their timeline.
- [ ] Any authenticated user with record access can retrieve and save asset or agreement notes.
- [ ] Empty, loading, dirty, success, and failed-save states are clear and accessible.
- [ ] Existing agreement timeline behaviour is unchanged.

## 6. Verification

- Focused frontend component/page/service tests and Web API controller tests.
- Frontend type check/lint/build and targeted .NET tests.
- Manual timeline, keyboard, and desktop/mobile visual review where a browser is available.

## 7. Decisions and open questions

| Item             | Decision or question                                                                                                | Owner / resolution            |
| ---------------- | ------------------------------------------------------------------------------------------------------------------- | ----------------------------- |
| Note cardinality | Preserve the existing multi-note model; return all matching notes newest first and require the note ID for updates. | Existing persistence contract |
