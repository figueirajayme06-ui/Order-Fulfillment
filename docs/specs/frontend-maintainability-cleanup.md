# Frontend maintainability cleanup

## Objective

Help human developers change the existing operational workflows without understanding unrelated state or updating
duplicate implementations. Follow the priority order in [the cleanup plan](../frontend/CODEBASE-CLEANUP-PLAN.md).

## Scope and constraints

Correct local check configuration, repair deterministic browser regression tests, reuse existing saved-view controls,
consolidate identical saved-view rules, and decompose the Ringfence, Availability, Agreement Detail, and Assets workflows.
Keep API contracts, storage keys/migrations, authorization, quantities, date semantics, and request-order protections.
No new dependencies, API changes, CI/deployment changes, visual redesign, or generic application framework.

## User experience

Planners and warehouse users keep the same information hierarchy, filters, selection, confirmations, navigation, and print
output. Loading, empty, error, success, and read-only states remain supported. Preserve keyboard paths, focus restoration,
accessible names, mobile fallback, and bounded large-data surfaces. Reuse native controls and existing components.

## Implementation and acceptance

1. Make lint/format inspect maintained source only; keep mechanical changes distinguishable from extraction.
2. Replace obsolete Orders browser coverage with explicit Agreements assertions and mocked authenticated API fixtures.
3. Give common saved-view presentation and behavior one owner while keeping page-specific parsers and migration explicit.
4. Extract local models/components/hooks by responsibility, preserving markup and behavior; avoid giant controller hooks.
5. Simplify touched expressions, localize visible copy, and remove only proven unused code.

Done means relevant regression tests, build, lint, and format pass, and affected browser/print surfaces have been checked.
Use existing page/hook suites to verify behavior, adding tests only for meaningful uncovered boundaries. Record actual
checks and any limitations in the cleanup plan. Runtime fixtures never mutate real agreements or assets.
