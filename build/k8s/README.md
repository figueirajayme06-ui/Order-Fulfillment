# Kubernetes Manifests for Order Fulfillment CronJobs

This directory contains Kubernetes manifests for deploying the Order Fulfillment data sync CronJobs to AKS clusters.

## Cluster & Namespace Architecture

| Environment | AKS Cluster | Namespace | Notes |
|-------------|-------------|-----------|-------|
| Dev | `aks-agkapg-dev` | `order-fulfillment-dev` | Shared cluster |
| SIT | `aks-agkapg-dev` | `order-fulfillment-sit` | Shared cluster |
| Test | `aks-agkapg-dev` | `order-fulfillment-test` | Shared cluster |
| Live | `aks-agkapg-lv` | `order-fulfillment` | Separate cluster (different subscription) |

> **Note:** Dev/SIT/Test share a single AKS cluster and use separate namespaces to avoid conflicts.

## Directory Structure

```
build/k8s/
├── base/                    # Shared base manifests
│   ├── kustomization.yaml
│   ├── namespace.yaml
│   ├── serviceaccount.yaml
│   ├── cronjob-warehouses.yaml
│   ├── cronjob-assets.yaml
│   ├── cronjob-quotes.yaml
│   └── cronjob-products.yaml
└── overlays/                # Environment-specific configurations
    ├── dev/
    │   ├── kustomization.yaml
    │   ├── configmap.yaml
    │   └── secret.yaml
    ├── sit/
    ├── test/
    └── live/
```

## Deployment

### Deploy to an environment

```bash
# Dev (subscription: 378303f1-2f9d-4f56-9d3b-fd43d3c80909)
az aks get-credentials --resource-group rg-agkapg-aks-dev --name aks-agkapg-dev
kubectl apply -k build/k8s/overlays/dev

# SIT (same cluster as Dev)
kubectl apply -k build/k8s/overlays/sit

# Test (same cluster as Dev)
kubectl apply -k build/k8s/overlays/test

# Live (subscription: 486396db-5f3b-4187-8c07-9eb3f561a77b)
az account set --subscription 486396db-5f3b-4187-8c07-9eb3f561a77b
az aks get-credentials --resource-group rg-agkapg-aks-lv --name aks-agkapg-lv
kubectl apply -k build/k8s/overlays/live
```

### Preview what will be deployed

```bash
kubectl kustomize build/k8s/overlays/dev
```

### Check deployment status

```bash
kubectl get cronjobs -n order-fulfillment
kubectl get jobs -n order-fulfillment
kubectl get pods -n order-fulfillment
```

### Manually trigger a job

```bash
kubectl create job manual-test --from=cronjob/nof-warehouses -n order-fulfillment
```

### View logs

```bash
kubectl logs job/nof-warehouses-<job-id> -n order-fulfillment
```

## Environment Configuration

Each overlay must define:

| File | Contents |
|------|----------|
| `kustomization.yaml` | ACR registry, Managed Identity client ID |
| `configmap.yaml` | Storage account name, Key Vault URI |
| `secret.yaml` | Application Insights connection string |

### Values to configure per environment

| Setting | Dev | SIT | Test | Live |
|---------|-----|-----|------|------|
| Storage Account | `stofdev` | `stofsit` | `stoftest` | `stoflive` |
| Key Vault | `kv-ofdev` | `kv-ofsit` | `kv-oftest` | `kv-oflive` |
| MI Client ID | (from Azure) | (from Azure) | (from Azure) | (from Azure) |
| ACR | (from Azure) | (from Azure) | (from Azure) | (from Azure) |
| App Insights | (from Azure) | (from Azure) | (from Azure) | (from Azure) |

## Prerequisites per environment

Before deploying to a new environment:

1. **AKS Cluster** with Workload Identity enabled
2. **Managed Identity** with:
   - Key Vault Secrets User role on Key Vault
   - Storage Blob/Queue/Table Data Contributor on Storage Account
   - SQL Database user with appropriate permissions
3. **Federated Credential** linking the MI to the K8s service account:
   ```bash
   az identity federated-credential create \
     --name aks-order-fulfillment-federation \
     --identity-name mi-of-{env} \
     --resource-group rg-of-{env} \
     --issuer "<AKS-OIDC-ISSUER-URL>" \
     --subject "system:serviceaccount:order-fulfillment:order-fulfillment-sa"
   ```
4. **ACR** with images pushed and AKS kubelet identity granted `AcrPull` role
5. **Firewall rules** for:
   - `*.blob.core.windows.net` (Storage)
   - `*.vault.azure.net` (Key Vault)
   - `*.database.windows.net` (SQL)
   - `*.inforcloudsuite.com` (CloudSuite API - for Assets/Products jobs)

## CronJob Schedules

| Job | Schedule | Description |
|-----|----------|-------------|
| nof-warehouses | `20 * * * *` | Every hour at :20 |
| nof-assets | `10,40 * * * *` | Every hour at :10 and :40 |
| nof-quotes | `20,50 * * * *` | Every hour at :20 and :50 |
| nof-products | `0 12 * * *` | Daily at 12:00 |
