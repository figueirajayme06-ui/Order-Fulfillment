# Feature specification: Frontend preview rollout

## 1. Objective

**User and job:** Business users need to try the new Order Fulfilment frontend from the existing OF application without installation or technical setup.

**Problem:** The React frontend has no stable environment deployment or discoverable route from the legacy application.

**Outcome:** Selected OF Dev users see a preview link, can open the new frontend through EasyAuth, and can return to the current frontend. The same container image can later be promoted with environment-specific runtime configuration.

## 2. Scope and boundaries

### In scope

- Reuse `Users.Roles` with a `NewFrontendPreview` value; no database migration.
- Show the legacy launcher to users with that role, or to all users when `FrontendExperience:ShowForAll` is enabled.
- Allow direct and bookmarked access independently of launcher visibility.
- Retain EasyAuth and the existing OF user/division lookup.
- Add runtime frontend-experience configuration and a preview banner with a return link.
- Build the combined React/API image and deploy it from the existing Azure DevOps workflow.
- Replace the obsolete Container Apps draft with an OF Dev Linux Web App for Containers.

### Out of scope

- Treating the preview role or launcher as an authorization boundary.
- A database schema change or a new feature-flag service.
- Recreating the removed Container Apps environment.
- Production traffic splitting, automatic data reset, or changes to fulfilment business rules.

### Constraints and known rules

- Users reaching the new URL must still pass EasyAuth and resolve to an OF user with assigned divisions.
- Environment URLs and labels are runtime settings, not Vite build variables.
- `ASPNETCORE_ENVIRONMENT` remains `Production` in OF Dev so local identity fallbacks are not enabled.
- Legacy OF remains available throughout the pilot.
- The Linux image cannot run on the existing Windows App Service plan.

## 3. User experience

### Primary flow

1. An administrator assigns `NewFrontendPreview` to selected users in the existing user administration screen.
2. An eligible user opens the Tools menu and selects `Try new Order Fulfilment (Preview)`.
3. The new frontend opens in a new tab and displays a compact `Preview / OF Dev` environment strip.
4. The user can select `Return to current Order Fulfilment` to navigate back in the same tab.
5. During broader rollout, `ShowForAll` displays the launcher without changing user roles.

### Information hierarchy

- The legacy launcher remains in the existing Tools menu.
- The new frontend shows preview/environment context above page content without obscuring operational controls.
- The return action remains visible in the preview strip and is hidden when printing.

### States and edge cases

- Loading: Runtime configuration loads independently and does not block the application.
- Empty: A missing return URL omits only the return action.
- Error: Failure to load runtime configuration hides the preview strip and leaves core workflows usable.
- Success/confirmation: The launcher and banner render when their respective runtime settings are valid.
- Disabled/no-permission: Ineligible users do not see the launcher, but the destination URL remains directly accessible.

### Accessibility and responsive behaviour

- Keyboard/focus behaviour: Both launcher and return action are native links with the existing focus treatment.
- Accessible names/status treatment: Preview status is communicated in text, not colour alone.
- Mobile fallback: The preview strip wraps below the mobile header without becoming sticky.
- Print impact: The preview strip and launcher are navigation-only and do not appear in React print output.

## 4. Technical plan

- Affected routes/pages/components: legacy shared header and user administration; React `AppLayout`; new `GET /api/app-config`; health endpoints.
- API/data changes: Additive, non-secret runtime configuration response only. No persistence change.
- Existing patterns/components to reuse: `FeatureProvider`, `Users.Roles`, CSS modules, theme tokens, same-origin API service, EasyAuth headers, existing Azure DevOps workflow and registry connection.
- Proposed tasks, each independently testable:
  1. Add shared frontend-experience options and legacy role/link visibility.
  2. Add runtime configuration and health endpoints to `OF.WebApp`.
  3. Add the localised React preview strip and resilient configuration loading.
  4. Replace deprecated Container Apps infrastructure with a Dev-only Linux Web App for Containers.
  5. Extend the existing deployment workflow to build, push and deploy an immutable image.

## 5. Acceptance criteria

- [ ] A user with `NewFrontendPreview` sees the legacy preview link; an unselected user does not.
- [ ] `ShowForAll=true` displays the link to every resolved OF user.
- [ ] Link visibility does not protect the new URL; a bookmarked URL remains usable after EasyAuth sign-in.
- [ ] The new frontend identifies itself as an OF Dev preview and provides a return link when configured.
- [ ] Invalid or unavailable runtime configuration does not prevent normal application use.
- [ ] No environment URL is compiled into the React bundle.
- [ ] The deployed app uses existing OF Dev data/integrations and emits Application Insights telemetry.
- [ ] The existing legacy deployment remains unchanged and no database migration is introduced.

## 6. Verification

- Focused tests: legacy link visibility/role list, WebApp configuration controller/routing, React service/banner.
- Build/lint/format commands: focused `dotnet test`; `npm test`; `npm run build`; `npm run lint`; `npm run format`; Docker build; Terraform format/validate.
- Manual visual checks: desktop expanded/collapsed navigation, mobile header, keyboard focus, print, link in eligible/ineligible legacy sessions.
- Data or migration checks: Verify existing Dev users can be assigned the role and retain their divisions; no schema deployment.

## 7. Decisions and open questions

| Item | Decision or question | Owner / resolution |
| --- | --- | --- |
| Access boundary | Preview role controls discoverability only | Confirmed by product owner |
| Hosting | Linux Web App for Containers; do not recreate `cae-ofdev` | Confirmed implementation direction |
| Pipeline | Extend existing app and infrastructure workflows | Confirmed by product owner |
| Initial environment | OF Dev only | Confirmed by product owner |
| EasyAuth callback | Add the new Web App callback URI to the existing Entra registration | Deployment prerequisite |
| Managed identities | Consolidate runtime and ACR access on `mi-of-{environment}`, then remove the retired Container Apps identity and duplicate permissions after verification | Future task |
