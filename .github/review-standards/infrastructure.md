# Infrastructure Coding Standards

Applies to changes under `.github/workflows/`, `.github/template/`, `Dockerfile*`, `build/k8s/`, and `k8s/`.

## GitHub Actions Workflows

- Workflows targeting production must restrict the trigger to specific protected branches (`master`, `release/*`, `hotfix/*`) — do not add `workflow_dispatch` to production deploy workflows without an explicit branch guard
- Always exclude Dependabot from non-Dependabot workflows: `if: github.actor != 'dependabot[bot]'`
- Declare only the **minimum required permissions** at the job or workflow level — do not grant `write-all` or broad permissions when a subset suffices
- Use `actions/checkout@v4` (or later) with `fetch-depth: 0` when the job needs full history; use `fetch-depth: 1` (default) otherwise
- Pin third-party actions to a specific version tag (e.g. `@v3`, `@v2`) — do not use `@main` or `@latest` in production pipelines

## Azure Authentication

- Use **OIDC federation** (`azure/login@v3` with `client-id`, `tenant-id`, `subscription-id`) where Azure access is needed — do not use long-lived service principal secrets stored as GitHub secrets
- The `id-token: write` permission is required for OIDC login — ensure it is declared at the workflow or job level

## Secrets & Configuration

- Secrets must come from **Azure Key Vault** (injected via AKS Secret Store CSI Driver) in deployed environments and from **GitHub Secrets** in CI — do not hard-code credentials, connection strings, or API keys in workflow files, Dockerfiles, or Kubernetes manifests
- Reference secrets with `${{ secrets.SECRET_NAME }}` — never echo a secret value in a `run` step
- Environment-specific variables must live in **GitHub Environments** (`vars.*`) — one environment per deployment target

## Docker

- Use **multi-stage builds**: a build stage (SDK image) and a final runtime stage (aspnet/runtime image only)
- The final image must not contain the SDK, source code, or build tooling
- Base images must be pinned to a specific major.minor version (e.g. `mcr.microsoft.com/dotnet/aspnet:10.0`) — do not use `latest`
- The runtime image must expose only the required port and run as a non-root user where possible

## Kubernetes (AKS / Kustomize)

- Changes to base manifests (`build/k8s/base/`) affect all environments — review impact on dev, SIT, test, and live before merging
- Environment-specific overrides belong in the relevant overlay (`build/k8s/overlays/{dev,sit,test,live}/`)
- Do not store plaintext secrets or connection strings in Kubernetes manifests committed to git — use Key Vault CSI Driver and `SecretProviderClass`
- CronJob schedules should be validated against the intended frequency before merging

## Composite Actions (`.github/template/`)

- Composite actions use `shell: bash` for all `run` steps — do not mix shell types within a single action
- Inputs must declare `required: true` or provide a `default` — do not leave inputs implicitly optional
- Action outputs must be declared in the `outputs` section if consumed by downstream steps
