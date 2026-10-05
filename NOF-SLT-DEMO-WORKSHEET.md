# NOF replatform: senior leadership demo worksheet

## Purpose and decision sought

**Purpose:** demonstrate that the new Order Fulfilment (NOF) frontend gives users a clearer, safer and more supportable operational experience while retaining the proven fulfilment platform underneath it.

**Decision sought:** agreement to progress a controlled pilot, with agreed measures for user adoption, performance, supportability and rollout readiness.

## Core message

> This is a replatform, not a replacement of fulfilment. We are retaining existing business rules, data, authentication and integrations, while replacing the legacy user interface with a modern platform that is easier to use, test, support and improve.

## Before the meeting

- Sign in using the known-good Dev account and confirm the preview banner is visible.
- Open the new NOF Dev site: `https://asofwebappdev.azurewebsites.net`.
- Confirm that the agreement, asset and timeline pages load before the meeting.
- Keep the legacy UI in a separate tab only as a recovery/reference option; do not lead with a side-by-side comparison.
- Have a short recording or screenshots ready in case an integration or test record is unavailable.
- Do not activate an agreement, create/remove a reservation, delete a record or alter live-like operational data during the demo.

### Prepared records

| Purpose | Record | What it demonstrates |
| --- | --- | --- |
| Large-agreement workflow | **A731846** - Test Customer - Scenario 1 | 400 unfulfilled lines; selected-line availability stays in context. Direct URL: `https://asofwebappdev.azurewebsites.net/agreements/2150` |
| Mixed fulfilment | **A731883** | Partially fulfilled agreement with six lines. |
| Simple current example | **T731890** | One unfulfilled current agreement; suitable for a short, low-risk walkthrough. |
| Saved views | Existing personal/shared/division/global views | Persistence and collaboration without rebuilding filters. |

## 15-minute run of show

| Time | Show | Say | Leadership takeaway |
| --- | --- | --- | --- |
| 0:00-1:00 | Start at the NOF home/workspace screen. | "This is a replatform, not a replacement of fulfilment logic. The focus is a safer platform and a clearer operational experience." | Change is contained: the user experience is modernised without a risky replacement of the fulfilment platform. |
| 1:00-4:00 | **Agreements**: search, column filter, status, sort, optional filters and a saved view. | "A planner can get straight to the relevant work and retain their working context." | Faster, more consistent finding and prioritising of work. |
| 4:00-8:00 | Open **A731846**. Point out its 400 lines. Select the first line and open its availability panel. | "The user still sees the agreement, line, quantity, warehouse and reservation context while making an availability decision. The workflow does not push the decision below a very long list." | The UI is designed for realistic operational scale, not just small examples. |
| 8:00-10:00 | In the availability panel, show agreement division, warehouse/facility options and any available stock context. | "Availability remains governed by the established platform rules; this presents it in a clearer decision surface." | Better planning visibility without weakening controls or changing the underlying rules. |
| 10:00-12:00 | **Assets**: switch grid/timeline, navigate the period, resize the context panel, open an asset profile if data is available. | "The timeline makes schedules easier to understand and gives us a foundation for bringing more planning context together over time." | A practical basis for future planning views: assets, agreements, reservations, Ringfences, warnings and exceptions. |
| 12:00-13:00 | Apply a saved view; show a shared/division/global view only if the selected data is appropriate. | "People can standardise repeatable ways of working instead of rebuilding filters and layouts." | Less repetitive work and more consistent operational views. |
| 13:00-14:00 | Show role-aware navigation or the Admin page. Explain ReadOnly behaviour; use a pre-prepared example only. | "The interface makes permitted actions clear, but the important point is that the API enforces role and division access." | Security is server-enforced, not dependent on hiding a button. |
| 14:00-15:00 | Return to the core message. | "The proposed next step is a controlled pilot, measured against adoption, performance and support outcomes before a broader rollout." | A low-risk, measurable route to adoption. |

## Technical questions: concise answers

| Question | Answer |
| --- | --- |
| What is changing? | The legacy Razor/Infragistics/jQuery/Knockout UI is being replaced by a React 19, TypeScript and Vite frontend, hosted with an ASP.NET Core 10 API. |
| Are we rewriting fulfilment logic? | No. The new frontend uses the existing shared fulfilment logic, database and integrations. This reduces business-change risk. |
| Does this change M3, Salesforce, IPG or other integrations? | No. The integration boundaries remain in place; this changes the user-facing application layer. |
| How is it more secure? | Access rules are explicit and enforced by the server. Every sensitive action checks the person's identity, role and authorised division. ReadOnly users can view authorised data but cannot make operational, administration or saved-view changes. |
| Can someone bypass a hidden button? | They can try to call an API directly, but the API repeats the authorisation checks and rejects an unauthorised request - for example with `403 Forbidden`. Hiding a button is user experience; server-side enforcement is the protection. |
| How is sign-in handled? | Production authentication remains Azure App Service EasyAuth, using the existing Microsoft identity platform. The browser does not handle passwords or own the sign-in design. |
| What does alignment with CPQ Next mean? | The applications use the same broad approach: React, TypeScript, Vite, typed API services, component-based UI, automated testing and telemetry. This enables shared skills and patterns, not a forced shared codebase. |
| Why is it more maintainable? | Responsibilities are clearer: React owns presentation, the WebApp owns the intentional API contract, and shared layers retain fulfilment behaviour. TypeScript and focused tests make changes easier to understand and verify. |
| Will it be faster? | The lighter frontend has a better basis for responsiveness and future optimisation. We should prove the benefit with pilot measures - page load, agreement search, availability response and task-completion time - rather than promise a percentage before measurement. |
| How does it handle large agreements? | It is designed for hundreds, and occasionally more than 1,000, lines. The selected line and availability context remain together rather than placing required actions after an unbounded grid. |
| How is deployment controlled? | The SPA and API deploy together in a Linux Web App for Containers using immutable image tags. They share one origin, which simplifies authentication and avoids separate CORS configuration. |
| How will we monitor it? | Frontend and backend telemetry use Application Insights, and hosting health endpoints support operational checks. Success measures should include user-facing failures, latency and workflow outcomes, not infrastructure health alone. |
| What risks remain? | User adoption/training, proving performance with representative volume, deployment promotion and completing parity where users rely on legacy behaviours. Mitigations are staged rollout, shared business logic, UAT/testing and clear pilot measures. |

## Phrases to use - and avoid

| Use | Avoid |
| --- | --- |
| "Creates the opportunity for lower run and change cost." | "This will definitely save money." |
| "Improves the basis for responsiveness and optimisation." | "It is X% faster." |
| "Retains existing business logic and integration boundaries." | "Nothing can go wrong because nothing changed." |
| "A controlled pilot lets us measure readiness." | "It is already ready for Live." |
| "The API enforces authorisation." | "The user cannot see the button." |

## Pilot success measures to propose

- Pilot users can complete the priority agreement, availability and asset workflows without support escalation.
- Page and API response measures meet an agreed baseline for representative large agreements.
- No unauthorised data or mutation is possible across role/division test scenarios.
- Support can identify release version, errors and failed requests using the agreed monitoring process.
- Outstanding parity gaps, training needs and rollout actions have named owners before wider adoption.

## If something fails during the demo

1. Do not troubleshoot live for more than 30 seconds.
2. State the intended outcome plainly: "This is the availability panel that keeps the selected line and stock context together."
3. Move to the next prepared workflow or show the backup recording/screenshot.
4. Close confidently: the demo is evidence of capability; pilot measures will validate operational readiness.
