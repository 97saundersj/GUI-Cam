resource "azurerm_service_plan" "onvif_api" {
  name                = var.onvif_api_service_plan_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  os_type             = "Linux"
  sku_name            = var.onvif_api_service_plan_sku
  tags                = var.tags
}

resource "azurerm_linux_web_app" "onvif_api" {
  name                = var.onvif_api_app_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  service_plan_id     = azurerm_service_plan.onvif_api.id
  https_only          = true
  tags                = var.tags

  site_config {
    always_on = var.onvif_api_service_plan_sku != "F1"

    application_stack {
      dotnet_version = "8.0"
    }
  }

  app_settings = {
    ASPNETCORE_ENVIRONMENT     = "Production"
    TapoService__BaseUrl       = local.tapo_service_base_url
    TapoService__PasswordCloud = var.tapo_password_cloud
  }

  depends_on = [azurerm_container_app.pytapo]
}

locals {
  onvif_api_publish_path = "${path.module}/${var.onvif_api_publish_dir}"

  onvif_api_source_files = concat(
    tolist(fileset("${path.module}/../api/OnvifApi", "**/*.cs")),
    tolist(fileset("${path.module}/../api/OnvifApi", "**/*.csproj")),
    tolist(fileset("${path.module}/../api/OnvifApi", "**/*.json")),
  )

  onvif_api_source_hash = sha256(join("", [
    for f in local.onvif_api_source_files : filesha256("${path.module}/../api/OnvifApi/${f}")
  ]))
}

resource "null_resource" "onvif_api_deploy" {
  count = var.deploy_onvif_api ? 1 : 0

  depends_on = [azurerm_linux_web_app.onvif_api]

  triggers = {
    source_hash = local.onvif_api_source_hash
  }

  provisioner "local-exec" {
    working_dir = path.module
    interpreter = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command     = <<-EOT
      $ErrorActionPreference = 'Stop'
      $publishDir = '${replace(var.onvif_api_publish_dir, "/", "\\")}'
      if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
      New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
      dotnet publish ..\api\OnvifApi\OnvifApi.csproj -c Release -o $publishDir
      if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
      $zipPath = '.deploy\onvif-api.zip'
      if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
      tar -caf $zipPath -C $publishDir .
      $zip = (Resolve-Path $zipPath).Path
      az webapp deploy --resource-group ${azurerm_resource_group.main.name} --name ${azurerm_linux_web_app.onvif_api.name} --src-path $zip --type zip
    EOT
  }
}
