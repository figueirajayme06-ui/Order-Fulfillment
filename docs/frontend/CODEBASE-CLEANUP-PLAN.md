# Frontend maintainability cleanup plan

Reviewed 29 September 2026. Scope: `src/OF.Frontend`. The findings below record the initial sampled review;
implementation results follow at the end. This is not a full correctness audit.

The existing services, pure model helpers, CSS Modules, common controls, and colocated tests provide a useful foundation.
The highest-value work is reducing the number of responsibilities developers must understand at once and giving repeated
behavior a clear owner. A new framework or directory-wide rewrite is unnecessary.

## Findings

Paths below are relative to `src/OF.Frontend`.

| Priority | Evidence | Maintenance impact |
| --- | --- | --- |
| High | `src/pages/ringfence/RingfencePage.tsx` has 2,112 lines combining persisted state parsing, list/detail loading, forms, asset entry, confirmations, and rendering. | A small workflow change requires understanding unrelated state and effects. |
| High | `src/components/fulfilment/AvailabilityPanel.tsx` has 1,685 lines; `src/pages/agreements/AgreementDetailPage.tsx` has 1,666; `src/pages/assets/AssetsPage.tsx` has 1,361. | Fetching, business decisions, interaction state, and substantial JSX are colocated. Size is a signal, not a reason to split arbitrarily. |
| High | `src/pages/assets/useAssetSavedViews.ts` and `src/pages/agreements/useAgreementSavedViews.ts` repeat selection, sharing, recipient search, dirty checking, feedback, and mutation handling. | Fixes must be maintained twice. Differences already include initialization/error handling and legacy selection-key migration; these must be understood before consolidation. |
| Medium | `package.json` lint/format scripts reference `../.gitignore`, but `src/.gitignore` does not exist. The frontend's own `.gitignore` excludes `dist/`. | The formatting check currently includes generated build output, making the cleanup noisy and potentially rewriting artifacts. |
| Medium | Saved-view input styles repeat in `AssetsPage.module.css`, `AgreementsPage.module.css`, and `components/common/SavedViewControls/SavedViewControls.module.css`. Page wrappers pass six style overrides. | The existing shared control does not fully own its presentation; fixes require checking several copies. Some spacing/responsive differences are intentional. |
| Medium | `e2e/orders.spec.ts` visits `/orders`, expects Orders text, and conditionally skips the row-navigation assertion when no row exists. `src/App.tsx` defines Agreements routes instead. | These tests cannot provide dependable regression protection for cleanup. This limitation is also documented in `docs/frontend/TESTING.md`. |
| Lower | `AssetFilterBar.tsx` contains hard-coded labels and compressed option JSX; both saved-view hooks embed the default-view feedback message in English. | Formatting and localization conventions are inconsistent in actively used code. |

## Delivery plan

Deliver each item as a small, independently reviewable change. Split the workflow work into one pull request per area.

1. **Establish reliable checks and readable formatting.** Correct the ignore paths in lint and formatting scripts;
   explicitly exclude generated output, runtime/test artifacts, and vendor-owned files. Keep the existing Prettier
   configuration and dependencies. Run formatting only on maintained files, then inspect the diff for unrelated changes.
   Keep this mechanical commit separate from refactoring. Record unit-test and build baselines before behavior-sensitive work.
   Acceptance: lint and format checks pass against the intended source files and do not inspect or rewrite `dist`.

2. **Repair the regression checks that will protect the cleanup.** Update the Orders browser tests to current Agreements
   routes and labels, with deterministic authenticated fixtures and at least one known row. Assert the row exists before
   exercising navigation. Reuse the current Playwright setup; do not silently skip behavior when fixture data is absent.
   Acceptance: the test fails if the expected row or navigation is broken. Until repaired, do not report the old suite as
   release evidence. Add focused regression cases only for uncovered behavior at the boundaries being refactored.

3. **Remove duplication with an existing owner first.** Move common saved-view control styling into its existing CSS Module.
   Retain only actual page layout/responsive differences in page styles. Keep the small typed page wrappers where they
   make labels and state types clearer. For the two saved-view hooks, first document their differences and extract identical
   pure operations such as recipient comparison and selection storage. Keep migration and initialization semantics explicit.
   Consider a small typed shared hook only if it simplifies both call sites without a growing list of feature flags.
   Acceptance: each extracted rule has one implementation; ownership, sharing permissions, recipient search ordering,
   default selection, dirty state, storage migration, and errors retain their intended behavior.

4. **Split large workflows by responsibility.** Start with Ringfence: move state parsing/validation and list transformations
   into local model files, then separate the form, asset list/entry, and confirmation rendering. Keep coordination in the
   page. Next separate Availability's commitment schedule, stock allocation, filters, and data loading along their existing
   boundaries; reuse `availabilityGrid.ts` and `availabilitySchedule.ts`. Follow with Agreement Detail's line/reservation
   rendering and action handling, then Assets' query/view-state coordination and ringfence actions. Prefer local components
   and narrowly scoped hooks; avoid replacing one large page with one equally large controller hook.
   Acceptance: each extracted unit has a clear responsibility and explicit inputs; reviewers can follow a user action
   without tracing unrelated workflow state. There is no arbitrary file-length target.

5. **Finish targeted readability and unused-code cleanup.** Use descriptive business names, named predicates for complex
   conditions, early returns, and short handlers. Keep comments explaining business constraints and ordering requirements.
   Remove unused code/styles/exports only after checking references, dynamic CSS usage, tests, and runtime behavior.
   Localize touched visible text across all supported languages. Preserve request-order guards and memoization supporting
   large datasets unless evidence shows they are unnecessary. Acceptance: no speculative utilities, dead compatibility
   branches, or unexplained dense expressions are introduced; any removal has evidence.

## Boundaries and verification

- Follow the local rule: introduce a new shared UI component only after a pattern is established in three independently
  changing places. Page-local decomposition and improving an existing shared component do not require a generic UI layer.
- Keep current API contracts, routes, persisted-state versions, authentication, and business rules stable. Legacy saved-view
  migration is not dead code merely because it is old. Do not remove it without an explicit retirement decision.
- Add no dependencies, state-management framework, universal form engine, or speculative generic repository/service layer.
- For each component/model extraction, run the relevant existing tests and `npm run build`. For shared logic, also run
  `npm test`; for broad changes, run `npm run lint` and `npm run format`.
- Manually verify affected desktop flows, keyboard/focus behavior, loading/error/empty states, saved views, and print.
  Availability/reservation changes must preserve confirmation, quantity semantics, request ordering, and large-agreement
  usability. Consult the existing testing and UX guides for the full applicable matrix.
- Keep formatting, behavior-preserving extraction, and intentional behavior fixes in separate commits so human reviewers
  can see what changed and roll back a single step.

Review checks: `npm run lint` returned exit code 0. `npm run format` failed on 24 files, including three generated `dist`
files. The absent ignore file means the lint scope should be corrected and rechecked before treating that result as a clean
baseline. Unit tests, production build, browser tests, and visual checks were not run for this documentation-only review.

## Implementation results — 29 September 2026

The cleanup followed the delivery order above, without new dependencies, API/storage changes, or CI/deployment changes.

1. Corrected lint/format ignore paths, excluded generated/test/runtime output, and retained the upstream Gantt stylesheet.
   Formatted maintained files using the existing Prettier configuration. The implementation baseline was 564 passing
   unit/component tests and a passing production build.
2. Replaced the obsolete Orders browser tests with deterministic Agreements coverage. Navigation and parity tests share
   authenticated fixtures. Fixed missing navigation translations exposed by the browser tests. Added regression coverage
   for extracted Ringfence/Assets and Availability interactions, including native dialog cancellation and keyboard search.
3. Replaced the two saved-view implementations with `hooks/useSavedViews.ts` and small page-specific adapters. Parsers,
   storage keys, and the legacy Orders selection migration remain explicit. Both pages now use the existing Assets
   initialization/error handling. Shared control styles own common input/feedback rules; pages retain layout differences.
4. Extracted Ringfence models, its editor, asset section, and native confirmation dialog. Separated Availability data loading,
   filters, details, commitment schedule, and stock form. Extracted Agreement Detail's model, line rows, summary helpers,
   and resize hook. Extracted Assets' saved-state conversion, URL context, ringfence action hook, and native overlap dialog.
   Page coordination remains explicit; no generic workflow engine was introduced.
5. Reused one ringfence batch-error decoder. Replaced repeated asset-search Enter handlers with native form submission and
   associated labels with inputs. Used `URLSearchParams` for the Ringfence-to-Assets URL. Localized saved-view feedback and
   asset-filter text in all five languages, removed unused style overrides, and organized imports with TypeScript's built-in
   tooling. Request-order guards, memoization, confirmation, and migration behavior were retained.

Maintenance guidance: keep local business rules in the new model files; let the page coordinate workflows and persistence.
Use the existing regression suites when changing these boundaries. Continue extracting only when a responsibility can be
named clearly; the remaining page length alone is not a reason to create additional wrappers or configuration layers.

The principal files are smaller without imposing a file-length limit:

| File | Before | After |
| --- | ---: | ---: |
| `RingfencePage.tsx` | 2,112 | 1,246 |
| `AvailabilityPanel.tsx` | 1,685 | 844 |
| `AgreementDetailPage.tsx` | 1,666 | 1,166 |
| `AssetsPage.tsx` | 1,361 | 1,031 |

Visual review also exposed two print constraints: the hidden sidebar left the main content in its 240px grid track, and
the selected-line pane retained its desktop zero-width flex sizing. The shared print layout now uses normal block flow,
and Agreement Detail releases the pane width when printing. Browser assertions check both main-content and table widths.

Verification: 566 tests across 72 unit/component suites passed, plus all 9 Playwright checks using installed Edge. The
7 focused AppLayout tests and the availability/print browser regression passed after the print corrections. Production
build, lint, formatting, and whitespace checks pass. Desktop screenshots for Ringfence, Assets, Availability, and asset
selection were inspected, along with Assets and Agreement print-media output.

Limits: browser checks use mocked API responses and small deterministic fixtures. They do not certify real-backend
integration, large-data performance, or physical print pagination. The existing production bundle-size warning remains;
no bundling/deployment changes were made. The pre-existing isolated i18n test-setup warning also remains.
