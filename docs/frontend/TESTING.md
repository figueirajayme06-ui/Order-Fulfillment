# Frontend testing and verification

## Purpose

Tests and visual checks protect high-density operational workflows. Use the smallest set of checks that proves the change, then run broader checks for shared or cross-page work.

Run commands from `src/OF.Frontend` unless stated otherwise.

## Commands

```powershell
# Run one focused Vitest suite by filename/pattern.
npm test -- Sidebar

# Run all frontend unit/component tests.
npm test

# Type-check and build the production frontend.
npm run build

# Lint and formatting checks for broad changes.
npm run lint
npm run format

# Playwright suite (see current status below).
npm run test:e2e
```

## Required checks by change type

| Change | Minimum verification |
| --- | --- |
| Isolated component or helper | Focused test where practical, then `npm run build`. |
| Shared component, state parser, or service | Relevant focused tests, `npm test`, and `npm run build`. |
| Broad frontend change | Relevant tests, `npm run build`, `npm run lint`, and `npm run format`. |
| Grid/filter/sort change | Manual desktop check of visible columns, column filters, More filters, sorting, saved views, loading/empty/error state, and print. |
| Timeline change | Manual check of period label/navigation, resizable context pane, legend, no-event assets, and print. |
| Fulfilment/reservation change | Manual check of agreement state versus line fulfilment, reserve/remove outcome, availability context, destructive-action confirmation, and error handling. |
| Sidebar/layout change | Manual check of desktop expanded/collapsed state, accessible labels/tooltips, mobile fallback, keyboard focus, and print. |
| New visible copy | Confirm all supported i18n files are updated and that labels remain understandable in context. |

## Manual visual review

Automated tests do not prove the information hierarchy is usable. For a changed workflow, review the screen at a normal desktop size and ask:

- Is the next decision/action obvious without scanning unrelated controls?
- Are high-frequency data fields visible, with secondary data progressively disclosed?
- Can a keyboard user see focus and operate every changed control?
- Is status understandable without colour alone?
- Is the empty/loading/error state clear and actionable?
- Did the change create duplicate filters, actions, or status treatments?

Use the [frontend UX and design guide](UX-DESIGN-GUIDE.md) as the review checklist.

## Print verification

Printing is supported by `src/OF.Frontend/src/print.css`.

When a table, timeline, or page layout changes:

1. Open the browser print preview from the application Print action.
2. Confirm navigation and controls are hidden.
3. Confirm the relevant grid columns remain readable in landscape output.
4. Confirm timelines print the current visible period and not a broken/empty surface.

## End-to-end test status

The default Playwright suite uses deterministic authenticated API fixtures. It covers Agreements navigation and filters,
agreement reset/deletion, reservation timelines, Ringfence editing/confirmation and Assets handoff, availability resizing,
and native asset-search submission. Expected rows must exist; tests do not skip assertions when data is missing.
Agreement timeline tests fix the browser date and verify that today's marker is inside the actual viewport for
current, historical, and future agreements. A mocked navigation call alone does not verify automatic scrolling.

The local browser URL follows Vite's certificate detection. To use an already installed browser instead of Playwright's
bundled Chromium, set `PLAYWRIGHT_CHANNEL` (for example, `msedge`) before running `npm run test:e2e`.

Workflow tests capture desktop and print-media screenshots in the ignored `test-results/` directory for visual review.
These tests verify frontend behavior against mocked responses. They do not replace integration checks against the real
API, production authentication, representative large datasets, or browser print-preview pagination checks.

Do not delete or weaken an existing failing test simply to make a change pass. Repair the route, fixture, or expected behaviour with the intended contract.

## Test locations

- Unit/component tests: `src/OF.Frontend/src/**/*.test.ts(x)`
- End-to-end tests: `src/OF.Frontend/e2e/`
- Test setup: `src/OF.Frontend/src/test-setup.ts`
- Browser configuration: `src/OF.Frontend/playwright.config.ts`

