# Order Fulfillment Frontend Replatform Plan

## Overview

Replatform the Order Fulfillment UI from ASP.NET Core MVC + Razor + Infragistics + jQuery/KnockoutJS to a modern React 19 + TypeScript + Vite SPA, following the same architecture as CPQ-Next.

## Status: ✅ Implementation Complete

## Architecture

### Current State (OF.UI)
- ASP.NET Core MVC with Razor views
- Infragistics igGrid for data tables
- jQuery + Bootstrap + KnockoutJS
- Server-rendered pages with client-side interactivity
- Azure App Service EasyAuth for authentication
- Localization via Razor view localization (EN, FR, DE, IT, ES)

### Target State
- **Frontend**: React 19 + TypeScript 5.9 + Vite 7 SPA (`src/OF.Frontend/`)
- **Backend**: ASP.NET Core 10 REST API (`src/OF.WebApp/`)
- **Pattern**: Mirrors CPQ-Next (CPQ.Backend + CPQ.Frontend)
- **Auth**: Azure App Service EasyAuth (headers parsed by backend middleware)
- **Styling**: CSS Modules + Aggreko design tokens (Inter Tight font, orange/charcoal theme)
- **Localization**: react-i18next (EN, FR, DE, IT, ES)
- **Testing**: Vitest (unit) + Playwright (E2E)
- **Telemetry**: Application Insights (frontend + backend)

### Production Architecture
- Single Azure App Service deployment
- Backend serves React SPA from `wwwroot/` folder
- Backend routes: `/api/*` → API controllers, all other routes → SPA (index.html)
- No CORS needed — single origin

### Local Development Architecture
- Frontend: `https://localhost:5174` (Vite dev server)
- Backend: `https://localhost:7200` (ASP.NET Core)
- Vite proxy forwards `/api/*` to backend
- One-command local startup scripts available under `build/dev/`:
  - `check.ps1` validates required local tooling
  - `start.ps1` orchestrates DB mode selection (Docker/LocalDB), schema publish, Azurite, backend, and frontend startup
  - `stop.ps1` stops tracked local dev processes
- VS Code tasks available in `.vscode/tasks.json`: `Dev: Check Prereqs`, `Dev: Start All`, `Dev: Stop All`

## Project Structure

```
src/
├── OF.WebApp/                    # ASP.NET Core 10 REST API + SPA host
│   ├── Controllers/              # API controllers
│   │   ├── AuthController.cs
│   │   ├── OrdersController.cs
│   │   ├── LinesController.cs
│   │   ├── ReservationsController.cs
│   │   ├── FulfilmentController.cs
│   │   ├── AssetsController.cs
│   │   ├── RingfenceController.cs
│   │   └── AdminController.cs
│   ├── Middleware/
│   │   └── EasyAuthMiddleware.cs
│   ├── Models/
│   │   └── ApiModels.cs
│   ├── Program.cs
│   └── OF.WebApp.csproj
│
├── OF.Frontend/                  # React 19 + TypeScript + Vite
│   ├── src/
│   │   ├── components/
│   │   │   ├── common/          # Shared UI components
│   │   │   ├── orders/          # Orders dashboard
│   │   │   ├── fulfilment/      # Fulfilment workflow
│   │   │   ├── ringfence/       # Ringfence management
│   │   │   └── admin/           # Admin panel
│   │   ├── contexts/
│   │   │   └── auth/            # EasyAuth context
│   │   ├── services/
│   │   │   └── api.ts           # Axios instance + interceptors
│   │   ├── i18n/
│   │   │   ├── en.json
│   │   │   ├── fr.json
│   │   │   ├── de.json
│   │   │   ├── it.json
│   │   │   └── es.json
│   │   ├── types/
│   │   ├── hooks/
│   │   ├── App.tsx
│   │   ├── main.tsx
│   │   ├── theme.css
│   │   └── index.css
│   ├── package.json
│   ├── tsconfig.json
│   ├── vite.config.ts
│   └── index.html
│
├── OF.Common/                    # Shared business logic (unchanged)
├── OF.UI/                        # Legacy (maintained in parallel during transition)
└── OF.Api/                       # Azure Functions (unchanged)

build/
└── dev/                          # Local startup automation scripts
    ├── check.ps1
    ├── start.ps1
    ├── stop.ps1
    └── README.md

.vscode/
└── tasks.json                    # Dev: Check Prereqs / Dev: Start All / Dev: Stop All
```

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Frontend framework | React 19 + Vite + TypeScript | Matches CPQ-Next; team familiarity |
| State management | React Context + hooks | Sufficient complexity; no Redux needed |
| API client | Axios with interceptors | Same pattern as CPQ-Next |
| Styling | CSS Modules + design tokens | Component-scoped, brand-consistent |
| Data tables | Custom table components | Modern, lightweight, full control; no Infragistics |
| Localization | react-i18next | Industry standard, lazy loading, pluralisation |
| Testing | Vitest (unit) + Playwright (E2E) | Same as CPQ-Next |
| Auth | EasyAuth headers → /api/auth/me | Existing Azure AD pattern, no client-side login flow |
| Backend pattern | References OF.Common | Reuses all existing business logic without duplication |
| Local startup workflow | PowerShell orchestration + VS Code tasks | Reduces onboarding friction and manual startup errors |

## Phases

### Phase 0: Project Scaffolding ✅
- [x] 0.1: Create OF.Frontend project (React 19 + TypeScript + Vite, Aggreko theme tokens)
- [x] 0.2: Create OF.WebApp project (ASP.NET Core 10, references OF.Common)
- [x] 0.3: Configure Vite proxy for local dev (/api → backend)
- [x] 0.4: Configure EasyAuth header parsing middleware
- [x] 0.5: Set up react-i18next with EN as default + FR/DE/IT/ES support
- [x] 0.6: Set up Application Insights telemetry (frontend + backend)
- [x] 0.7: Add solution references and verify build
- [x] 0.8: Add local development startup automation (`build/dev/check.ps1`, `build/dev/start.ps1`, `build/dev/stop.ps1`) and VS Code tasks (`Dev: Check Prereqs`, `Dev: Start All`, `Dev: Stop All`)

### Phase 1: Core API Layer (OF.WebApp controllers)
- [x] 1.1: AuthController — GET /api/auth/me (returns current user from EasyAuth headers)
- [x] 1.2: OrdersController — GET /api/orders (list with division filtering, search)
- [x] 1.3: OrdersController — GET /api/orders/{id} (header detail with lines)
- [ ] 1.4: LinesController — GET /api/orders/{id}/lines, PUT (update line)
- [x] 1.5: ReservationsController — CRUD for reservations
- [x] 1.6: FulfilmentController — GET /api/fulfilment/stock/serialized, /stock/nonserialized
- [x] 1.7: AssetsController — GET /api/assets (with search/filter/timeline dates)
- [x] 1.8: RingfenceController — CRUD for ringfences
- [x] 1.9: AdminController — user management endpoints
- [x] 1.10: ActivationController — POST /api/activation/{id} (activate + cancel)

**Note:** FulfilmentEngine (SatisfyLine, Reserve, BulkAction) is currently in OF.UI and depends on
Infragistics. It needs to be extracted to a shared project (or reimplemented in OF.WebApp) before
the full fulfilment workflow API is available.

### Phase 2: Frontend — Shared Infrastructure ✅
- [x] 2.1: Auth context (read EasyAuth user from /api/auth/me)
- [x] 2.2: API service layer (axios with interceptors, error handling)
- [x] 2.3: Common components (Button, Badge, Spinner, Alert, Card)
- [x] 2.4: Layout shell (nav sidebar, header with user info, main content area)
- [x] 2.5: React Router setup (routes for all pages)
- [x] 2.6: Theme (Aggreko design tokens — same as CPQ-Next theme.css)

### Phase 3: Frontend — Orders Dashboard (replaces Task View) ✅
- [x] 3.1: Orders list page with modern table (sortable columns, clickable rows)
- [x] 3.2: Division filter (dropdown based on user's divisions)
- [x] 3.3: Date range filter
- [x] 3.4: Fulfilment status filter (Unfulfilled/Partial/Fully/Finished)
- [x] 3.5: Search (by agreement number, customer)
- [x] 3.6: Status badges with colour coding
- [x] 3.7: Pagination (25 per page with prev/next)

### Phase 4: Frontend — Order Detail & Fulfilment ✅
- [x] 4.1: Order detail page (header info card + lines table)
- [x] 4.2: Line detail panel (click row to show reservations)
- [x] 4.3: Reservation list per line with add/remove
- [x] 4.4: Asset search/selector modal for creating reservations
- [x] 4.5: Fulfilment status badge display
- [x] 4.6: Bulk actions (depot fulfil, rehire) via BulkActionsController + CoreFulfilmentEngine
- [x] 4.7: Activation workflow (activate + cancel via ActivationController + Service Bus)
- [x] 4.8: Asset selector refinement - map CPQ generic item number to M3 item number for auto-search and show warehouse/division context hint in modal

### Phase 5: Frontend — Ringfence & Admin ✅
- [x] 5.1: Ringfence list page with create/delete
- [x] 5.2: Admin — user management (list, create, delete)
- [x] 5.3: Role-based UI visibility (admin-only nav, super-admin-only delete)

### Phase 5b: Assets View & Timeline (added during implementation)
- [x] 5b.1: Assets list page with table view (sortable, paginated, filtered by status/warehouse/division)
- [x] 5b.2: Assets timeline view using Frappe Gantt (split table+chart layout)
- [x] 5b.3: Colour-coded bars by asset status (On Hire, Service, Available, etc.)
- [x] 5b.4: Actual agreement line dates (ValidFrom/ValidTo) for accurate bar positioning
- [x] 5b.5: Table/Timeline toggle with shared filters
- [x] 5b.6: Order detail timeline page (/orders/:id/timeline) with Frappe Gantt

### Phase 6: Production Readiness ✅
- [x] 6.1: Dockerfile for combined build (dotnet publish + npm build → wwwroot)
- [x] 6.2: Terraform Container App resource + EasyAuth config (webapp.tf)
- [x] 6.3: CI/CD pipeline (GitHub Actions: build backend, build frontend, Docker)
- [x] 6.4: E2E tests scaffold with Playwright (orders + navigation specs)
- [ ] 6.5: Performance testing (lighthouse, API response times) — manual activity

## Scope Exclusions

- **Change Orders view** — out of scope for this replatform; complete removal is
  tracked as a separate future feature in
  [`remove-change-orders.md`](../specs/remove-change-orders.md)
- **OF.Api Azure Functions** — stays as-is (Service Bus message processing)
- **Database schema** — no changes; new API reads from same database
- **External integrations** — IPG, Salesforce, CloudSuite integrations unchanged

## Dependencies

- OF.Common (business logic, models, data access, fulfilment engine)
- OF.Data (EF Core DbContext, entity models)
- Existing database (SQL Server — same schema)
- Azure App Service EasyAuth (authentication provider)
- Application Insights (telemetry)

## Migration Strategy

1. Deploy new app (OF.WebApp + OF.Frontend) to a **separate** App Service slot/instance
2. Run both old and new UIs in parallel pointing to same database
3. Migrate users gradually via feature flags or URL routing
4. Decommission OF.UI once all users migrated
