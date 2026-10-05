# OF.Frontend working agreement

Read [the frontend UX and design guide](../../docs/frontend/UX-DESIGN-GUIDE.md) before changing a user-facing workflow.

- This is dense, desktop-first operational software for fleet planners, sales, and warehouse users. Prioritise
  predictable scanning and action over decorative novelty.
- Reuse `theme.css`, CSS Modules, and shared components before adding a new pattern.
- Keep visible grid fields to decision-critical information. Use column filters for visible fields and More filters for
  additional fields only.
- Use native semantic controls, visible focus, accessible names, keyboard paths, and labelled status—not colour alone.
- Use `react-i18next` for new visible text and the shared print stylesheet/data attributes for printable pages.
- Follow KISS, DRY, and YAGNI: extract a shared component only after a pattern is proven across at least three
  independently changing places.
- For cross-page, API/data-contract, or new workflow work, create a feature spec from
  [the template](../../docs/specs/FEATURE-SPEC-TEMPLATE.md) before implementation. Small isolated changes do not need a
  formal spec.
- Use [frontend testing and verification](../../docs/frontend/TESTING.md) for the appropriate test and manual-review
  matrix. Always run focused tests and `npm run build`; use `npm run lint` and `npm run format` when the change is
  broad. Visually check grid/timeline/print impact where relevant.
- Ask before adding dependencies, changing API/data contracts, or altering CI/deployment. Never commit secrets/local
  runtime data, edit vendor files, or remove a failing test merely to make a check pass.
