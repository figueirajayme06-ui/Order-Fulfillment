# Feature specification: Reusable data-grid foundation

## 1. Objective

**User and job:** Product teams building dense operational React screens need to reuse the proven Order Fulfillment grid
behaviour without copying Agreement- or Asset-specific rendering.

**Problem:** Column layout mechanics are generic, but domain catalogues share the same module and each of the four grid
surfaces repeats structural table markup. Styling, localisation, filters, data loading, and saved-view persistence are not
yet separated clearly enough for future cross-application reuse.

**Outcome:** An internal shared grid foundation owns framework-level structure and column mechanics while each feature
continues to own its data, labels, filters, cells, business actions, persistence, and timeline composition.

## 2. Scope and boundaries

### In scope

- Agreement and Asset list tables.
- Agreement and Asset timeline context grids.
- A generic column catalogue/layout core with no Agreement or Asset definitions.
- Shared React primitives for column groups and other repeated semantic grid structure.
- Feature-owned column catalogues and cell/filter renderers.
- Existing responsive, accessibility, print, selection, sorting, filtering, pagination, and saved-view behaviour.

### Out of scope

- Publishing an npm package or defining its final public API.
- Adding dependencies, virtualization, server-side query orchestration, inline editing, grouping, or aggregation.
- Moving data fetching, saved-view APIs, business rules, or the timeline calendar into the grid.
- Replacing native table semantics with a custom ARIA grid.
- Visual redesign.

### Constraints

- The shared extraction must be exercised by at least three independently changing surfaces; all four current surfaces
  will use it.
- Shared code accepts rendered labels and content. It must not know Agreement/Asset fields or saved-view schemas.
- Existing CSS tokens and page presentation remain the visual contract for this phase.
- The timeline calendar remains primary and its pane constraints remain unchanged.

## 3. Technical direction

- Move Agreement and Asset catalogues and key types into feature-owned modules.
- Leave pure layout validation, repair, fit, move, resize, and print-width functions in the shared core.
- Add small generic React primitives that own repeated semantic table structure and width metadata while accepting
  feature render callbacks/children.
- Keep the current full tables and timelines as composition roots. This avoids a prop-heavy enterprise-grid abstraction
  before another application supplies real requirements.
- Export shared primitives through the common-component barrel so a future package boundary is explicit.

## 4. Acceptance criteria

- [x] The generic layout module contains no Agreement or Asset catalogue definitions.
- [x] All four current grid surfaces consume the shared semantic grid primitives.
- [x] Agreement and Asset fields, labels, filters, links, status rendering, and selection remain feature-owned.
- [x] Existing column visibility, order, width, limits, responsive fitting, and saved-view migration remain compatible.
- [x] Existing table and timeline keyboard, focus, print, hover, pagination, and calendar behaviour is unchanged.
- [x] Shared primitives have focused tests using neutral example data and labels.
- [x] Full frontend tests, lint, changed-file formatting, production build, and desktop browser review pass.

## 5. Future package seam

A future package can expose the shared layout core and React primitives with React as a peer dependency. Translation,
icons, theme tokens, persistence, and domain renderers should be supplied by consuming applications. Package extraction
should wait for a second application so its actual needs can validate the public API and theming contract.
