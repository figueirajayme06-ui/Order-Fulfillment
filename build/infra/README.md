# Infrastructure as code.
This location includes the infrastructure as code for provisioning and managing our Azure resources. It is all lumped in one big file for now, feel free to change it. This is only ran via the Infra, web and Api pipeline, but is planned on PR.

## New frontend preview host

The React SPA and `OF.WebApp` API are hosted together in a Linux Web App for Containers. The host is enabled only for Dev by the existing infrastructure workflow; the removed Container Apps Environment must not be recreated.

Before the first application deployment:

1. Run the existing infrastructure workflow for Dev and review the Terraform plan for unrelated destroys.
2. Confirm the App Service integration subnet supports multi-plan subnet join and has sufficient addresses for the additional Linux plan.
3. Confirm `uai-containerapps` has pull access to the shared ACR and retains its Key Vault, Storage and Service Bus permissions.
4. Add the `new_frontend_auth_callback_url` Terraform output to the existing Entra application registration.
5. Run the existing application deployment workflow. It pushes `nof/of-webapp` with the full commit SHA and deploys that immutable tag to `asofwebappdev`.

The infrastructure workflow parameter **Show the new frontend link to all OF Dev users** controls `FrontendExperience__ShowForAll`. Leave it disabled for the role-targeted pilot and enable it for the later broad rollout. This setting controls link visibility only; EasyAuth and the OF user record remain required at the destination.

## Deployment identity and browser cache policy

The container deployment supplies the immutable release number, source ref, commit SHA, and build timestamp through
`FrontendExperience__*` App Service settings. `ShowDeploymentInfo` is enabled for Dev, SIT, and Test and explicitly
disabled for Live; when disabled, `/api/app-config` omits the deployment object and the React application renders no
indicator. Azure DevOps remains the history source.

The current application workflow deploys the container host in Dev. When the existing `enable_new_frontend` rollout
creates the SIT and Test hosts, invoke the same `deploy-webapp-container.yml` template in those stages so the immutable
metadata is supplied there too. Do not add the Live deployment until that host is intentionally enabled; its
`ShowDeploymentInfo` value remains false.

`index.html` and SPA fallbacks use `Cache-Control: no-store`. Fingerprinted Vite assets use a one-year immutable cache,
and API responses use `no-store`. An ordinary release preserves authentication and browser saved state. If an old open
tab observes a changed deployed version, it performs one guarded reload while retaining session-state grid context.

Deployments do not clear authentication or browser storage. Authentication revocation for a security incident remains
an identity-platform incident-response action rather than an application deployment cache mechanism.
