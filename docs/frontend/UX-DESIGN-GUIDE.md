# Order Fulfillment frontend UX and design guide

> **Status:** Living guidance for the React frontend. Update it when a user-tested pattern changes.

## Purpose

Order Fulfillment is a desktop-first operational application for fleet planners, sales representatives, and warehouse users. People use it to scan large data sets, identify exceptions, reserve assets, and take action quickly.

The interface should therefore feel calm, dense, predictable, and trustworthy. Density is useful; duplicated controls, decorative treatment, and ambiguous status are not.

This guide is the project source of truth for new frontend work. It complements, but overrides where necessary, generic design guidance and external AI skills.

## Decision hierarchy

When guidance conflicts, use this order:

1. Validated user need, business rules, and accessibility requirements.
2. This guide and an established product pattern.
3. The shared components and tokens in `src/OF.Frontend/src`.
4. Aggreko brand guidance referenced by `theme.css`.
5. Generic design guidance or external libraries.

Do not copy a legacy screen literally. Preserve familiar concepts where they help migration, but make the new workflow clearer and simpler.

## Principles

- **Operational clarity first.** Make the next decision or action obvious.
- **Show the essential; reveal the rest.** Keep high-frequency comparison data visible. Put lower-frequency context in expanded details, a profile, or More filters.
- **One control, one job.** Do not repeat a filter, status, or action in multiple places without a clear reason.
- **Keep context while users work.** IDs, current filters, period, and selection should remain understandable while scrolling, filtering, or changing view.
- **Be consistent before being novel.** A new visual treatment needs a real workflow benefit, not just visual variety.
- **KISS, DRY, and YAGNI.** Reuse a proven pattern; extract a shared component only after it has appeared in at least three independently changing places.
- **Native capability first.** Use the supplied control and API of the browser, shared component, or adopted library before adding custom behaviour. Add a custom solution only when the native capability cannot meet a documented workflow need.

## Operational scale assumptions

- An agreement commonly contains hundreds of lines and can, in rare cases, contain more than 1,000 lines.
- A single agreement line can have a quantity greater than one and, in exceptional cases, up to approximately 1,000 units.
- Design, implementation, and verification must use representative large data sets. A workflow that is usable only with a short fixture is not complete.
- Do not place controls or decision-critical context after an unbounded collection when they are needed to act on an item in that collection. Keep them visible through a bounded scrolling region, persistent inspector, or similarly scalable pattern.
- Keep line count, unit quantity, fulfilled/reserved quantity, and selection count distinct. Do not assume that one line represents one asset or one reservation.

## Foundations and implementation

### Tokens and visual language

- Use the tokens in `src/OF.Frontend/src/theme.css` for colour, typography, spacing, radius, shadow, and motion.
- Preserve Light as the default appearance. Dark and ATS theme are persisted user choices; ATS theme maps shared-library
  colours and Montserrat typography through the existing semantic tokens rather than page-specific overrides.
- Use semantic tokens such as `--colour-success` and `--colour-error`; do not introduce direct hex values for normal application UI.
- Hard-coded colour is acceptable only for documented third-party overrides or a genuinely one-off data visualisation treatment.
- Use **Inter Tight** for interface and data text. Use **Space Grotesk** for headings only.
- The visual baseline is an off-white page, white work surfaces, restrained borders/shadows, and orange for primary actions, selected context, key links, and focus. Orange is not decoration.
- Use CSS Modules for component/page styling. Keep selectors local and avoid specificity battles.

### Shared components

Prefer the existing common components before creating a new control:

- `Button` for normal actions.
- `Badge` for labelled semantic status.
- `Card` for contained summary/context.
- `Alert`, `Spinner`, and `TableSkeleton` for feedback/loading.
- `AdvancedFilters` for optional, non-grid filters.
- `TableColumnHeader` and `TableColumnFilter` for sortable/filterable grid columns.

Use a small page-specific style when the interaction is truly specific to a table or timeline. Do not build a highly configurable generic component for a single page.

## Page hierarchy

1. Identify the page or record clearly.
2. Put record-level actions close to the record title.
3. Show persistent, high-value controls before optional controls.
4. Put the primary work surface next: grid, timeline, availability, or form.
5. Keep supporting context close to the action that needs it.

Avoid using page headers as overflow space. A status belongs with the object it describes; a control belongs near the object it changes.

## Tables and filters

Tables are the primary working surface for Agreements and Assets.

- Initial columns must support a decision users make across most rows. Ask: _would a planner or sales user need to compare this without opening a row?_
- Put contextual, exceptional, or low-frequency data in row expansion, an asset profile, or More filters.
- Use a combined cell only when the values form one useful scanning concept, such as customer/agreement or warehouse/location.
- Visible fields receive their filter and sort affordance at column level.
- More filters contains additional fields only. Do not duplicate a visible column filter there.
- Show active optional-filter count and give users a clear way to remove an individual filter or clear all.
- A sort label must communicate the active direction. Do not make static labels look interactive.
- Keep status readable with text and an icon/colour; colour alone is insufficient.
- Preserve the current filter/sort/view state through a saved view where supported.

## Timelines

- The left panel provides only enough context to identify the row and make a planning decision.
- The left/table panel must be resizable; users choose between row context and time detail.
- Always label the current visible period. Earlier and Later navigate the current period rather than resetting the timeline to a new arbitrary range.
- Keep period navigation together, outside expandable filter content.
- Use a visible legend for event/status colours.
- Do not render a placeholder event for an Available asset with no scheduled event. The asset remains visible; its timeline row stays empty.
- Treat timeline bars as concise identifiers. Do not duplicate their information in the left panel unless the comparison requires it.

## Fulfilment and reservations

- The agreement header shows aggregate agreement context and state.
- The lines grid shows line-specific fulfilment; label it as line fulfilment where ambiguity is possible.
- Keep reservation identifiers inline with each line as a distinct, labelled value. Repeat them with the management controls in the selected line's availability header, where the reservation and availability decisions are made together.
- A line can have a large quantity and consequently dozens or hundreds of reservation records. Neither the line card nor availability header may scale with that count: use the available width to show at most two compact identifiers side by side, followed by the additional count.
- Every visible reservation pill in the selected-line availability header includes a compact remove control and removal remains confirmation-protected. Use `+N more` only when records are hidden; do not add a separate `Manage` action when all records are already visible.
- Expanded reservation lists must remain height-bounded, scroll independently, and be limited to one expanded line at a time. Printed line grids show the first two reservations plus the additional count; a full reservation manifest belongs in a separate print/export workflow.
- Keep availability directly below the line/reservation context it serves.
- For large agreements, keep the selected line and its availability in the same viewport. Do not append availability after the full, unbounded lines grid.
- When the agreement warehouse/division must guide selection, use a subtle full-column highlight and a clear label. Do not use alarming borders or colour as the only cue.
- Confirm destructive reservation actions and make the result clear.

## Navigation and layout

- Desktop users need a stable, compact sidebar; it may collapse but must retain accessible labels/tooltips.
- The user indicator is identity only: initials/avatar and name. Do not show technical context such as the raw division list there.
- Mobile navigation is a supported fallback, not a competing primary layout.
- Use responsive layout to preserve tasks, not to force every desktop data grid into a narrow screen.

## Content, localisation, and status

- Use sentence case and plain operational language.
- Name controls for the action users recognise: `Save view`, `Reserve`, `Clear all`, `Print`.
- Keep action names stable across the flow and its feedback message.
- New visible copy goes through `react-i18next`; do not introduce hard-coded user-facing text without a reason.
- Empty and error states should explain what happened and the useful next action. They should not be vague or apologetic.

## Accessibility and interaction

Target WCAG 2.2 AA. The local standard is intentionally practical:

- Use native buttons, links, inputs, and selects before custom roles.
- Every interactive control has a visible or programmatic name.
- Keyboard users can reach, operate, and leave every interactive feature. Use `Escape` for transient panels/dialogues where appropriate.
- Do not remove the global visible focus treatment.
- Do not rely on colour alone for state or availability.
- Maintain logical focus order and avoid obscuring focused controls.
- Respect `prefers-reduced-motion`.
- Test empty, loading, success, error, and disabled states when the feature has them.

See the [WCAG 2.2 quick reference](https://www.w3.org/WAI/WCAG22/quickref/) for the underlying standard.

## Print

Printing is a supported workflow, not a screenshot feature.

- Use the shared hooks in `src/OF.Frontend/src/print.css`.
- Hide navigation and interactive-only controls with the existing print data attributes.
- Table and timeline changes require a print check, including readable columns and sensible landscape output where relevant.
- Do not create page-specific print rules if the shared print stylesheet can express the requirement.

## Designing a new change

For work larger than a minor visual correction, write a short design note in the ticket or pull request before implementation:

1. **User and job:** who is using this, and what do they need to decide or do?
2. **Existing pattern:** which current page/component is the closest precedent?
3. **Information hierarchy:** what stays visible, what is progressive disclosure, and why?
4. **States:** loading, empty, error, selected, disabled, and successful outcome as relevant.
5. **Interaction:** keyboard behaviour, filters/sorting, destructive action handling, and responsive/print impact.
6. **Acceptance criteria:** observable outcomes a business user can validate.

Use screenshots or a small wireframe only where they clarify a meaningful layout decision. Do not make a specification more elaborate than the change.

## Specification-driven delivery

Use a specification that is proportional to the change. The purpose is to make the intended outcome, constraints, and verification unambiguous before implementation—not to create paperwork.

| Change size                                           | Required approach                                                                                                                                                        |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Small, isolated correction                            | A clear task statement, affected screen, constraint, and observable outcome are sufficient.                                                                              |
| Medium feature or workflow change                     | Add the six-point design note above to the ticket or pull request before implementation.                                                                                 |
| Cross-page, API/data-contract, or new workflow change | Create `docs/specs/<feature-name>.md` from [the feature-spec template](../specs/FEATURE-SPEC-TEMPLATE.md), review it, then plan and implement in small verifiable steps. |

For a non-trivial feature, follow this sequence:

1. **Specify:** document the user need, success criteria, constraints, exclusions, and known business rules.
2. **Plan:** inspect the relevant code in read-only mode and propose the smallest implementation plan. Resolve material ambiguity before changing code.
3. **Implement:** complete independently testable tasks, keeping the spec in scope rather than sending every project document as context.
4. **Verify:** compare the result to the acceptance criteria, run the specified checks, and perform the relevant visual review.
5. **Learn:** update the spec or this guide if a user-tested decision changes the shared pattern.

Feature specs must state their boundaries explicitly:

- **Always:** reuse the established frontend patterns, run relevant tests, and preserve accessibility/print behaviour.
- **Ask first:** add dependencies, change a database schema or API contract, alter CI/deployment, or introduce a new cross-page pattern.
- **Never:** commit secrets or local runtime data, edit generated/vendor files, or remove a failing test merely to make a check pass.

The template deliberately includes non-goals and acceptance criteria. They prevent scope drift and make human review meaningful.

## Definition of done

Before handing over frontend work:

- Reuse of tokens and shared components has been considered.
- Keyboard, focus, visible labels, and status clarity have been checked.
- Relevant empty/loading/error states are represented.
- Grid, timeline, saved-view, and print impact have been checked where applicable.
- New visible copy is localised.
- Focused tests and `npm run build` pass.
- The changed desktop workflow has had a visual review; review mobile fallback when the layout/navigation changes.
