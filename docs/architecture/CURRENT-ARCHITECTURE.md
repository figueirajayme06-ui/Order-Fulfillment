# Current application architecture

## Purpose

This is the current architecture map for the user-facing Order Fulfillment application. It is deliberately concise; integration, infrastructure, and deployment detail remains in [`build/docs`](../../build/docs/README.md).

## Current path

```text
Browser
  └─ OF.Frontend (React 19, TypeScript, Vite)
       ├─ development: Vite proxy forwards /api to OF.WebApp
       └─ production: static SPA served by OF.WebApp
  └─ OF.WebApp (ASP.NET Core 10 REST API and SPA host)
       └─ OF.Common / OF.UI.Shared / OF.Data
            └─ SQL data, Azure Storage, Service Bus, and integration data
```

## Application ownership

| Area | Location | Role |
| --- | --- | --- |
| Current frontend | `src/OF.Frontend` | React application for Agreements, Assets, fulfilment, Ringfence, Admin, and timelines. |
| Current web/API host | `src/OF.WebApp` | ASP.NET Core REST controllers, EasyAuth integration, and production SPA host. |
| Web/API feature modules | `src/OF.WebApp/Features` | Narrow feature-local access policies, explicit response contracts/mapping, builders, and synthesizers used by controllers. |
| Shared domain/business logic | `src/OF.Common` | Reusable domain logic and fulfilment engine. |
| Shared UI data access/models | `src/OF.UI.Shared` | Shared repository/data-access types used by the current API. |
| Database model and schema | `src/OF.Data`, `src/OF.Data.Design` | EF/data model and SQL project. `OF.Data.Design` is the sole deployment owner for shared-database schema and programmable objects. |
| Legacy UI | `src/OF.UI` | ASP.NET/Infragistics implementation retained as migration reference. New UI work belongs in `OF.Frontend` unless explicitly requested otherwise. |
| Background integrations | `src/OF.Data.*`, `src/OF.Api` | Data ingestion and message-processing services. |

## Core boundaries

- The React frontend calls `/api/*` through typed service modules in `src/OF.Frontend/src/services`.
- `OF.WebApp` owns the React-facing contract. Controller/database shapes must not be treated as an accidental frontend contract; document intentional changes in the [Web API contract](../api/WEB-API-CONTRACT.md).
- `OF.WebApp/Controllers` owns HTTP routes, authentication/status outcomes, query orchestration, and mutation sequencing. `OF.WebApp/Features` owns only proven feature-local seams: Agreement and division access policies; Agreement, Asset, Ringfence, and saved-view response mapping; Asset profile schedule building; and Asset timeline fallback synthesis.
- Feature modules stay specific to their endpoint family. They do not introduce a generic controller/query/mapping framework, take over persistence from `OF.Data`/`OF.UI.Shared`, or move EF-sensitive `IQueryable` projections merely to shorten a controller.
- `OF.Common` owns shared fulfilment behaviour. Changes to reservation, rehire, depot fulfilment, or activation need their business rules and side effects considered explicitly.
- `OF.Data.Design` is the single deployable source of truth for database procedures and functions. The availability boundary is deliberately split: NOF calls `dbo.GetFulfilmentAvailabilitySummary` and its `dbo.GetFulfilmentGenericsWithSubstitutions` helper, while `dbo.GetCPQAvailabilitySummary` and `dbo.GetGenericsWithSubstitutions` remain CPQ Next-only compatibility contracts. Do not deploy duplicate definitions from another application project or repository.
- Production authentication is Azure App Service EasyAuth. Development uses the fallback described in the [local development runbook](../development/LOCAL-DEVELOPMENT.md).
- The legacy UI is useful for understanding historical workflow behaviour, but it is not the visual or component source of truth for the React app.

## Current documentation ownership

| Topic | Source of truth |
| --- | --- |
| User-facing frontend patterns | [Frontend UX and design guide](../frontend/UX-DESIGN-GUIDE.md) |
| React-facing endpoint and saved-view compatibility | [Web API and persisted views contract](../api/WEB-API-CONTRACT.md) |
| Local setup and troubleshooting | [Local development runbook](../development/LOCAL-DEVELOPMENT.md) |
| New workflow requirements | [Feature specification template](../specs/FEATURE-SPEC-TEMPLATE.md) |
| Integration data flows / activation design | [`build/docs/design`](../../build/docs/design/README.md) |
| Build, release, and infrastructure | [`build/docs/build`](../../build/docs/build/README.md), [`build/infra`](../../build/infra/README.md), and [`build/k8s`](../../build/k8s/README.md) |

## Documentation migration policy

Do not relocate `build/docs` as incidental cleanup. Its paths are used by root documentation, existing cross-links, agent guidance, and pipeline actions that publish/write design assets. A future migration should be a dedicated, tested change covering links and build automation.
