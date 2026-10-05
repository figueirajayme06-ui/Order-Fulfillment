# Feature specification: agreement bulk fulfilment

## 1. Objective

**User and job:** Fleet planners need to apply depot fulfilment or rehire to several agreement lines in one operation.

**Problem:** The React agreement detail page does not provide an explicit line-selection workflow. Its existing action buttons submit every line, provide no in-progress or outcome feedback, and the depot-fulfil API creates a quantity-one reservation even when a line requires more than one unit.

**Outcome:** A planner can select agreement lines, run one bulk action against exactly that selection, and see a clear result. Depot fulfilment covers the selected line's outstanding quantity, matching the original `OF.UI` behaviour.

## 2. Scope and boundaries

### In scope

- Select individual agreement lines or all visible agreement lines.
- Depot fulfil or rehire the selected lines.
- Prevent duplicate submissions and show success/error feedback.
- Refresh agreement and reservation data after a successful action.
- Create depot-fulfil reservations for the selected line's outstanding quantity.

### Out of scope

- Changing rehire alternative-selection rules.
- Adding package-group selection.
- Replacing already fulfilled lines or exposing the legacy “all” variants.
- Changing reservation, activation, or agreement authorisation.

### Constraints and known rules

- The existing bulk endpoints and division-access boundary remain unchanged.
- Fully fulfilled lines are not processed when `includeAlreadyFulfilled` is false.
- Unknown or foreign line IDs remain harmless and are ignored by the header-scoped API.
- Controls are hidden from print output and all new visible copy is localised.

## 3. User experience

### Primary flow

1. Select one or more agreement lines with row checkboxes or the header checkbox.
2. Choose `Depot fulfil` or `Rehire` next to the Lines heading.
3. While processing, both actions are disabled and their state is announced.
4. On success, refreshed line/reservation state is displayed, the selection is cleared, and a processed-line count is shown.

### Information hierarchy

- The existing line data and line-fulfilment status stay visible.
- Selection is a narrow leading table column.
- Bulk controls and the selected-line count stay adjacent to the Lines heading.

### States and edge cases

- Loading: existing agreement loading state remains.
- Empty: action buttons are disabled when there are no selected lines.
- Error: retain the selection for retry and show a dismissible error alert.
- Success: clear selection and show the number of processed lines.
- Partial result: report the returned processed count; skipped fulfilled or ineligible lines remain visible after refresh.

### Accessibility and responsive behaviour

- Native checkboxes have agreement-line labels; the select-all checkbox exposes a mixed state.
- Buttons remain keyboard accessible and disabled during submission.
- Feedback uses an alert live region.
- The table retains horizontal scrolling on narrow layouts.
- Selection and bulk controls are excluded from print.

## 4. Technical plan

- Affected route/page: `/agreements/:headerId`, `AgreementDetailPage`.
- API/data behaviour: keep the request/response shape; calculate depot quantity from line quantity minus existing effective reservations.
- Existing patterns/components: shared `Button` and `Alert`, CSS Modules, `react-i18next`.
- Proposed tasks:
  1. Add line-selection state, accessible checkbox controls, submission state, and result feedback.
  2. Correct depot-fulfil reservation quantity in `BulkActionsController`.
  3. Add component and controller coverage, then run focused tests and the frontend build.

## 5. Acceptance criteria

- [ ] No bulk action is available until at least one line is selected.
- [ ] Select-all selects and clears all agreement lines and displays a mixed state for partial selection.
- [ ] Depot fulfil and rehire submit only the selected line IDs once.
- [ ] Successful actions refresh data, clear selection, and report the processed count.
- [ ] Failed actions retain selection, re-enable controls, and show a useful error.
- [ ] Depot fulfil reserves the outstanding quantity for each processed line, not a hard-coded quantity of one.
- [ ] New copy is localised and interactive controls do not appear in print.

## 6. Verification

- Focused tests: `AgreementDetailPage.test.tsx`, `BulkActionsControllerTests`.
- Build: `npm run build` from `src/OF.Frontend`.
- Manual visual checks: normal desktop line scanning, partial/select-all states, keyboard operation, disabled/loading/success/error states, narrow-table scrolling, and print preview.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| Fulfilled-line replacement | Keep the safer current API mode (`includeAlreadyFulfilled: false`); replacement remains out of scope. | Product follow-up if required |
| Depot quantity | Match legacy `OF.UI`: reserve the outstanding line quantity; this equals the full line quantity when no reservation exists. | Confirmed by request and legacy engine |
