### Setup
terraform {
  backend "azurerm" {
    resource_group_name = "Landing-Zones-Default"
    key                 = "of.tfstate"
    use_azuread_auth    = true
  }
  required_version = "~> 1.14.0"
  required_providers {
    azapi = {
      source  = "Azure/azapi"
      version = "~> 2.9.0"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.71.0"
    }
  }
}

provider "azuread" {
}

provider "azurerm" {
  subscription_id = var.tf_subscription_id
  features {}
  storage_use_azuread = true
}

provider "azurerm" {
  alias = "hub"
  subscription_id = var.hub_subscription_id
  features {}
  storage_use_azuread = true
}

### Variables

variable "laws_name" {
  type    = string
  default = "law-ofdvtst"
}

variable "app_version" {
  type = string
}

variable "cr_name" {
  type = string
}

variable "cr_resource_group" {
  type = string
}

variable "tf_subscription_id" {
  type        = string
  description = "Subscription ID for the Aggreko Order Fulfillment Dev/Test."
}

variable "hub_subscription_id" {
  type        = string
  description = "Subscription ID for the Aggreko Tech Hub."
}

variable "hub_resource_group" {
  type        = string
}

variable "cr_subscription_id" {
  type        = string
  description = "Subscription ID for the APG shared Azure Container Registry."
}

variable "app_sku" {
  type    = string
  default = "S1"
}

variable "app_tier" {
  type    = string
  default = "Standard"
}

variable "environment" {
  type    = string
  default = "Dev"
}

variable "auth_client_id" {
  type = string
}

variable "auth_scope" {
  type = string
}

variable "ipg_sbns_connection" {
  type = string
}

variable "ipg_sub_name" {
  type    = string
  default = "OrderFulfillment"
}

variable "vnet_name" {
  type    = string
  default = ""
}

variable "vnet_subnet_root" {
  type        = string
  default     = ""
  description = "Subnet name prefix for VNet-integrated resources (e.g. 'agk-nof-dev'). Subnets are resolved by Terraform data sources: '<root>' (VNet integration) and '<root>-pe' (private endpoints)."
}

variable "subscription_id" {
  type    = string
  default = ""
}

variable "enable_private_access" {
  type        = bool
  default     = false
  description = "Master toggle for private networking. When true, enables private endpoints and VNet integration for app services. Requires vnet_name and vnet_subnet_root to be set."
}

variable "enable_new_frontend" {
  type        = bool
  default     = false
  description = "Creates the containerised React frontend host and enables its launcher in the legacy application."
}

variable "show_new_frontend_for_all" {
  type        = bool
  default     = false
  description = "Shows the new frontend launcher to every resolved OF user. The destination remains directly accessible regardless of this value."
}

### Locals

locals {
  landingZoneRg = "Landing-Zones-Default"
  region        = "westeurope"
  suffix        = "of${lower(var.environment)}"

  tags = {
    Application      = "OF Orchestrator"
    ATSOwner         = "Christopher Seth"
    Environment      = var.environment
    SharedResource   = "No"
    TechnicalContact = "Christopher Seth"
    TerraformManaged = "Yes"
  }

  app_insights_tags = merge(local.tags, {
    "hidden-link: /app-insights-resource-id" = azurerm_application_insights.insights.id
  })

  is_pre_live = lower(var.environment) != "live"

  container_common_settings = [
    {
      name  = "AZURE_CLIENT_ID"
      value = azurerm_user_assigned_identity.containerapps.client_id
    },
    {
      name  = "StorageAccountName"
      value = azurerm_storage_account.storage.name
    },
    {
      name  = "KeyVaultUri"
      value = azurerm_key_vault.kv.vault_uri
    },
    {
      name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
      value = azurerm_application_insights.insights.connection_string
    },
    {
      name  = "LOGGING__LOGLEVEL__DEFAULT"
      value = "Information"
    },
    {
      name  = "LOGGING__APPLICATIONINSIGHTS__LOGLEVEL__DEFAULT"
      value = "Information"
    },
    {
      name  = "LOGGING__APPLICATIONINSIGHTS__LOGLEVEL__AZURE"
      value = "Warning"
    },
    {
      name  = "LOGGING__APPLICATIONINSIGHTS__LOGLEVEL__MICROSOFT"
      value = "Warning"
    }
  ]

  access_policy = merge(
    {
      "current_client" = {
        tenant_id               = data.azurerm_client_config.current.tenant_id
        object_id               = data.azurerm_client_config.current.object_id
        key_permissions         = ["Get"]
        secret_permissions      = ["Set", "Get", "Delete", "Purge", "List", "Recover", "Backup", "Restore"]
        storage_permissions     = ["Get"]
        certificate_permissions = []
      },
      "containerapps" = {
        tenant_id               = azurerm_user_assigned_identity.containerapps.tenant_id
        object_id               = azurerm_user_assigned_identity.containerapps.principal_id
        key_permissions         = ["Get"]
        secret_permissions      = ["Get", "List"]
        storage_permissions     = ["Get"]
        certificate_permissions = []
      },
      "mi_of" = {
        tenant_id               = data.azurerm_user_assigned_identity.mi_of.tenant_id
        object_id               = data.azurerm_user_assigned_identity.mi_of.principal_id
        key_permissions         = ["Get"]
        secret_permissions      = ["Get", "List"]
        storage_permissions     = ["Get"]
        certificate_permissions = []
      },
      "ats_cloudservices_reporting" = {
        tenant_id               = data.azurerm_client_config.current.tenant_id
        object_id               = "5a284d34-2f65-4519-8605-131a081c868d" // ATS-AUTOMATION-CloudServices-Reporting - Required by CloudOps
        key_permissions         = []
        secret_permissions      = []
        storage_permissions     = []
        certificate_permissions = ["Get", "List"]
      },
      "drata_entra_app" = {
        tenant_id               = data.azurerm_client_config.current.tenant_id
        object_id               = "823148ce-4c46-4016-8d6b-ea230299ce4b" // Drata Entra App - Required by CloudOps
        key_permissions         = ["Get", "List"]
        secret_permissions      = ["Get", "List"]
        storage_permissions     = ["Get", "List"]
        certificate_permissions = ["Get", "List"]
      },
      "ats_security_ops" = {
        tenant_id               = data.azurerm_client_config.current.tenant_id
        object_id               = "3c85be42-173d-4e5b-8c03-3426cbcd2073" // ATS Security Operations - Required by CloudOps
        key_permissions         = ["Get", "List", "Update", "Create", "Import", "Delete", "Recover", "Backup", "Restore", "Decrypt", "Encrypt", "UnwrapKey", "WrapKey", "Verify", "Sign", "Release", "Rotate", "GetRotationPolicy", "SetRotationPolicy"]
        secret_permissions      = ["Get", "List", "Set", "Delete", "Recover", "Backup", "Restore"]
        storage_permissions     = ["Backup", "Delete", "DeleteSAS", "Get", "GetSAS", "List", "ListSAS", "Recover", "RegenerateKey", "Restore", "Set", "SetSAS", "Update"]
        certificate_permissions = ["Get", "List", "Update", "Create", "Import", "Delete", "Recover", "Backup", "Restore", "ManageContacts", "ManageIssuers", "GetIssuers", "ListIssuers", "SetIssuers", "DeleteIssuers"]
      }
    },
    local.is_pre_live ? {
      "apps_dev_team" = {
        tenant_id               = data.azurerm_client_config.current.tenant_id
        object_id               = "b09c3bc7-fd7f-4c42-8e29-0d5681e96404" // Apps dev team
        key_permissions         = []
        secret_permissions      = ["Set", "Get", "Delete", "Purge", "List", "Recover", "Backup", "Restore"]
        storage_permissions     = []
        certificate_permissions = ["Get", "Delete", "Create", "List"]
      }
    } : {}
  )
}

### Imports

data "azurerm_client_config" "current" {}

data "azurerm_log_analytics_workspace" "logs" {
  name                = var.laws_name
  resource_group_name = local.landingZoneRg
}

# login_server is computed from var.cr_name — no cross-subscription data source required
locals {
  cr_login_server = "${var.cr_name}.azurecr.io"
}

data "azurerm_user_assigned_identity" "mi_of" {
  name                = "mi-of-${lower(var.environment)}"
  resource_group_name = azurerm_resource_group.rg.name
}

data "azurerm_mssql_server" "mssql" {
  name                = "mssql-${local.suffix}"
  resource_group_name = azurerm_resource_group.rg.name
}

data "azurerm_subnet" "pe" {
  count                = var.enable_private_access ? 1 : 0
  name                 = "${var.vnet_subnet_root}-pe"
  virtual_network_name = var.vnet_name
  resource_group_name  = local.landingZoneRg
}

data "azurerm_subnet" "vnet_integration" {
  count                = var.enable_private_access ? 1 : 0
  name                 = var.vnet_subnet_root
  virtual_network_name = var.vnet_name
  resource_group_name  = local.landingZoneRg
}

### Resources

resource "azurerm_resource_group" "rg" {
  name     = "rg${local.suffix}"
  location = local.region
  tags     = local.tags
}

resource "azurerm_application_insights" "insights" {
  name                = "ai${local.suffix}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = local.region
  application_type    = "web"
  workspace_id        = data.azurerm_log_analytics_workspace.logs.id
  tags                = local.tags
}

resource "azurerm_storage_account" "storage" {
  name                            = "st${local.suffix}"
  resource_group_name             = azurerm_resource_group.rg.name
  location                        = local.region
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  allow_nested_items_to_be_public = false
  min_tls_version                 = "TLS1_2"
  public_network_access_enabled   = !var.enable_private_access
  shared_access_key_enabled       = false
  tags                            = local.tags

  dynamic "network_rules" {
    for_each = var.enable_private_access ? [1] : []
    content {
      default_action = "Deny"
      bypass         = ["AzureServices"]
    }
  }
}

resource "azurerm_servicebus_namespace" "sbns" {
  name                = "sbms${local.suffix}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = local.region
  sku                 = "Standard"
  minimum_tls_version = "1.2"
  tags                = local.tags
}

resource "azurerm_servicebus_queue" "assets" {
  name         = "assets"
  namespace_id = azurerm_servicebus_namespace.sbns.id
}

resource "azurerm_servicebus_queue" "activate_header_queue" {
  name         = "activateheader"
  namespace_id = azurerm_servicebus_namespace.sbns.id
}

resource "azurerm_servicebus_queue" "activate_queue" {
  name         = "activateagreement"
  namespace_id = azurerm_servicebus_namespace.sbns.id
}

resource "azurerm_servicebus_queue" "quote_queue" {
  name         = "upsertquote"
  namespace_id = azurerm_servicebus_namespace.sbns.id
}

resource "azurerm_servicebus_queue" "agreement_queue" {
  name         = "updatebyagreement"
  namespace_id = azurerm_servicebus_namespace.sbns.id
}

resource "azurerm_servicebus_queue" "changenotify" {
  name         = "changenotify"
  namespace_id = azurerm_servicebus_namespace.sbns.id
}

resource "azurerm_servicebus_queue" "changeapproval" {
  name         = "changeapproval"
  namespace_id = azurerm_servicebus_namespace.sbns.id
}

resource "azurerm_servicebus_queue" "changecomplete" {
  name         = "changecomplete"
  namespace_id = azurerm_servicebus_namespace.sbns.id
}

resource "azurerm_servicebus_queue" "agreement-sync" {
  name                  = "agreement-sync"
  namespace_id          = azurerm_servicebus_namespace.sbns.id
  requires_session      = true
  max_size_in_megabytes = 5120
}

### Identity

resource "azurerm_user_assigned_identity" "containerapps" {
  location            = azurerm_resource_group.rg.location
  name                = "uai-containerapps"
  resource_group_name = azurerm_resource_group.rg.name

  tags = local.tags
}

# acrpull on the APG ACR must be assigned manually (cross-subscription):
# az role assignment create --role "AcrPull" \
#   --assignee <uai-containerapps principal_id> \
#   --scope /subscriptions/${var.cr_subscription_id}/resourceGroups/${var.cr_resource_group}/providers/Microsoft.ContainerRegistry/registries/${var.cr_name}
removed {
  from = azurerm_role_assignment.containerapp
  lifecycle {
    destroy = false
  }
}

resource "azurerm_role_assignment" "fn_storage_blob" {
  scope                = azurerm_storage_account.storage.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = data.azurerm_user_assigned_identity.mi_of.principal_id
}

resource "azurerm_role_assignment" "fn_storage_account_contributor" {
  scope                = azurerm_storage_account.storage.id
  role_definition_name = "Storage Account Contributor"
  principal_id         = data.azurerm_user_assigned_identity.mi_of.principal_id
}

resource "azurerm_role_assignment" "fn_storage_queue" {
  scope                = azurerm_storage_account.storage.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = data.azurerm_user_assigned_identity.mi_of.principal_id
}

resource "azurerm_role_assignment" "fn_storage_table" {
  scope                = azurerm_storage_account.storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = data.azurerm_user_assigned_identity.mi_of.principal_id
}

resource "azurerm_role_assignment" "containerapps_storage_blob" {
  scope                = azurerm_storage_account.storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.containerapps.principal_id
}

resource "azurerm_role_assignment" "containerapps_storage_table" {
  scope                = azurerm_storage_account.storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_user_assigned_identity.containerapps.principal_id
}

resource "azurerm_role_assignment" "mi_of_servicebus" {
  scope                = azurerm_servicebus_namespace.sbns.id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = data.azurerm_user_assigned_identity.mi_of.principal_id
}

resource "azurerm_role_assignment" "containerapps_servicebus" {
  scope                = azurerm_servicebus_namespace.sbns.id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = azurerm_user_assigned_identity.containerapps.principal_id
}

####### Compute

resource "azurerm_service_plan" "plan" {
  name                = "plan-${local.suffix}"
  location            = local.region
  resource_group_name = azurerm_resource_group.rg.name
  os_type             = "Windows"
  sku_name            = var.app_sku
  tags                = local.tags
}

resource "azurerm_windows_web_app" "web" {
  name                            = "as${local.suffix}"
  location                        = local.region
  resource_group_name             = azurerm_resource_group.rg.name
  service_plan_id                 = azurerm_service_plan.plan.id
  https_only                      = true
  tags                            = local.app_insights_tags
  key_vault_reference_identity_id = data.azurerm_user_assigned_identity.mi_of.id
  virtual_network_subnet_id       = var.enable_private_access ? one(data.azurerm_subnet.vnet_integration[*].id) : null

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.containerapps.id, data.azurerm_user_assigned_identity.mi_of.id]
  }

  site_config {
    http2_enabled          = true
    websockets_enabled     = true
    minimum_tls_version    = "1.2"
    ftps_state             = "FtpsOnly"
    use_32_bit_worker      = true
    vnet_route_all_enabled = var.enable_private_access

    application_stack {
      current_stack  = "dotnet"
      dotnet_version = "v10.0"
    }
  }

  app_settings = {
    "APPINSIGHTS_INSTRUMENTATIONKEY"             = azurerm_application_insights.insights.instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING"      = azurerm_application_insights.insights.connection_string
    "KeyVaultUri"                                = azurerm_key_vault.kv.vault_uri
    "StorageAccountName"                         = azurerm_storage_account.storage.name
    "ServiceBusNamespace"                        = "${azurerm_servicebus_namespace.sbns.name}.servicebus.windows.net"
    "ApiUri"                                     = "https://${azurerm_windows_function_app.api.default_hostname}/api"
    "AZURE_CLIENT_ID"                            = data.azurerm_user_assigned_identity.mi_of.client_id
    "XDT_MicrosoftApplicationInsights_Mode"      = "default"
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~2"
    "WEBSITE_RUN_FROM_PACKAGE"                   = "1"
    "WEBSITE_SWAP_WARMUP_PING_PATH"              = "/api"
    "WEBSITE_SLOT_ENV"                           = "production"
    "WEBSITE_ENABLE_SYNC_UPDATE_SITE"            = "true"
    "FrontendExperience__Enabled"                 = tostring(var.enable_new_frontend)
    "FrontendExperience__ShowForAll"              = tostring(var.show_new_frontend_for_all)
    "FrontendExperience__NewFrontendUrl"          = var.enable_new_frontend ? "https://${azurerm_linux_web_app.webapp[0].default_hostname}" : ""
  }

  auth_settings_v2 {
    auth_enabled           = true
    default_provider       = "azureactivedirectory"
    require_authentication = true

    active_directory_v2 {
      client_id            = var.auth_client_id
      tenant_auth_endpoint = "https://sts.windows.net/${data.azurerm_client_config.current.tenant_id}/v2.0"
      allowed_audiences    = ["api://${var.auth_client_id}"]
    }

    login {
      token_store_enabled = true
    }
  }

  connection_string {
    name  = "SqlConnection"
    type  = "SQLServer"
    value = "@Microsoft.KeyVault(VaultName=${azurerm_key_vault.kv.name};SecretName=SqlConnection;ClientId=${data.azurerm_user_assigned_identity.mi_of.client_id})"
  }

  logs {
    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 30
      }
    }
    detailed_error_messages = true
    failed_request_tracing  = true
  }
}

resource "azurerm_windows_function_app" "api" {
  name                            = "fa${local.suffix}"
  location                        = local.region
  resource_group_name             = azurerm_resource_group.rg.name
  service_plan_id                 = azurerm_service_plan.plan.id
  storage_account_name            = azurerm_storage_account.storage.name
  storage_account_access_key      = null
  storage_uses_managed_identity   = true
  tags                            = local.app_insights_tags
  functions_extension_version     = "~4"
  https_only                      = true
  key_vault_reference_identity_id = data.azurerm_user_assigned_identity.mi_of.id
  virtual_network_subnet_id       = var.enable_private_access ? one(data.azurerm_subnet.vnet_integration[*].id) : null

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.containerapps.id, data.azurerm_user_assigned_identity.mi_of.id]
  }

  app_settings = {
    "APPINSIGHTS_INSTRUMENTATIONKEY"                  = azurerm_application_insights.insights.instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING"           = azurerm_application_insights.insights.connection_string
    "KeyVaultUri"                                     = azurerm_key_vault.kv.vault_uri
    "AZURE_CLIENT_ID"                                 = data.azurerm_user_assigned_identity.mi_of.client_id
    "StorageAccountName"                              = azurerm_storage_account.storage.name
    "IPGServiceBusConnection"                         = var.ipg_sbns_connection
    "IPGSubscriptionName"                             = var.ipg_sub_name
    "ServiceBusNamespace"                             = "${azurerm_servicebus_namespace.sbns.name}.servicebus.windows.net"
    "ServiceBusConnection__fullyQualifiedNamespace"   = "${azurerm_servicebus_namespace.sbns.name}.servicebus.windows.net"
    "ServiceBusConnection__clientId"                  = data.azurerm_user_assigned_identity.mi_of.client_id
    "FUNCTIONS_WORKER_RUNTIME"                        = "dotnet-isolated"
    "WEBSITE_RUN_FROM_PACKAGE"                        = "1"
    "WEBSITE_SWAP_WARMUP_PING_PATH"                   = "/api/status"
    "WEBSITE_SWAP_WARMUP_PING_STATUSES"               = "200"
    "WEBSITE_ADD_SITENAME_BINDINGS_IN_APPHOST_CONFIG" = "1"
  }

  site_config {
    always_on              = true
    http2_enabled          = true
    websockets_enabled     = true
    minimum_tls_version    = "1.2"
    ftps_state             = "FtpsOnly"
    use_32_bit_worker      = true
    vnet_route_all_enabled = var.enable_private_access

    application_stack {
      dotnet_version              = "v10.0"
      use_dotnet_isolated_runtime = true
    }
  }
}

# Container App Jobs and Environment removed June 22, 2026.
# Replaced by AKS CronJobs managed via Kustomize overlays in build/k8s/.
# Jobs were set to Manual trigger before removal to prevent duplicate runs.

###### Database (removed from Terraform management; resources remain in Azure)

removed {
  from = azurerm_mssql_server.mssql
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_mssql_database.db
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_sql_firewall_rule.azureresources
  lifecycle {
    destroy = false
  }
}

### Config

resource "azurerm_key_vault" "kv" {
  name                          = "kv-${local.suffix}"
  location                      = local.region
  resource_group_name           = azurerm_resource_group.rg.name
  enabled_for_disk_encryption   = true
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days    = 7
  purge_protection_enabled      = false
  sku_name                      = "standard"
  public_network_access_enabled = !var.enable_private_access
  tags                          = local.tags

  dynamic "network_acls" {
    for_each = var.enable_private_access ? [1] : []
    content {
      default_action = "Deny"
      bypass         = "AzureServices"
    }
  }

  dynamic "access_policy" {
    for_each = local.access_policy
    content {
      tenant_id               = access_policy.value.tenant_id
      object_id               = access_policy.value.object_id
      key_permissions         = coalesce(access_policy.value.key_permissions, [])
      secret_permissions      = coalesce(access_policy.value.secret_permissions, [])
      storage_permissions     = coalesce(access_policy.value.storage_permissions, [])
      certificate_permissions = coalesce(access_policy.value.certificate_permissions, [])
    }
  }
}
# Service Bus now uses managed identity (DefaultAzureCredential) via the
# ServiceBusNamespace app setting. No shared access key secrets are required.
# Role assignments azurerm_role_assignment.mi_of_servicebus and
# azurerm_role_assignment.containerapps_servicebus grant Azure Service Bus
# Data Owner on the namespace to the respective managed identities.

# StorageConnection KV secret removed: storage now uses managed identity.
# The secret can be deleted from Key Vault manually if it still exists.
removed {
  from = azurerm_key_vault_secret.kvsstorage
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_key_vault_secret.sbnsstorage
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_key_vault_secret.kvsauthsecret
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_key_vault_secret.kvsdbconnection
  lifecycle {
    destroy = false
  }
}

### Output

output "instrumentation_key" {
  value     = azurerm_application_insights.insights.instrumentation_key
  sensitive = true
}

output "app_id" {
  value = azurerm_application_insights.insights.app_id
}

output "storage_name" {
  value = azurerm_storage_account.storage.primary_table_endpoint
}

output "storage_connection" {
  value     = var.enable_private_access ? null : azurerm_storage_account.storage.primary_connection_string
  sensitive = true
}

output "vault_uri" {
  value = azurerm_key_vault.kv.vault_uri
}

output "app_service_name" {
  value = azurerm_windows_web_app.web.name
}

output "function_app_name" {
  value = azurerm_windows_function_app.api.name
}

### State Migration
#
# The azurerm provider does not support `moved` across resource types.
# Instead we use removed + import pairs:
#   - removed: forgets the old state address without destroying the Azure resource
#   - import:  brings the same Azure resource into state under the new address
#              (idempotent in TF 1.7+ — skipped if already in state)

removed {
  from = azurerm_app_service_plan.plan
  lifecycle {
    destroy = false
  }
}

import {
  to = azurerm_service_plan.plan
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.Web/serverFarms/plan-${local.suffix}"
}

removed {
  from = azurerm_app_service.web
  lifecycle {
    destroy = false
  }
}

import {
  to = azurerm_windows_web_app.web
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.Web/sites/as${local.suffix}"
}

removed {
  from = azurerm_function_app.api
  lifecycle {
    destroy = false
  }
}

import {
  to = azurerm_windows_function_app.api
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.Web/sites/fa${local.suffix}"
}

### Bootstrap Imports
#
# These blocks bring pre-existing Azure resources into Terraform state for
# environments whose state container was newly created (SIT, Test, etc.).
# Import blocks are idempotent in TF 1.7+ and are silently skipped if the
# resource is already in state, so they are safe to leave in permanently.

import {
  to = azurerm_resource_group.rg
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}"
}

import {
  to = azurerm_application_insights.insights
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.Insights/components/ai${local.suffix}"
}

import {
  to = azurerm_storage_account.storage
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.Storage/storageAccounts/st${local.suffix}"
}

import {
  to = azurerm_servicebus_namespace.sbns
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}"
}

import {
  to = azurerm_servicebus_queue.assets
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}/queues/assets"
}

import {
  to = azurerm_servicebus_queue.activate_header_queue
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}/queues/activateheader"
}

import {
  to = azurerm_servicebus_queue.activate_queue
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}/queues/activateagreement"
}

import {
  to = azurerm_servicebus_queue.quote_queue
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}/queues/upsertquote"
}

import {
  to = azurerm_servicebus_queue.agreement_queue
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}/queues/updatebyagreement"
}

import {
  to = azurerm_servicebus_queue.changenotify
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}/queues/changenotify"
}

import {
  to = azurerm_servicebus_queue.changeapproval
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}/queues/changeapproval"
}

import {
  to = azurerm_servicebus_queue.changecomplete
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}/queues/changecomplete"
}

import {
  to = azurerm_servicebus_queue.agreement-sync
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ServiceBus/namespaces/sbms${local.suffix}/queues/agreement-sync"
}

import {
  to = azurerm_user_assigned_identity.containerapps
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/uai-containerapps"
}

import {
  to = azurerm_key_vault.kv
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/rg${local.suffix}/providers/Microsoft.KeyVault/vaults/kv-${local.suffix}"
}
