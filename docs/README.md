# Order Fulfillment documentation

This is the starting point for developers and coding agents. It separates the current application guidance from integration, build, and historical material.

## Choose the document for the task

| If you are changing...                                      | Read first                                                                                                                  |
| ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| A user-facing React workflow                                | [Frontend UX and design guide](frontend/UX-DESIGN-GUIDE.md) and [`src/OF.Frontend/AGENTS.md`](../src/OF.Frontend/AGENTS.md) |
| Frontend tests or release verification                      | [Frontend testing and verification](frontend/TESTING.md)                                                                    |
| A React-facing API endpoint, query, response, or saved view | [Web API and persisted views contract](api/WEB-API-CONTRACT.md)                                                             |
| A cross-page workflow, API contract, or data change         | [Feature specification template](specs/FEATURE-SPEC-TEMPLATE.md)                                                            |
| Local startup, authentication, data, or emulator issues     | [Local development runbook](development/LOCAL-DEVELOPMENT.md)                                                               |
| Application boundaries or the current/legacy split          | [Current architecture](architecture/CURRENT-ARCHITECTURE.md)                                                                |
| Integration data flows, deployment, or infrastructure       | [Operational and integration documentation](#operational-and-integration-documentation)                                     |

## Current product guidance

- [Current architecture](architecture/CURRENT-ARCHITECTURE.md)
- [Frontend UX and design guide](frontend/UX-DESIGN-GUIDE.md)
- [Frontend testing and verification](frontend/TESTING.md)
- [Frontend maintainability review and cleanup results](frontend/CODEBASE-CLEANUP-PLAN.md)
- [Web API and persisted views contract](api/WEB-API-CONTRACT.md)
- [Local development runbook](development/LOCAL-DEVELOPMENT.md)
- [Feature specification template](specs/FEATURE-SPEC-TEMPLATE.md)
- [September 2026 frontend enhancement roadmap](specs/frontend-enhancements-roadmap.md)
- [OF UI parity roadmap and parallel agent work packages](specs/of-ui-parity-roadmap.md)
- [Configurable timeline context columns](specs/configurable-timeline-columns.md)
- [Reusable data-grid foundation](specs/reusable-data-grid-foundation.md)
- [Grid filtering and deployment resilience roadmap](specs/grid-filter-and-deployment-resilience-roadmap.md)
- [Agreement availability workbench](specs/agreement-availability-workbench.md)
- [Agreement bulk fulfilment](specs/agreement-bulk-fulfilment.md)
- [Add operational equipment](specs/add-operational-equipment.md)
- [Availability divisions and location filters](specs/availability-stock-areas.md)
- [Date-aware serialized asset availability](specs/date-aware-serialized-asset-availability.md)
- [Reservation clash notifications](specs/reservation-clash-notifications.md)
- [Future task: remove Change Orders](specs/remove-change-orders.md)
- [NOF frontend planning enhancements](specs/nof-frontend-planning-enhancements.md)

## Operational and integration documentation

`build/` is intentionally retained as the home of integration, build, and infrastructure documentation. It is referenced by existing links and automation, so it should not be moved as part of normal feature work.

- [Build documentation index](../build/docs/README.md)
- [Integration and process design](../build/docs/design/README.md)
- [Agreement activation process](../build/docs/design/activation/README.md)
- [Build and release](../build/docs/build/README.md)
- [Local development scripts](../build/dev/README.md)
- [Infrastructure](../build/infra/README.md)
- [Kubernetes deployment](../build/k8s/README.md)

## Historical and planning material

These documents provide context but are not the source of truth for current frontend/API behaviour:

- [`docs/planning-scripts`](planning-scripts/)
- [`docs/migration-scripts`](migration-scripts/)
- [`src/OF.UI`](../src/OF.UI/) legacy UI implementation

When a historical document disagrees with the current architecture, UX guide, or API contract, update or annotate the historical document rather than implementing the outdated behaviour.
