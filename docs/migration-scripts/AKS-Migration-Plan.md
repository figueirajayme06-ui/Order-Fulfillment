# Migration Plan: Container App Jobs → AKS Kubernetes CronJobs

**Created:** May 22, 2026  
**Updated:** June 18, 2026  
**Status:** ✅ Dev Complete | ✅ SIT Complete | ✅ Test Ready to Deploy | ⏳ Live Prereqs Complete (Awaiting Networking + Change Request)

---

## Remaining Steps (June 18, 2026)

### Test
1. ▶️ Run all 4 deploy pipelines to Test (`deploy-warehouses`, `deploy-assets`, `deploy-quotes`, `deploy-products`)
2. Manually trigger test jobs and verify logs:
   ```bash
   kubectl create job test-quotes --from=cronjob/nof-quotes -n order-fulfillment-test
   kubectl logs -f job/test-quotes -n order-fulfillment-test
   kubectl delete jobs --all -n order-fulfillment-test
   ```
3. Unsuspend scheduled CronJobs (same patch approach as Dev/SIT)
4. Suspend Container App Jobs in `rgoftest` (switch to Manual trigger)

### Live
1. ▶️ Raise two networking requests (see [Live Deployment Plan](#live-environment-deployment-plan)):
   - **Request 1:** `10.100.200.0/x` → `*.inforcloudsuite.com` HTTPS/443
   - **Request 2:** `10.100.200.0/x` (vnet-agkapg-lv) → `10.100.210.0/x` (vnet-nof-lv) HTTPS/443 + TCP/1433
2. Raise change request
3. During maintenance window: run all 4 deploy pipelines to Live
4. Manually trigger test jobs, verify logs
5. Unsuspend scheduled CronJobs
6. Suspend Container App Jobs in `rgoflive`

### Cleanup (all environments — when confident in AKS stability)
1. Remove Container App Job resources from `build/infra/main.tf` (see [Phase 6](#phase-6-cleanup))
2. Run `terraform plan` → `terraform apply` per environment to destroy: `caj-nof-warehouses`, `caj-nof-assets`, `caj-nof-quotes`, `caj-nof-products`, `caj-nof-cpq`, and `cae-nof-{suffix}`
3. Note: CPQ job (`caj-nof-cpq-*`) is not migrated — remove separately as part of its own replacement solution

---
**PR:** [#515](https://github.com/AggrekoTechnologyServices/Order-Fulfillment/pull/515)  

---

## Environments Overview

| Environment | AKS Cluster | Subscription | Status | MI Client ID |
|-------------|-------------|--------------|--------|--------------|
| **Dev** | `aks-agkapg-dev` | `378303f1-...` | ✅ **Live & Running (Pipeline Automated)** | `bfc32cab-4f35-46e1-9fad-c42996ab6e89` |
| **SIT** | `aks-agkapg-dev` | `378303f1-...` | ✅ **Live & Running** | `e143c84f-195f-407f-a2ee-981ec2eeecc6` |
| **Test** | `aks-agkapg-dev` | `378303f1-...` | ✅ **Ready to Deploy** | `81e849a0-f3d8-4de0-a7bf-f6b4e2bb3422` |
| **Live** | `aks-agkapg-lv` | `486396db-...` | ⏳ **Prereqs Complete — Awaiting Change Request** | `4d5c109a-c9c9-4475-b5ab-4f68bc926cff` |

> **Note:** Dev/SIT/Test share the same AKS cluster (`aks-agkapg-dev`). Each environment uses its own namespace:
> - Dev: `order-fulfillment-dev`
> - SIT: `order-fulfillment-sit`  
> - Test: `order-fulfillment-test`
> - Live: `order-fulfillment` (separate cluster)

---

## Deployment Commands (Kustomize Overlays)

```bash
# Deploy to specific environment
kubectl apply -k build/k8s/overlays/dev
kubectl apply -k build/k8s/overlays/sit
kubectl apply -k build/k8s/overlays/test
kubectl apply -k build/k8s/overlays/live
```

---

## Current State Summary

| Job | Image | Schedule | CPU | Memory | Retry |
|-----|-------|----------|-----|--------|-------|
| Warehouses | `nof/warehouses:latest` | `20 * * * *` | 0.25 | 0.5Gi | 1 |
| Assets | `nof/assets:latest` | `10,40 * * * *` | 2 | 4Gi | 1 |
| Quotes | `nof/quotes:latest` | `20,50 * * * *` | 0.25 | 0.5Gi | 1 |
| Products | `nof/products:latest` | `0 12 * * *` | 0.25 | 0.5Gi | 3 |

> **Note:** CPQ job has been removed (being replaced by different solution)

---

## Migration Progress Summary

### Dev Environment ✅ COMPLETE

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1: Prerequisites | ✅ Complete | AKS access, ACR verified, networking checked |
| Phase 2: Workload Identity | ✅ Complete | Using `mi-of-dev`, federation configured |
| Phase 3: K8s Resources | ✅ Complete | Manifests in `build/k8s/overlays/dev/` |
| Phase 4: Deploy & Test | ✅ Complete | All 4 jobs tested successfully |
| Phase 5: Cutover | ✅ Complete | CronJobs running on schedule |
| Phase 6: Cleanup | ⚠️ In Progress | Container App Jobs suspended (Manual trigger); Azure resources pending Terraform removal. Pipeline fully automated — `kubectl apply -k` runs on every deploy. |

> ✅ **Pipeline fix applied June 18, 2026** — `build/k8s/` manifests are now published as a separate `k8s` pipeline artifact and downloaded automatically by deploy jobs. The `kubectl apply -k overlays/$ENV` step is confirmed working end-to-end.

> ⚠️ **Outstanding (SIT/Test/Live):** Before deploying to each environment, the App Insights connection string must be added to that environment's Key Vault. See [App Insights Secrets Migration](#app-insights-secrets-migration) below.

> ✅ **Namespace migrated to `order-fulfillment-dev`** (May 27, 2026) - Federated credential updated and resources redeployed.

### SIT Environment ✅ COMPLETE

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1: Prerequisites | ✅ Complete | AKS access granted, ACR access verified (shared cluster) |
| Phase 2: Workload Identity | ✅ Complete | Federated credential created on `mi-of-sit` (June 10, 2026) |
| Phase 3: K8s Resources | ✅ Complete | Manifests in `build/k8s/overlays/sit/` |
| Phase 4: Deploy & Test | ✅ Complete | All 4 jobs deployed and tested (June 18, 2026) |
| Phase 5: Cutover | ✅ Complete | CronJobs running on schedule |
| Phase 6: Cleanup | ⚠️ In Progress | Container App Jobs to be suspended |

> ✅ **KV secret added to `kv-ofsit`** (June 10, 2026)
> ✅ **Federated credential created on `mi-of-sit`** (June 10, 2026)
> ✅ **Pipeline deployed successfully** (June 18, 2026)

### Test Environment ✅ READY TO DEPLOY

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1: Prerequisites | ✅ Complete | AKS access granted, ACR access verified (shared cluster) |
| Phase 2: Workload Identity | ✅ Complete | Federated credential created on `mi-of-test` (June 18, 2026) |
| Phase 3: K8s Resources | ✅ Ready | Manifests in `build/k8s/overlays/test/` |
| Phase 4: Deploy & Test | ⬜ Pending | Run Test deploy pipelines and test jobs |
| Phase 5: Cutover | ⬜ Pending | Enable schedules |
| Phase 6: Cleanup | ⬜ Pending | Suspend Container App Jobs |

> ✅ **KV secret added to `kv-oftest`** (June 18, 2026) — `APPLICATIONINSIGHTS-CONNECTION-STRING` is in place.
> ✅ **Federated credential created on `mi-of-test`** (June 18, 2026) — workload identity is ready.
> ✅ **`mi-of-test` permissions verified** — KV access policy has `Get`/`List` on secrets; Storage roles confirmed.
> ▶️ **Next action:** Run all 4 Test deploy pipelines (`deploy-warehouses`, `deploy-assets`, `deploy-quotes`, `deploy-products`).

### Live Environment ⏳ PREREQS COMPLETE — AWAITING CHANGE REQUEST

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 1: Prerequisites | ✅ Complete | AKS access verified, ACR pull confirmed (`AcrPull` role on `acragkapgj9r7vkuplv`) |
| Phase 2: Workload Identity | ✅ Complete | Federated credential created on `mi-of-live` (June 18, 2026) |
| Phase 3: K8s Resources | ✅ Ready | Manifests in `build/k8s/overlays/live/` — `secret.yaml` already removed |
| Phase 4: Deploy & Test | ⬜ Pending | Requires change request approval |
| Phase 5: Cutover | ⬜ Pending | Maintenance window required |
| Phase 6: Cleanup | ⬜ Pending | Suspend Container App Jobs in `rgoflive` |

> ✅ **KV secret added to `kv-oflive`** (June 18, 2026) — `APPLICATIONINSIGHTS-CONNECTION-STRING` is in place.
> ✅ **Federated credential created on `mi-of-live`** (June 18, 2026) — OIDC issuer: `https://westeurope.oic.prod-aks.azure.com/5cb01ea2-.../c42f7e5f-.../`, subject: `order-fulfillment:order-fulfillment-sa`.
> ✅ **`mi-of-live` KV permissions verified** — access policy has `Get`/`List` on secrets.
> ✅ **ACR pull access confirmed** — `aks-agkapg-lv-agentpool` has `AcrPull` on `acragkapgj9r7vkuplv` (June 18, 2026).
> ⚠️ **Networking still required** — raise two firewall requests before deploying (see [Live Deployment Plan](#live-environment-deployment-plan)).
> ▶️ **Next action:** Raise networking requests + change request, then deploy during maintenance window.

---

## Phase 1: Prerequisites & Discovery ✅

**Completed:** May 22-25, 2026 (Dev Environment)

### AKS Cluster Configuration Verified ✅

```
- Name: aks-agkapg-dev
- Location: westeurope
- Kubernetes Version: 1.34.0
- Node Count: 4
- VM Size: Standard_D4ds_v5
- OIDC Issuer: Enabled ✅
- Workload Identity: Enabled ✅
- Azure RBAC: Enabled
- Key Vault Secrets Provider: Enabled ✅
```

### ACR Access Verified ✅

- **ACR:** `acragkapg5ynksx9udev.azurecr.io`
- **AKS kubelet identity** has `AcrPull` role
- All job images confirmed present in ACR

---

## Phase 2: Workload Identity Setup ✅

**Completed:** May 25, 2026 (Dev Environment)

### Using Existing Managed Identity: `mi-of-dev` ✅

Instead of creating a new managed identity, we reuse the existing `mi-of-dev` which already has all required permissions (Key Vault, Storage, SQL Database).

**Identity Details:**
| Property | Value |
|----------|-------|
| Name | `mi-of-dev` |
| Client ID | `bfc32cab-4f35-46e1-9fad-c42996ab6e89` |
| Principal ID | `fd871ded-7efd-41f7-8665-5550668d223e` |
| Resource Group | `rgofdev` |

### Federated Credential Updated ✅

```bash
# Updated May 27, 2026 - subject changed to order-fulfillment-dev namespace
az identity federated-credential update \
  --subscription bac9cfa1-7f78-4238-8d82-ae90f5f1be45 \
  --name aks-order-fulfillment-federation \
  --identity-name mi-of-dev \
  --resource-group rgofdev \
  --subject "system:serviceaccount:order-fulfillment-dev:order-fulfillment-sa"
```

### Existing Permissions (inherited from Container Apps) ✅

The `mi-of-dev` identity already has:
- **Key Vault** (`kv-ofdev`): Key Vault Reader, Secrets Get/List
- **Storage Account** (`stofdev`): Blob/Queue/Table Data Contributor  
- **SQL Database** (`db-ofdev`): Database user with read/write access

### ACR Image Pull Verified ✅

**Tested:** May 25, 2026

```
Successfully pulled image "acragkapg5ynksx9udev.azurecr.io/nof/warehouses:latest" in 262ms
```

---

## Phase 3: Kubernetes Resources ✅

**Completed:** May 25-27, 2026

### Kustomize Structure Created

All environments now use Kustomize overlays for configuration management.

```
build/k8s/
├── base/                      # Shared manifests
│   ├── kustomization.yaml
│   ├── namespace.yaml
│   ├── serviceaccount.yaml
│   ├── cronjob-warehouses.yaml
│   ├── cronjob-assets.yaml
│   ├── cronjob-quotes.yaml
│   └── cronjob-products.yaml
└── overlays/                  # Environment-specific config
    ├── dev/
    │   ├── kustomization.yaml
    │   ├── configmap.yaml
    │   └── secret.yaml
    ├── sit/
    │   └── ...
    ├── test/
    │   └── ...
    └── live/
        └── ...
```

### Deployment Commands

```bash
# Deploy to specific environment
kubectl apply -k build/k8s/overlays/dev
kubectl apply -k build/k8s/overlays/sit
kubectl apply -k build/k8s/overlays/test
kubectl apply -k build/k8s/overlays/live

# Preview what will be deployed
kubectl kustomize build/k8s/overlays/dev
```

---

## Phase 4: Deployment & Testing

### Step 4.1: Deploy to AKS (suspended) ✅

**Completed:** May 25-27, 2026 (Dev Environment)

```bash
kubectl apply -k build/k8s/overlays/dev
```

**Deployed CronJobs:**
```
NAME             SCHEDULE        SUSPEND   ACTIVE
nof-assets       10,40 * * * *   False     0
nof-products     0 12 * * *      False     0
nof-quotes       20,50 * * * *   False     0
nof-warehouses   20 * * * *      False     0
```

> **Note:** Dev jobs run in `order-fulfillment-dev` namespace

### Step 4.2: Manual Testing ✅

**Completed:** May 26-27, 2026

All jobs tested successfully using manual triggers:

```bash
kubectl create job --from=cronjob/nof-warehouses test-warehouses -n order-fulfillment-dev
kubectl create job --from=cronjob/nof-assets test-assets -n order-fulfillment-dev
kubectl create job --from=cronjob/nof-quotes test-quotes -n order-fulfillment-dev
kubectl create job --from=cronjob/nof-products test-products -n order-fulfillment-dev
```

#### ✅ Issues Resolved

| Issue | Resolution |
|-------|------------|
| SQL Login Failed | Used existing `mi-of-dev` instead of new identity |
| CloudSuite API Timeout | Firewall rule `*.inforcloudsuite.com` added to APG-AKS DV |
| Key Vault Access | MI already had permissions |
| Storage Access | MI already had permissions |

#### ✅ Network Connectivity Verified

**All Private Endpoints Accessible (via TechHub firewall rules):**
| Resource | FQDN | Private IP | Status |
|----------|------|------------|--------|
| Key Vault | `kv-ofdev.vault.azure.net` | 10.100.209.136 | ✅ Working |
| SQL Server | `mssql-ofdev.database.windows.net` | 10.100.209.137 | ✅ Working |
| Storage Blob | `stofdev.blob.core.windows.net` | 10.100.209.133 | ✅ Working |
| Storage Queue | `stofdev.queue.core.windows.net` | 10.100.209.134 | ✅ Working |
| Storage Table | `stofdev.table.core.windows.net` | 10.100.209.132 | ✅ Working |
| CloudSuite API | `*.inforcloudsuite.com` | External | ✅ Working |

### Step 4.3: Verify Functionality ✅

- [x] Check logs for successful Key Vault access
- [x] Verify database connections work
- [x] Confirm data sync completes
- [x] Check Application Insights telemetry

---

## Phase 5: Cutover ✅

**Status:** ✅ Complete (Dev Environment)

### Step 5.1: Suspend Container App Jobs ✅

**Completed:** May 28, 2026 - Switched to Manual trigger via REST API to stop duplicate syncs.

```powershell
# All 4 sync jobs suspended via ARM API
$sub = "bac9cfa1-7f78-4238-8d82-ae90f5f1be45"
$body = '{"properties":{"configuration":{"triggerType":"Manual","manualTriggerConfig":{"parallelism":1,"replicaCompletionCount":1}}}}'
$body | Out-File -FilePath "$env:TEMP\caj-patch.json" -Encoding ascii -NoNewline

foreach ($job in @("caj-nof-warehouses-ofdev","caj-nof-assets-ofdev","caj-nof-quotes-ofdev","caj-nof-products-ofdev")) {
  az rest --method PATCH `
    --url "https://management.azure.com/subscriptions/$sub/resourceGroups/rgofdev/providers/Microsoft.App/jobs/${job}?api-version=2024-03-01" `
    --body "@$env:TEMP\caj-patch.json" `
    --query "properties.configuration.triggerType" -o tsv
}
```

**Result:** All 4 jobs now `Manual`. `caj-nof-cpq-ofdev` remains `Schedule` (separate solution, not being migrated).

> **Note:** Terraform still describes these jobs as `Schedule` type. Update Terraform when removing the resources entirely (Phase 6).

### Step 5.2: Enable AKS CronJobs ✅

**Completed:** May 27, 2026

```powershell
# PowerShell (Windows)
echo '{"spec":{"suspend":false}}' | Out-File -Encoding ascii patch.json
kubectl patch cronjob nof-warehouses -n order-fulfillment-dev --type=merge --patch-file patch.json
kubectl patch cronjob nof-assets -n order-fulfillment-dev --type=merge --patch-file patch.json
kubectl patch cronjob nof-quotes -n order-fulfillment-dev --type=merge --patch-file patch.json
kubectl patch cronjob nof-products -n order-fulfillment-dev --type=merge --patch-file patch.json
Remove-Item patch.json
```

### Step 5.3: Monitor ✅

Jobs running successfully on schedule:
```bash
kubectl get cronjobs -n order-fulfillment-dev
kubectl get jobs -n order-fulfillment-dev --sort-by=.metadata.creationTimestamp
```

---

## Phase 6: Cleanup

**Status:** ⚠️ Dev/SIT pending Terraform removal | ⬜ Test pending deployment | ⬜ Live manual (not Terraform-managed)

> **Container App Jobs have been suspended** in Dev (May 28, 2026) and SIT (June 18, 2026). No duplicate runs occurring.
> **Live** Container App Jobs and environment are in a separate resource group not managed by Terraform — suspend and delete manually when ready.

### Step 6.1: Remove Container App Jobs from Terraform (Dev / SIT / Test)

Do this per environment as part of the deployment, once AKS CronJobs are confirmed running.

**File:** `build/infra/main.tf`

**Resources to Remove:**
| Resource Name | Line Range | Azure Resource |
|---------------|------------|----------------|
| `azapi_resource.quotes_warehouses` | 617-670 | `caj-nof-warehouses-{suffix}` |
| `azapi_resource.quotes_assets` | 671-724 | `caj-nof-assets-{suffix}` |
| `azapi_resource.quotes_job` | 725-778 | `caj-nof-quotes-{suffix}` |
| `azapi_resource.products_job` | 779-832 | `caj-nof-products-{suffix}` |
| `azapi_resource.cpq_job` | 833-886 | `caj-nof-cpq-{suffix}` |
| `azurerm_container_app_environment.container_environment` | 590-614 | `cae-nof-{suffix}` |

**Option A: Full Removal (recommended after validation)**
- Delete the resource blocks from `main.tf`
- Run `terraform plan` to verify destruction
- Run `terraform apply` to remove from Azure

**Option B: Preserve Azure Resources (if needed for rollback)**
```hcl
# Add these blocks to preserve resources in Azure while removing from Terraform state
removed {
  from = azapi_resource.quotes_warehouses
  lifecycle { destroy = false }
}
# Repeat for each resource...
```

### Step 6.2: Live Cleanup (Manual)

The Live Container App Jobs and environment are **not managed by Terraform**. Once AKS CronJobs are confirmed running in Live:

1. **Suspend** all 4 jobs (switch to Manual trigger via ARM API — same approach as Dev/SIT/Test)
2. Monitor for a period to confirm no issues
3. **Delete** the Container App Jobs and environment manually via portal or CLI:
   ```bash
   # Switch to Live subscription
   az account set --subscription 486396db-5f3b-4187-8c07-9eb3f561a77b

   # Delete jobs (use actual resource group and job names)
   az containerapp job delete --name caj-nof-warehouses-<suffix> --resource-group <rg-live-caj>
   az containerapp job delete --name caj-nof-assets-<suffix> --resource-group <rg-live-caj>
   az containerapp job delete --name caj-nof-quotes-<suffix> --resource-group <rg-live-caj>
   az containerapp job delete --name caj-nof-products-<suffix> --resource-group <rg-live-caj>
   az containerapp job delete --name caj-nof-cpq-<suffix> --resource-group <rg-live-caj>

   # Delete environment after all jobs removed
   az containerapp env delete --name cae-nof-<suffix> --resource-group <rg-live-caj>
   ```

### Step 6.3: Add AKS CronJobs to Terraform (optional)

- Use `kubernetes_cron_job_v1` resource or Helm chart
- Or manage via GitOps (ArgoCD/Flux)

---

## CI/CD: Deploying Changes Going Forward

The existing Azure DevOps pipelines (`deploy-warehouses.yml`, `deploy-assets.yml`, etc.) already handle **code changes** without modification. Kubernetes **manifest changes** require a separate apply step.

### How It Works Today

| Change Type | How It Deploys | Action Required |
|-------------|----------------|-----------------|
| Code change (sync logic) | Pipeline pushes new image with `latest` tag to ACR; next CronJob run pulls it automatically (`imagePullPolicy: Always`) | None — existing pipelines unchanged |
| Manifest change (schedule, resources, env vars) | Pipeline runs `kubectl apply -k` automatically after Docker push | None — automated via `deploy-containers.yml` |

> ✅ **Pipeline fully operational as of May 28, 2026.** All RBAC roles granted and verified.

### Deploying a Code Change

The existing pipelines (`deploy-warehouses.yml`, etc.) **already work**. They push to ACR with `latest` tag and the next scheduled CronJob run picks up the new image automatically. No pipeline changes needed.

```
Developer merges PR → Pipeline builds & pushes image:latest to ACR
→ Next scheduled CronJob run pulls new image → Done
```

### Deploying a Manifest Change (schedule, config, etc.)

When you change files in `build/k8s/` (e.g., update a schedule, adjust resource limits), the pipeline handles it automatically:

```
Developer merges PR → Pipeline builds & pushes image:latest to ACR
→ Pipeline runs `kubectl apply -k build/k8s/overlays/{env}` → Done
```

To apply manually if needed:

```powershell
# After merging the PR, apply the overlay for the target environment
kubectl apply -k build/k8s/overlays/dev
kubectl apply -k build/k8s/overlays/sit   # when SIT migrated
kubectl apply -k build/k8s/overlays/test  # when Test migrated
kubectl apply -k build/k8s/overlays/live  # when Live migrated
```

> **Tip:** Preview changes before applying: `kubectl kustomize build/k8s/overlays/dev`

### ✅ Pipeline Automation Implemented (May 28, 2026)

All 4 deploy pipelines (`deploy-warehouses.yml`, `deploy-assets.yml`, `deploy-quotes.yml`, `deploy-products.yml`) now include an `AzureCLI@2` step that automatically runs `kubectl apply -k` after the Docker push. The step uses the `aksServiceConnection` parameter defined in `.azd/actions/variables.yml`:

```yaml
# Service connections
aksServiceConnection: 'Aggreko Order Fulfillment Dev/Test'   # Dev/SIT/Test
aksServiceConnectionLive: 'Aggreko Order Fulfillment'         # Live
# AKS cluster details (refactored May 29, 2026 — moved from hardcoded bash to pipeline variables)
aksRg: 'rg-agkapg-aks-dev'
aksName: 'aks-agkapg-dev'
aksSub: '378303f1-2f9d-4f56-9d3b-fd43d3c80909'
aksRgLive: 'rg-agkapg-aks-lv'
aksNameLive: 'aks-agkapg-lv'
aksSubLive: '486396db-5f3b-4187-8c07-9eb3f561a77b'
```

### AKS RBAC Access Request

**Status: ✅ Granted (May 28, 2026)**

Both `Azure Kubernetes Service Cluster User Role` (get credentials) and `Azure Kubernetes Service RBAC Cluster Admin` (deploy resources) granted to the OF service principals:

| Service Principal | Object ID | Cluster | Cluster User Role | RBAC Cluster Admin |
|-------------------|-----------|---------|-------------------|--------------------|
| `Aggreko Order Fulfillment Dev/Test` (`83aca030-...`) | `83aca030-68eb-4899-8af3-54c6fdebe760` | `aks-agkapg-dev` | ✅ Granted | ✅ Granted |
| `Aggreko Order Fulfillment` (`fd82361d-...`) | `fd82361d-bdd6-4733-a345-a1ba0ea7c1a8` | `aks-agkapg-lv` | ✅ Granted | ✅ Granted |

> **Note:** The pipeline uses `az rest` to call `listClusterUserCredential` directly (bypasses subscription account list requirement), then `kubelogin convert-kubeconfig -l azurecli` to convert the kubeconfig from `devicecode` to `azurecli` auth mode before running `kubectl apply -k`.

### Pipeline Fix: k8s Manifests Artifact (June 18, 2026) ✅

Deployment jobs (`deployment` type with `runOnce.deploy`) only auto-download published artifacts — they do not checkout source. The `build/k8s/` directory was not available in the deploy stage.

**Fix applied:**
- `build.yml`: Added a second `PublishPipelineArtifact@1` step that publishes `$(Build.SourcesDirectory)/build/k8s` as the `k8s` artifact.
- `deploy-containers.yml`: Changed `AzureCLI@2` `workingDirectory` to `$(Agent.BuildDirectory)/k8s` and updated `kubectl apply -k overlays/$ENV`.

The `kubectl apply -k` step is now **confirmed working** end-to-end for Dev (June 18, 2026).

---

## App Insights Secrets Migration

**Status:** ✅ Dev complete | ✅ SIT complete | ⬜ Test outstanding | ⬜ Live (Cloud Ops required)

Application Insights connection strings have been moved out of plaintext Kubernetes `Secret` manifests in git. They are now sourced from Azure Key Vault via the AKS Secret Store CSI Driver (`SecretProviderClass`), which the cluster already has enabled.

### What changed (May 29, 2026)

- `build/k8s/overlays/{dev,sit,test}/secret.yaml` — **deleted** (contained plaintext connection strings)
- `build/k8s/overlays/{dev,sit,test}/secretproviderclass.yaml` — **added** (pulls secret from KV, creates K8s Secret at pod startup)
- `build/k8s/base/cronjob-*.yaml` — **updated** (CSI volume mount added to all 4 CronJobs to trigger the sync)
- `build/k8s/overlays/live/secret.yaml` — **still present** pending Cloud Ops adding the secret to `kv-oflive`

### How it works

The `SecretProviderClass` tells the CSI driver which Key Vault secret to fetch. When a CronJob pod starts, the driver mounts the secret as a file and simultaneously creates/updates the Kubernetes Secret (`order-fulfillment-secrets`). The pod reads `APPLICATIONINSIGHTS_CONNECTION_STRING` from that K8s Secret via `envFrom.secretRef` — no app code changes required.

### Outstanding steps before deploying each environment

| Environment | Key Vault | Action | Owner |
|-------------|-----------|--------|-------|
| Dev | `kv-ofdev` | ✅ Secret added (`APPLICATIONINSIGHTS-CONNECTION-STRING`) | Done |
| SIT | `kv-ofsit` | ✅ Secret added (June 10, 2026) | Done |
| Test | `kv-oftest` | ✅ Secret added (June 18, 2026) | Done |
| Live | `kv-oflive` | ✅ Secret added (June 18, 2026) | Done |

> **Note:** The old `secret.yaml` values can be found in git history if needed (`git log --all -- build/k8s/overlays/*/secret.yaml`).

---

## Configuration Reference

### Environment-Specific Configuration (via Kustomize Overlays)

All environment configuration is managed via Kustomize overlays in `build/k8s/overlays/`.

| Environment | Storage Account | Key Vault | ACR |
|-------------|-----------------|-----------|-----|
| Dev | `stofdev` | `kv-ofdev` | `acragkapg5ynksx9udev` |
| SIT | `stofsit` | `kv-ofsit` | `acragkapg5ynksx9udev` |
| Test | `stoftest` | `kv-oftest` | `acragkapg5ynksx9udev` |
| Live | `stoflive` | `kv-oflive` | `acragkapgj9r7vkuplv` |

### Managed Identity Client IDs

| Environment | MI Name | Client ID |
|-------------|---------|-----------|
| Dev | `mi-of-dev` | `bfc32cab-4f35-46e1-9fad-c42996ab6e89` |
| SIT | `mi-of-sit` | `e143c84f-195f-407f-a2ee-981ec2eeecc6` |
| Test | `mi-of-test` | `81e849a0-f3d8-4de0-a7bf-f6b4e2bb3422` |
| Live | `mi-of-live` | `4d5c109a-c9c9-4475-b5ab-4f68bc926cff` |

---

## Environment Deployment Plans

### SIT Environment Deployment Plan

#### Prerequisites Checklist

- [ ] **AKS Cluster Access:** Uses same cluster as Dev (`aks-agkapg-dev`)
  ```bash
  # Should already have credentials from Dev deployment
  az aks get-credentials --resource-group rg-agkapg-aks-dev --name aks-agkapg-dev
  kubectl get nodes
  ```

- [ ] **ACR Access:** Already verified (same cluster as Dev)

- [ ] **Workload Identity:** Uses same OIDC issuer as Dev

#### Phase 2: Workload Identity Setup

1. **Use the same OIDC Issuer URL as Dev:**
   ```bash
   # Same issuer as Dev since they share the cluster
   AKS_OIDC_ISSUER="https://westeurope.oic.prod-aks.azure.com/5cb01ea2-4160-4f87-afc6-5e72e6b82ad1/6f47f798-054e-4de2-914d-0c7a0e976792/"
   ```

2. **Create Federated Credential on MI:**
   ```bash
   # Requires ManagedIdentity write permission on OF Dev/Test subscription (bac9cfa1-...)
   # Raise via PIM if you don't have write access
   az identity federated-credential create \
     --subscription bac9cfa1-7f78-4238-8d82-ae90f5f1be45 \
     --name aks-order-fulfillment-federation \
     --identity-name mi-of-sit \
     --resource-group rgofsit \
     --issuer "$AKS_OIDC_ISSUER" \
     --subject "system:serviceaccount:order-fulfillment-sit:order-fulfillment-sa"
   ```

3. **Verify MI Permissions:** Ensure `mi-of-sit` has:
   - Key Vault Secrets User on `kv-ofsit`
   - Storage Blob/Queue/Table Data Contributor on `stofsit`
   - SQL Database user in `db-ofsit`

#### Phase 3: Firewall Rules

Request networking team to add firewall rules for AKS → SIT resources:
- [ ] `*.blob.core.windows.net` (Storage)
- [ ] `*.vault.azure.net` (Key Vault)
- [ ] `*.database.windows.net` (SQL)
- [ ] `*.inforcloudsuite.com` (CloudSuite API)

#### Phase 4: Deploy & Test

1. **Deploy (suspended):**
   ```bash
   kubectl apply -k build/k8s/overlays/sit
   kubectl get cronjobs -n order-fulfillment-sit
   ```

2. **Test each job:**
   ```bash
   kubectl create job test-warehouses --from=cronjob/nof-warehouses -n order-fulfillment-sit
   kubectl logs -f job/test-warehouses -n order-fulfillment-sit
   
   kubectl create job test-quotes --from=cronjob/nof-quotes -n order-fulfillment-sit
   kubectl create job test-assets --from=cronjob/nof-assets -n order-fulfillment-sit
   kubectl create job test-products --from=cronjob/nof-products -n order-fulfillment-sit
   ```

3. **Cleanup test jobs:**
   ```bash
   kubectl delete jobs --all -n order-fulfillment-sit
   ```

#### Phase 5: Cutover

1. **Suspend Container App Jobs** in `rgofsit`

2. **Enable AKS CronJobs:**
   ```bash
   echo '{"spec":{"suspend":false}}' | Out-File -Encoding ascii patch.json
   kubectl patch cronjob nof-warehouses -n order-fulfillment-sit --type=merge --patch-file patch.json
   kubectl patch cronjob nof-assets -n order-fulfillment-sit --type=merge --patch-file patch.json
   kubectl patch cronjob nof-quotes -n order-fulfillment-sit --type=merge --patch-file patch.json
   kubectl patch cronjob nof-products -n order-fulfillment-sit --type=merge --patch-file patch.json
   Remove-Item patch.json
   ```

3. **Monitor:**
   ```bash
   kubectl get cronjobs -n order-fulfillment-sit
   kubectl get jobs -n order-fulfillment-sit
   ```

---

### Test Environment Deployment Plan

#### Prerequisites Checklist

- [ ] **AKS Cluster Access:** Uses same cluster as Dev (`aks-agkapg-dev`)
  ```bash
  # Should already have credentials from Dev deployment
  az aks get-credentials --resource-group rg-agkapg-aks-dev --name aks-agkapg-dev
  kubectl get nodes
  ```

- [ ] **ACR Access:** Already verified (same cluster as Dev)

- [ ] **Workload Identity:** Uses same OIDC issuer as Dev

#### Phase 2: Workload Identity Setup

1. **Use the same OIDC Issuer URL as Dev:**
   ```bash
   # Same issuer as Dev since they share the cluster
   AKS_OIDC_ISSUER="https://westeurope.oic.prod-aks.azure.com/5cb01ea2-4160-4f87-afc6-5e72e6b82ad1/6f47f798-054e-4de2-914d-0c7a0e976792/"
   ```

2. **Create Federated Credential on MI:**
   ```bash
   # Requires ManagedIdentity write permission on OF Dev/Test subscription (bac9cfa1-...)
   # Raise via PIM if you don't have write access
   az identity federated-credential create \
     --subscription bac9cfa1-7f78-4238-8d82-ae90f5f1be45 \
     --name aks-order-fulfillment-federation \
     --identity-name mi-of-test \
     --resource-group rgoftest \
     --issuer "$AKS_OIDC_ISSUER" \
     --subject "system:serviceaccount:order-fulfillment-test:order-fulfillment-sa"
   ```

#### Phase 3: Firewall Rules

Request firewall rules for AKS → Test resources (same as SIT).

#### Phase 4: Deploy & Test

```bash
kubectl apply -k build/k8s/overlays/test
kubectl create job test-warehouses --from=cronjob/nof-warehouses -n order-fulfillment-test
# Test remaining jobs...
```

#### Phase 5: Cutover

1. Suspend Container App Jobs in `rgoftest`
2. Enable AKS CronJobs:
   ```bash
   echo '{"spec":{"suspend":false}}' | Out-File -Encoding ascii patch.json
   kubectl patch cronjob nof-warehouses -n order-fulfillment-test --type=merge --patch-file patch.json
   kubectl patch cronjob nof-assets -n order-fulfillment-test --type=merge --patch-file patch.json
   kubectl patch cronjob nof-quotes -n order-fulfillment-test --type=merge --patch-file patch.json
   kubectl patch cronjob nof-products -n order-fulfillment-test --type=merge --patch-file patch.json
   Remove-Item patch.json
   ```
3. Monitor for 24-48 hours

---

### Live Environment Deployment Plan

> ⚠️ **PRODUCTION DEPLOYMENT** - Follow change management process

**Live uses a separate AKS cluster in a different subscription:**
- **Cluster:** `aks-agkapg-lv`
- **Subscription:** `486396db-5f3b-4187-8c07-9eb3f561a77b`

#### Prerequisites Checklist

- [ ] **Change Request Approved**
- [ ] **Rollback Plan Reviewed**
- [ ] **Switch to Live Subscription:**
  ```bash
  az account set --subscription 486396db-5f3b-4187-8c07-9eb3f561a77b
  ```
- [ ] **AKS Cluster Access:** Verify access to `aks-agkapg-lv`
  ```bash
  az aks get-credentials --resource-group rg-agkapg-aks-lv --name aks-agkapg-lv
  kubectl get nodes
  ```

- [ ] **ACR Access:** Verify AKS can pull images from `acragkapgj9r7vkuplv.azurecr.io`

- [ ] **Workload Identity Enabled:** Verify AKS has OIDC issuer
  ```bash
  az aks show --resource-group rg-agkapg-aks-lv --name aks-agkapg-lv --query "oidcIssuerProfile.issuerUrl"
  ```

#### Phase 2: Workload Identity Setup

1. **Get AKS OIDC Issuer URL (different from Dev cluster):**
   ```bash
   AKS_OIDC_ISSUER=$(az aks show --resource-group rg-agkapg-aks-lv --name aks-agkapg-lv --query "oidcIssuerProfile.issuerUrl" -o tsv)
   echo $AKS_OIDC_ISSUER
   ```

2. **Create Federated Credential on MI:**
   ```bash
   # Requires ManagedIdentity write permission on OF Live subscription (6ae495cb-...)
   az identity federated-credential create \
     --subscription 6ae495cb-5948-4659-a6ae-45b3e58cc68a \
     --name aks-order-fulfillment-federation \
     --identity-name mi-of-live \
     --resource-group rgoflive \
     --issuer "$AKS_OIDC_ISSUER" \
     --subject "system:serviceaccount:order-fulfillment:order-fulfillment-sa"
   ```

#### Phase 3: Firewall Rules

Request firewall rules for AKS → Live resources.

#### Phase 4: Deploy & Test (During Maintenance Window)

```bash
kubectl apply -k build/k8s/overlays/live
kubectl create job test-warehouses --from=cronjob/nof-warehouses -n order-fulfillment
# Verify logs, then test remaining jobs
kubectl delete jobs --all -n order-fulfillment
```

#### Phase 5: Cutover

1. **Suspend Container App Jobs** in `rgoflive`
2. **Enable AKS CronJobs**
3. **Monitor closely** for first 24-48 hours

---

## Rollback Plan

If issues occur during cutover:

> **Namespace by environment:**
> - Dev: `order-fulfillment-dev`
> - SIT: `order-fulfillment-sit`
> - Test: `order-fulfillment-test`
> - Live: `order-fulfillment`

1. **Suspend AKS CronJobs:**
   ```bash
   # Replace {namespace} with the appropriate namespace for the environment
   echo '{"spec":{"suspend":true}}' | Out-File -Encoding ascii patch.json
   kubectl patch cronjob nof-warehouses -n {namespace} --type=merge --patch-file patch.json
   kubectl patch cronjob nof-assets -n {namespace} --type=merge --patch-file patch.json
   kubectl patch cronjob nof-quotes -n {namespace} --type=merge --patch-file patch.json
   kubectl patch cronjob nof-products -n {namespace} --type=merge --patch-file patch.json
   Remove-Item patch.json
   ```

2. **Re-enable Container App Jobs:**
   - Revert Terraform changes
   - Run `terraform apply`

3. **Investigate:**
   - Check AKS job logs: `kubectl logs job/<job-name> -n {namespace}`
   - Check events: `kubectl get events -n {namespace} --sort-by=.lastTimestamp`
