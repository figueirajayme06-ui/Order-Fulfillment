# Feature specification: Workspace dark mode

## 1. Objective

**User and job:** Order Fulfillment users working across Agreements, Assets, Ringfence, Admin, detail, and timeline pages need a comfortable alternative to the current light work surfaces.

**Problem:** Workspace pages currently use a fixed light colour scheme, which can be uncomfortable in low-light environments and cannot be changed by the user.

**Outcome:** Every workspace page presents a consistent top-right dark-mode switch. The selected appearance applies across routes and is restored on later visits.

## 2. Scope and boundaries

### In scope

- A shared light/dark switch in the existing workspace utility bar.
- Persistent theme selection using browser local storage.
- Dark semantic colour tokens for page backgrounds, surfaces, controls, tables, timelines, statuses, and text.
- Accessible, responsive, reduced-motion, and print behaviour.

### Out of scope

- Per-page theme settings.
- Server-side user preference storage or API changes.
- Redesigning workspace layouts or changing brand colours.
- A switch on the already-dark homepage.

### Constraints and known rules

- Reuse `theme.css`, CSS Modules, the existing utility bar, and installed icon components.
- Maintain WCAG 2.2 AA contrast and the existing visible focus treatment.
- Preserve light print output regardless of the active screen theme.
- Do not alter API contracts, dependencies, CI, or deployment.

## 3. User experience

### Primary flow

1. The user opens any workspace route and finds the dark-mode switch at the right of the shared utility bar.
2. Activating the switch immediately changes all workspace surfaces to dark mode.
3. Navigating between workspace pages retains the selection.
4. Returning later restores the last valid selection.

### Information hierarchy

- The switch remains visible with the other global workspace actions.
- The control uses a compact label, sun/moon cues, and a conventional switch track without competing with primary workflow actions.

### States and edge cases

- Loading: the stored theme is applied by the shared layout while authentication loads.
- Error: unavailable local storage does not block theme switching for the current session.
- Success/confirmation: the screen changes immediately; no toast is needed.
- Invalid stored value: fall back to light mode.

### Accessibility and responsive behaviour

- Keyboard/focus behaviour: use a native button with `role="switch"`, `aria-checked`, and the global focus ring.
- Accessible names/status treatment: the fixed translated label names the setting and `aria-checked` communicates its state.
- Mobile fallback: the utility bar may wrap while the switch remains operable and right-aligned.
- Print impact: the switch remains under `data-print-hidden`; print tokens force light output.

## 4. Technical plan

- Affected routes/pages/components: shared `AppLayout`, a new `ThemeToggle`, `theme.css`, selected page styles with hard-coded light surfaces, and locale resources.
- API/data changes: none.
- Existing patterns/components to reuse: `AppLayout.utilityBar`, CSS theme tokens, `react-i18next`, `react-icons`, and local-storage error handling used by the sidebar preference.
- Proposed tasks, each independently testable:
  1. Add persistent theme state to `AppLayout` and apply `data-theme` to the document root.
  2. Add and test the accessible shared switch in the workspace utility bar.
  3. Add dark token values and replace key hard-coded light surfaces with theme-aware tokens.
  4. Verify component behaviour, all frontend tests, build, lint, formatting, and print rules.

## 5. Acceptance criteria

- [x] A dark-mode switch is visible in the top-right utility area on every non-home route.
- [x] Mouse and keyboard activation toggle the complete workspace between light and dark modes.
- [x] The control exposes an accessible name and checked state.
- [x] The selected mode persists across route changes and reloads when local storage is available.
- [x] Tables, cards, forms, dropdowns, status treatments, and timelines remain legible in both modes.
- [x] The homepage and printed pages retain their intended appearance.

## 6. Verification

- Focused tests: `ThemeToggle` interaction and `AppLayout` persistence/application behaviour.
- Build/lint/format commands: `npm test`, `npm run build`, `npm run lint`, and targeted Prettier checks.
- Manual visual checks: Agreements, Assets, Ringfence, Admin, detail/timeline, responsive utility bar, keyboard focus, and print preview.

## 7. Decisions and open questions

| Item            | Decision or question                                     | Owner / resolution                  |
| --------------- | -------------------------------------------------------- | ----------------------------------- |
| Initial mode    | Default to light unless a valid stored selection exists. | Implemented behaviour               |
| Switch location | Far right of the existing workspace utility bar.         | Initial placement requested by user |
