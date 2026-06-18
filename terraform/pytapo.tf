resource "azurerm_container_registry" "main" {
  name                = var.container_registry_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = true
  tags                = var.tags
}

locals {
  pytapo_source_files = concat(
    tolist(fileset("${path.module}/../pytapo", "**/*.py")),
    ["Dockerfile", "requirements.txt"],
  )

  pytapo_source_hash = sha256(join("", [
    for f in local.pytapo_source_files : filesha256("${path.module}/../pytapo/${f}")
  ]))

  pytapo_image_tag         = substr(local.pytapo_source_hash, 0, 12)
  pytapo_image             = "${azurerm_container_registry.main.login_server}/gui-cam-pytapo:${local.pytapo_image_tag}"
  tapo_service_base_url    = var.tapo_service_base_url != null && var.tapo_service_base_url != "" ? var.tapo_service_base_url : "https://${azurerm_container_app.pytapo.ingress[0].fqdn}"
}

resource "null_resource" "pytapo_image" {
  count = var.deploy_pytapo ? 1 : 0

  depends_on = [azurerm_container_registry.main]

  triggers = {
    source_hash = local.pytapo_source_hash
  }

  provisioner "local-exec" {
    working_dir = path.module
    interpreter = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command     = <<-EOT
      $ErrorActionPreference = 'Stop'
      $image = '${local.pytapo_image}'
      az acr login --name ${azurerm_container_registry.main.name}
      if ($LASTEXITCODE -ne 0) { throw "az acr login failed (exit $LASTEXITCODE)" }
      docker build -t $image -f ..\pytapo\Dockerfile ..\pytapo
      if ($LASTEXITCODE -ne 0) { throw "docker build failed (exit $LASTEXITCODE)" }
      docker push $image
      if ($LASTEXITCODE -ne 0) { throw "docker push failed (exit $LASTEXITCODE)" }
    EOT
  }
}

resource "azurerm_container_app" "pytapo" {
  name                         = var.pytapo_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"
  tags                         = var.tags

  depends_on = [null_resource.pytapo_image]

  registry {
    server               = azurerm_container_registry.main.login_server
    username             = azurerm_container_registry.main.admin_username
    password_secret_name = "acr-password"
  }

  secret {
    name  = "acr-password"
    value = azurerm_container_registry.main.admin_password
  }

  secret {
    name  = "tapo-password-cloud"
    value = var.tapo_password_cloud
  }

  template {
    min_replicas = var.pytapo_min_replicas
    max_replicas = var.pytapo_max_replicas

    container {
      name   = "pytapo"
      image  = local.pytapo_image
      cpu    = var.pytapo_cpu
      memory = var.pytapo_memory

      env {
        name        = "TAPO_PASSWORD_CLOUD"
        secret_name = "tapo-password-cloud"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8000
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
}
