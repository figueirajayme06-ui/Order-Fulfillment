# OF.WebApp - containerised React SPA and API host.
# Container Apps is intentionally not used: the former Container Apps
# Environment was retired when background jobs moved to AKS.

resource "azurerm_service_plan" "webapp_linux" {
  count = var.enable_new_frontend ? 1 : 0

  name                = "plan-ofwebapp-${lower(var.environment)}"
  location            = local.region
  resource_group_name = azurerm_resource_group.rg.name
  os_type             = "Linux"
  sku_name            = var.app_sku
  tags                = local.tags
}

resource "azurerm_linux_web_app" "webapp" {
  count = var.enable_new_frontend ? 1 : 0

  name                            = "asofwebapp${lower(var.environment)}"
  location                        = local.region
  resource_group_name             = azurerm_resource_group.rg.name
  service_plan_id                 = azurerm_service_plan.webapp_linux[0].id
  https_only                      = true
  public_network_access_enabled   = true
  key_vault_reference_identity_id = azurerm_user_assigned_identity.containerapps.id
  virtual_network_subnet_id       = var.enable_private_access ? one(data.azurerm_subnet.vnet_integration[*].id) : null
  vnet_image_pull_enabled         = var.enable_private_access
  tags                            = local.app_insights_tags

  identity {
    type = "UserAssigned"
    identity_ids = [
      azurerm_user_assigned_identity.containerapps.id,
      data.azurerm_user_assigned_identity.mi_of.id,
    ]
  }

  site_config {
    always_on                                     = true
    http2_enabled                                 = true
    minimum_tls_version                           = "1.2"
    ftps_state                                    = "Disabled"
    vnet_route_all_enabled                        = var.enable_private_access
    health_check_path                             = "/health/ready"
    health_check_eviction_time_in_min             = 5
    container_registry_use_managed_identity       = true
    container_registry_managed_identity_client_id = azurerm_user_assigned_identity.containerapps.client_id

    application_stack {
      docker_image_name   = "nof/of-webapp:${var.app_version}"
      docker_registry_url = "https://${local.cr_login_server}"
    }
  }

  app_settings = merge(
    { for setting in local.container_common_settings : setting.name => setting.value },
    {
      "ASPNETCORE_ENVIRONMENT"                 = "Production"
      "ASPNETCORE_FORWARDEDHEADERS_ENABLED"    = "true"
      "WEBSITES_PORT"                          = "8080"
      "ServiceBusNamespace"                    = "${azurerm_servicebus_namespace.sbns.name}.servicebus.windows.net"
      "FrontendExperience__EnvironmentLabel"   = "OF ${var.environment}"
      "FrontendExperience__LegacyFrontendUrl"  = "https://as${local.suffix}.azurewebsites.net"
      "FrontendExperience__ShowPreviewBanner"  = "true"
      "FrontendExperience__ShowDeploymentInfo" = tostring(local.is_pre_live)
      "FrontendExperience__AppVersion"         = var.app_version
      "WEBSITES_CONTAINER_START_TIME_LIMIT"    = "600"
    }
  )

  auth_settings_v2 {
    auth_enabled           = true
    require_authentication = true
    unauthenticated_action = "RedirectToLoginPage"
    default_provider       = "azureactivedirectory"
    excluded_paths         = ["/health/live", "/health/ready"]

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
    value = "@Microsoft.KeyVault(VaultName=${azurerm_key_vault.kv.name};SecretName=SqlConnection;ClientId=${azurerm_user_assigned_identity.containerapps.client_id})"
  }

  logs {
    detailed_error_messages = true
    failed_request_tracing  = true

    application_logs {
      file_system_level = "Information"
    }

    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 30
      }
    }
  }

  lifecycle {
    # The application pipeline owns the immutable image tag after provisioning.
    ignore_changes = [site_config[0].application_stack[0].docker_image_name]
  }
}

output "new_frontend_url" {
  value = var.enable_new_frontend ? "https://${azurerm_linux_web_app.webapp[0].default_hostname}" : null
}

output "new_frontend_auth_callback_url" {
  value = var.enable_new_frontend ? "https://${azurerm_linux_web_app.webapp[0].default_hostname}/.auth/login/aad/callback" : null
}
