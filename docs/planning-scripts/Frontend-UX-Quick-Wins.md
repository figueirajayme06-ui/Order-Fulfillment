# Order Fulfillment Frontend UX Quick Wins (Desktop)

> **Historical planning record.** This backlog predates the Agreements terminology, current navigation behaviour, and the feedback-session UI patterns. Use the [frontend UX and design guide](../frontend/UX-DESIGN-GUIDE.md) for new work; retain this document only for its implementation history.

Date: 2026-07-31
Reviewer: GitHub Copilot (GPT-5.3-Codex)

## Context Update

- This backlog is now desktop-first only.
- Target users are business operators handling fleet reservations and order fulfilment.
- Mobile-specific enhancements are intentionally de-scoped.

## Scope Reviewed

- Live UI at /assets (desktop)
- App shell and navigation
- Orders, Assets, Ringfence, Admin, Order Detail, Timeline pages
- Shared UI primitives (Button, Alert, Card, AdvancedFilters, AssetSelector, AvailabilityPanel)

## De-Scoped from Previous Pass

- Mobile navigation fallback/collapsible sidebar
- Mobile-focused horizontal table scroll cues

## Prioritization Model

- P0: High impact, low effort, low risk (same day)
- P1: High impact, small implementation (1-2 days)
- P2: Medium impact, still quick (2-3 days)

Effort scale:
- S: 1-3 hours
- M: 0.5-1 day
- L: 1-2 days

## Desktop Workflow Quick-Win Backlog

| ID | Priority | Effort | UX improvement | Why this helps | Suggested implementation files |
| --- | --- | --- | --- | --- | --- |
| UX-01 | P0 | S | Add strong :focus-visible styles for all interactive controls | Keyboard operators get clear focus location; current global reset removes native button affordances | src/OF.Frontend/src/index.css, src/OF.Frontend/src/components/common/Button/Button.module.css, page-level CSS modules |
| UX-02 | P0 | S | Add confirm dialogs for destructive actions | Prevents accidental deletes in admin and reservation workflows | src/OF.Frontend/src/pages/admin/AdminPage.tsx, src/OF.Frontend/src/pages/ringfence/RingfencePage.tsx, src/OF.Frontend/src/pages/orders/OrderDetailPage.tsx |
| UX-03 | P0 | S | Add inline form validation and disable Save until valid | Removes silent no-op form submissions that feel broken to users | src/OF.Frontend/src/pages/admin/AdminPage.tsx, src/OF.Frontend/src/pages/ringfence/RingfencePage.tsx |
| UX-04 | P0 | M | Add success/error feedback for create, delete, and bulk actions | Operators need immediate operation outcome confirmation in high-throughput tasks | src/OF.Frontend/src/pages/admin/AdminPage.tsx, src/OF.Frontend/src/pages/ringfence/RingfencePage.tsx, src/OF.Frontend/src/pages/orders/OrderDetailPage.tsx, src/OF.Frontend/src/components/common/Alert/Alert.tsx |
| UX-05 | P0 | M | Make clickable table rows keyboard accessible | Improves speed and accessibility for power users navigating dense lists | src/OF.Frontend/src/pages/orders/OrdersPage.tsx, src/OF.Frontend/src/pages/orders/OrderDetailPage.tsx, src/OF.Frontend/src/pages/assets/AssetsPage.tsx |
| UX-06 | P1 | M | Add filter summary chips plus one-click Clear all for basic filters | Makes active criteria obvious and reduces no-results confusion | src/OF.Frontend/src/pages/orders/OrdersPage.tsx, src/OF.Frontend/src/pages/assets/AssetsPage.tsx, src/OF.Frontend/src/components/common/AdvancedFilters/AdvancedFilters.tsx |
| UX-07 | P1 | S | Show result context (Showing X-Y of Z) above tables | Helps users orient quickly in large, paginated operational datasets | src/OF.Frontend/src/pages/orders/OrdersPage.tsx, src/OF.Frontend/src/pages/assets/AssetsPage.tsx |
| UX-08 | P1 | S | Preserve filter and sort state in URL query params | Enables shareable and refresh-safe views for support and shift handover | src/OF.Frontend/src/pages/orders/OrdersPage.tsx, src/OF.Frontend/src/pages/assets/AssetsPage.tsx |
| UX-09 | P1 | M | Add sticky selected-assets action summary bar (Implemented 2026-07-31) | Improves confidence when doing multi-select add-to-ringfence actions | src/OF.Frontend/src/pages/assets/AssetsPage.tsx, src/OF.Frontend/src/pages/assets/AssetsPage.module.css |
| UX-10 | P1 | S | Add quick copy actions for key identifiers (Agreement, Asset ID) | Reduces repetitive manual selection/copy in business workflows | src/OF.Frontend/src/pages/orders/OrdersPage.tsx, src/OF.Frontend/src/pages/assets/AssetsPage.tsx, src/OF.Frontend/src/pages/orders/OrderDetailPage.tsx |
| UX-11 | P1 | M | Freeze key columns in wide tables (status/id) during horizontal scroll | Maintains context in dense, column-heavy desktop tables | src/OF.Frontend/src/pages/orders/OrdersPage.module.css, src/OF.Frontend/src/pages/assets/AssetsPage.module.css, src/OF.Frontend/src/pages/orders/OrderDetailPage.module.css |
| UX-12 | P1 | S | Add loading skeleton rows for table states (Implemented 2026-07-31) | Improves perceived performance and prevents layout jumping | src/OF.Frontend/src/pages/orders/OrdersPage.tsx, src/OF.Frontend/src/pages/assets/AssetsPage.tsx, src/OF.Frontend/src/components/common/TableSkeleton/TableSkeleton.tsx |
| UX-13 | P2 | S | Add Escape-to-close, focus trapping, and trigger-focus restore in AssetSelector modal (Implemented 2026-07-31) | Delivers expected desktop modal behavior and keyboard reliability | src/OF.Frontend/src/components/fulfilment/AssetSelector.tsx, src/OF.Frontend/src/components/fulfilment/AssetSelector.module.css, src/OF.Frontend/src/pages/orders/OrderDetailPage.tsx |
| UX-14 | P2 | S | Pair status color indicators with text labels consistently (Implemented 2026-07-31) | Improves readability and lowers interpretation errors in status-heavy views | src/OF.Frontend/src/pages/orders/OrdersPage.tsx, src/OF.Frontend/src/pages/assets/AssetsPage.tsx, corresponding CSS modules |
| UX-15 | P2 | S | Normalize hardcoded UI copy through i18n keys | Avoids mixed-language UI and keeps labels consistent across business functions | src/OF.Frontend/src/components/layout/Sidebar.tsx, src/OF.Frontend/src/pages/*, src/OF.Frontend/src/components/* |
| UX-16 | P2 | S | Fix undefined design token usage (--ref-colour-grey10) (Implemented 2026-07-31) | Removes inconsistent fallback styling in filter controls | src/OF.Frontend/src/theme.css, src/OF.Frontend/src/components/fulfilment/AssetSelector.module.css, src/OF.Frontend/src/components/fulfilment/AssetFilterBar.module.css |

## Suggested Implementation Order

1. UX-01 focus states
2. UX-02 destructive confirmations
3. UX-03 form validation
4. UX-04 operation feedback
5. UX-05 keyboard row access
6. UX-06 filter chips and clear all
7. UX-11 frozen key columns

## Acceptance Checklist (for each ticket)

- UX behavior is visible in both Orders and Assets where applicable
- Keyboard path is fully usable (Tab, Enter, Space, Escape where relevant)
- No regressions in existing API calls or payload shapes
- Empty, loading, and error states are still represented
- Dense desktop data views remain performant and readable
- Playwright smoke paths still pass for navigation and orders

## Verification Steps (UX-14 and UX-16)

### UX-14: Status indicators include color and text

1. Run npm run build in src/OF.Frontend.
2. Open /orders in Table view and confirm each Status cell shows a colored dot plus readable text label.
3. Switch /orders to Timeline view and confirm the Status column also shows dot plus text label.
4. Open /assets in Table view and confirm the Status column shows dot plus text label.
5. Pass criteria: status is understandable without relying on color alone in both Orders and Assets.

### UX-16: --ref-colour-grey10 token is defined and used safely

1. Open any order detail page, select a line, and open AssetSelector with + Reserve.
2. In the AssetSelector header, confirm the warehouse/division context hint chip has a visible light-grey background.
3. In AssetSelector filters, hover Clear and confirm hover background appears with no missing-style/fallback glitch.
4. Optional source check: verify --ref-colour-grey10 is defined in theme.css and used with fallback in AssetSelector.module.css and AssetFilterBar.module.css.
5. Pass criteria: no undefined-token behavior is visible in AssetSelector/filter controls.

## Notes from this review pass

- The current frontend already has a strong foundation for business workflows.
- The highest return now is in interaction reliability, operational clarity, and keyboard efficiency.
- Most items above are UI-only and can be delivered without backend changes.
- UX-09 is now implemented in the Assets table workflow with a sticky selected-assets summary/action bar.
- UX-12 is now implemented in Orders and Assets table-loading states using a shared TableSkeleton component.
- UX-13 is now implemented in AssetSelector with Escape-to-close, focus trap, dialog accessibility semantics, and focus restoration to the reserve trigger after close.
- UX-14 is now implemented in Orders and Assets list views with combined status dot plus text labels for clearer status interpretation.
- UX-16 is now implemented by defining --ref-colour-grey10 in theme tokens and applying safe fallbacks where it is consumed.
