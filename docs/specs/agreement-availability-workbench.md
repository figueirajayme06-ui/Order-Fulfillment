# Feature specification: agreement availability workbench

## 1. Objective

**User and job:** A fulfilment planner working through a large agreement needs to compare lines and inspect stock
availability for the active line without losing their position.

**Problem:** Agreements commonly contain hundreds of lines and can exceed 1,000. Availability is currently appended
after the complete lines grid, so its distance from the selected line grows with the agreement.

**Outcome:** Selecting a line opens a persistent availability inspector in the same bounded work surface. The planner
can continue scrolling the lines independently while the selected-line context and availability remain visible.

## 2. Scope and boundaries

### In scope

- A viewport-bounded agreement-lines work surface with an independently scrolling grid and sticky column headers.
- A selected-line availability inspector below the grid that can be resized with pointer or keyboard input,
  collapsed, and closed.
- A compact inspector context showing line, item, required/fulfilled quantity, warehouse, and validity dates.
- Preservation of the existing availability, reservation, bulk-action, responsive-fallback, and print behaviour.
- Index reservations once by line so rendering hundreds of lines does not repeatedly scan the full reservation set.

### Out of scope

- API, database, fulfilment-rule, or reservation-quantity changes.
- Pagination, row virtualisation, new line filters, or changes to select-all semantics.
- A new shared split-pane dependency or cross-page component.

### Constraints and known rules

- Agreements may contain more than 1,000 lines; a line quantity may approach 1,000 units.
- A line is not equivalent to one asset or reservation.
- Existing in-progress bulk-fulfilment, reservation-disclosure, and availability division/location-filter changes must be preserved.
- The inspector is interactive-only and remains excluded from print; the complete lines table remains printable.

## 3. User experience

### Primary flow

1. The planner selects or keyboard-activates an agreement line.
2. The lines pane contracts within the work surface and keeps the selected row visible.
3. The availability inspector opens below it with persistent line context and the existing availability controls.
4. The planner can resize the split, collapse the inspector to its context header, choose another line, or close it.

### States and edge cases

- A selected line without a generic item number shows a clear unavailable message rather than an empty area.
- Changing lines updates the context and availability without resetting the lines scroll position.
- Closing the inspector restores focus to the selected row.
- On a narrow viewport the layout returns to normal document flow and does not expose a non-functional resize handle.

### Accessibility and print

- Each line has a keyboard-operable availability disclosure that exposes its expanded state without turning the
  table row into a nested interactive widget.
- The resize separator exposes its orientation and percentage value and supports arrow, Home, and End keys.
- Collapse and close controls have explicit accessible names and visible focus.
- Print removes the inspector and height constraints so the existing fulfilment table can flow across pages.

## 4. Technical plan

- Affected page: `src/OF.Frontend/src/pages/agreements/AgreementDetailPage.tsx` and its CSS Module/tests.
- Visible copy: add agreement-workbench strings to every supported locale.
- API/data changes: none.
- Existing patterns: theme tokens, shared `Button`, current `AvailabilityPanel`, and shared print hooks.

## 5. Acceptance criteria

- [ ] A selected line and its availability remain within one bounded desktop work surface regardless of line count.
- [ ] The lines grid scrolls vertically and horizontally with its headers remaining visible.
- [ ] Pointer and keyboard users can resize, collapse, reopen, and close availability without losing line context.
- [ ] The inspector identifies the line and distinguishes required quantity from fulfilled quantity.
- [ ] A line without a generic item number produces a useful state.
- [ ] Narrow-screen and print fallbacks remain usable.
- [ ] Existing reservation, bulk-action, availability, and page tests continue to pass.

## 6. Verification

- Focused tests: agreement-detail selection, inspector context, collapse/close, separator keyboard controls, and
  no-generic-item state.
- Commands: `npm test -- AgreementDetailPage`, `npm run build`, `npm run lint`, and `npm run format`.
- Manual: light/dark desktop, 1/100/1,000-line data, pointer and keyboard resize, narrow fallback, and print preview.
