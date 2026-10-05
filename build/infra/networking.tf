#==============================================================================
#
#  networking.tf
#
#  Private endpoints and VNet integration for OF resources.
#
#  Private endpoints are created by Terraform when enable_private_access = true
#  and vnet_subnet_root is provided.
#
#  Private DNS zones and VNet links are managed centrally by the cloud team.
#
#==============================================================================

locals {
  pe_enabled     = var.enable_private_access
  pe_name_suffix = "of-${lower(var.environment)}"
  pe_sa_types       = toset([
    "blob", 
    "file",
    "queue", 
    "table"
    ])
}

data "azurerm_private_dns_zone" "kv_private_link" {
  provider            = azurerm.hub
  name                = "privatelink.vaultcore.azure.net"
  resource_group_name = var.hub_resource_group
}

data "azurerm_private_dns_zone" "db_private_link" {
  provider            = azurerm.hub
  name                = "privatelink.database.windows.net"
  resource_group_name = var.hub_resource_group
}

data "azurerm_private_dns_zone" "sa_private_link" {
  for_each            = local.pe_sa_types
  provider            = azurerm.hub
  name                = "privatelink.${each.value}.core.windows.net"
  resource_group_name = var.hub_resource_group
}

#------------------------------------------------------------------------------
# Key Vault Private Endpoint
#------------------------------------------------------------------------------
resource "azurerm_private_endpoint" "pe_kv" {
  for_each                      = local.pe_enabled ? toset(["enabled"]) : toset([])
  name                          = "pe-kv-${local.pe_name_suffix}"
  location                      = azurerm_resource_group.rg.location
  resource_group_name           = azurerm_resource_group.rg.name
  subnet_id                     = data.azurerm_subnet.pe[0].id
  custom_network_interface_name = "pe-kv-${local.pe_name_suffix}-nic"
  tags                          = local.tags

  private_service_connection {
    name                           = "pe-kv-${local.pe_name_suffix}"
    private_connection_resource_id = azurerm_key_vault.kv.id
    subresource_names              = ["vault"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [data.azurerm_private_dns_zone.kv_private_link.id]
  }
}

#------------------------------------------------------------------------------
# SQL Server Private Endpoint
#------------------------------------------------------------------------------
resource "azurerm_private_endpoint" "pe_sql" {
  for_each                      = local.pe_enabled ? toset(["enabled"]) : toset([])
  name                          = "pe-mssql-${local.suffix}-db-${local.suffix}-${lower(var.environment)}"
  location                      = azurerm_resource_group.rg.location
  resource_group_name           = azurerm_resource_group.rg.name
  subnet_id                     = data.azurerm_subnet.pe[0].id
  custom_network_interface_name = "pe-mssql-${local.suffix}-db-${local.suffix}-${lower(var.environment)}-nic"
  tags                          = local.tags

  private_service_connection {
    name                           = "pe-mssql-${local.suffix}-db-${local.suffix}-${lower(var.environment)}"
    private_connection_resource_id = data.azurerm_mssql_server.mssql.id
    subresource_names              = ["sqlServer"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [data.azurerm_private_dns_zone.db_private_link.id]
  }
}

#------------------------------------------------------------------------------
# Storage Account Private Endpoints (blob, file, queue, table)
#------------------------------------------------------------------------------
resource "azurerm_private_endpoint" "pe_sa_blob" {
  for_each                      = local.pe_enabled ? toset(["enabled"]) : toset([])
  name                          = "pe-sa-blob-${local.pe_name_suffix}"
  location                      = azurerm_resource_group.rg.location
  resource_group_name           = azurerm_resource_group.rg.name
  subnet_id                     = data.azurerm_subnet.pe[0].id
  custom_network_interface_name = "pe-sa-blob-${local.pe_name_suffix}-nic"
  tags                          = local.tags

  private_service_connection {
    name                           = "pe-sa-blob-${local.pe_name_suffix}"
    private_connection_resource_id = azurerm_storage_account.storage.id
    subresource_names              = ["blob"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [data.azurerm_private_dns_zone.sa_private_link["blob"].id]
  }
}

resource "azurerm_private_endpoint" "pe_sa_file" {
  for_each                      = local.pe_enabled ? toset(["enabled"]) : toset([])
  name                          = "pe-sa-file-${local.pe_name_suffix}"
  location                      = azurerm_resource_group.rg.location
  resource_group_name           = azurerm_resource_group.rg.name
  subnet_id                     = data.azurerm_subnet.pe[0].id
  custom_network_interface_name = "pe-sa-file-${local.pe_name_suffix}-nic"
  tags                          = local.tags

  private_service_connection {
    name                           = "pe-sa-file-${local.pe_name_suffix}"
    private_connection_resource_id = azurerm_storage_account.storage.id
    subresource_names              = ["file"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [data.azurerm_private_dns_zone.sa_private_link["file"].id]
  }
}

resource "azurerm_private_endpoint" "pe_sa_queue" {
  for_each                      = local.pe_enabled ? toset(["enabled"]) : toset([])
  name                          = "pe-sa-queue-${local.pe_name_suffix}"
  location                      = azurerm_resource_group.rg.location
  resource_group_name           = azurerm_resource_group.rg.name
  subnet_id                     = data.azurerm_subnet.pe[0].id
  custom_network_interface_name = "pe-sa-queue-${local.pe_name_suffix}-nic"
  tags                          = local.tags

  private_service_connection {
    name                           = "pe-sa-queue-${local.pe_name_suffix}"
    private_connection_resource_id = azurerm_storage_account.storage.id
    subresource_names              = ["queue"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [data.azurerm_private_dns_zone.sa_private_link["queue"].id]
  }
}

resource "azurerm_private_endpoint" "pe_sa_table" {
  for_each                      = local.pe_enabled ? toset(["enabled"]) : toset([])
  name                          = "pe-sa-table-${local.pe_name_suffix}"
  location                      = azurerm_resource_group.rg.location
  resource_group_name           = azurerm_resource_group.rg.name
  subnet_id                     = data.azurerm_subnet.pe[0].id
  custom_network_interface_name = "pe-sa-table-${local.pe_name_suffix}-nic"
  tags                          = local.tags

  private_service_connection {
    name                           = "pe-sa-table-${local.pe_name_suffix}"
    private_connection_resource_id = azurerm_storage_account.storage.id
    subresource_names              = ["table"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [data.azurerm_private_dns_zone.sa_private_link["table"].id]
  }
}

#------------------------------------------------------------------------------
# VNet Integration
#
# Previously managed via azurerm_app_service_virtual_network_swift_connection
# resources. Now handled natively via virtual_network_subnet_id on the
# upgraded azurerm_windows_web_app and azurerm_windows_function_app resources.
# The removed blocks below prevent Terraform from destroying the Azure-side
# VNet integration during the migration.
#------------------------------------------------------------------------------
removed {
  from = azurerm_app_service_virtual_network_swift_connection.web_vnet
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_app_service_virtual_network_swift_connection.web_pricing_vnet
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_app_service_virtual_network_swift_connection.api_vnet
  lifecycle {
    destroy = false
  }
}
