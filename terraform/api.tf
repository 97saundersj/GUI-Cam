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
    TapoService__BaseUrl       = var.tapo_service_base_url
    TapoService__PasswordCloud = var.tapo_password_cloud
  }
}

locals {
  onvif_api_publish_path = "${path.module}/${var.onvif_api_publish_dir}"
  onvif_api_publish_files = try(
    sort(fileset(local.onvif_api_publish_path, "**")),
    []
  )
  onvif_api_publish_hash = length(local.onvif_api_publish_files) > 0 ? sha256(join("", [
    for f in local.onvif_api_publish_files : filesha256("${local.onvif_api_publish_path}/${f}")
  ])) : "not-published"
}

resource "null_resource" "onvif_api_deploy" {
  count = var.deploy_onvif_api ? 1 : 0

  depends_on = [azurerm_linux_web_app.onvif_api]

  triggers = {
    publish_output = local.onvif_api_publish_hash
  }

  provisioner "local-exec" {
    working_dir = path.module
    interpreter = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command     = <<-EOT
      $ErrorActionPreference = 'Stop'
      $publishDir = '${replace(var.onvif_api_publish_dir, "/", "\\")}'
      $entryDll = Join-Path $publishDir 'OnvifApi.dll'
      if (-not (Test-Path $entryDll)) {
        throw "Publish the API first: dotnet publish ..\api\OnvifApi\OnvifApi.csproj -c Release -o $publishDir"
      }
      $zipPath = '.deploy\onvif-api.zip'
      if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
      tar -caf $zipPath -C $publishDir .
      $zip = (Resolve-Path $zipPath).Path
      az webapp deploy --resource-group ${azurerm_resource_group.main.name} --name ${azurerm_linux_web_app.onvif_api.name} --src-path $zip --type zip
    EOT
  }
}
