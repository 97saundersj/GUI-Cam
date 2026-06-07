resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
  tags     = var.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = var.log_analytics_workspace_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_container_app_environment" "main" {
  name                       = var.container_apps_environment_name
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = var.tags
}

resource "azurerm_container_app" "converter" {
  name                         = var.converter_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = var.tags

  secret {
    name  = "rtsp-source"
    value = var.mtx_paths_cam_source
  }

  template {
    min_replicas = var.converter_min_replicas
    max_replicas = var.converter_max_replicas

    container {
      name   = "mediamtx"
      image  = var.converter_image
      cpu    = var.converter_cpu
      memory = var.converter_memory

      env {
        name  = "MTX_HLSADDRESS"
        value = var.mtx_hls_address
      }

      env {
        name        = "MTX_PATHS_CAM_SOURCE"
        secret_name = "rtsp-source"
      }

      env {
        name  = "MTX_HLSVARIANT"
        value = var.mtx_hls_variant
      }

      env {
        name  = "MTX_HLSSEGMENTDURATION"
        value = var.mtx_hls_segment_duration
      }

      env {
        name  = "MTX_HLSPARTDURATION"
        value = var.mtx_hls_part_duration
      }

      env {
        name  = "MTX_HLSALWAYSREMUX"
        value = var.mtx_hls_always_remux
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8888
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
}
