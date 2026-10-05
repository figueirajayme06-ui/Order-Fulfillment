# Order Fulfillment repository guidance

Start with [the documentation hub](docs/README.md). It identifies the current application, the source of truth for each area, and historical/operational material that remains in `build/`.

- `src/OF.Frontend` is the current user-facing React application. Read its local `AGENTS.md` and the frontend UX guide before user-facing changes.
- `src/OF.WebApp` is the current React-facing API and production SPA host. Read the Web API contract before changing a controller or frontend service.
- `src/OF.UI` is the legacy UI/reference implementation. Do not add new UI work there unless explicitly requested.
- Keep integration, build, and deployment documents in `build/` where their existing links and automation expect them.
- Do not commit secrets, Azurite/runtime state, generated vendor output, or machine-specific configuration.

