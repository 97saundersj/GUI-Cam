resource "azurerm_storage_account" "web" {
  name                     = var.web_storage_account_name
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "StorageV2"
  min_tls_version          = "TLS1_2"
  tags                     = var.tags
}

resource "azurerm_storage_account_static_website" "web" {
  storage_account_id = azurerm_storage_account.web.id
  index_document     = "index.html"
  error_404_document = "index.html"
}

locals {
  web_hls_stream_url  = "https://${azurerm_container_app.converter.ingress[0].fqdn}/cam/index.m3u8"
  web_hls_stream2_url = "https://${azurerm_container_app.converter.ingress[0].fqdn}/cam2/index.m3u8"
  web_onvif_api_url   = "https://${azurerm_linux_web_app.onvif_api.default_hostname}"

  web_source_files = concat(
    tolist(fileset("${path.module}/../web", "index.html")),
    [for f in fileset("${path.module}/../web/src", "**") : "src/${f}"],
    [for f in fileset("${path.module}/../web/public", "**") : "public/${f}"],
    ["package-lock.json"],
  )

  web_source_hash = sha256(join("", [
    for f in local.web_source_files : filesha256("${path.module}/../web/${f}")
  ]))

  web_build_config_hash = sha256(jsonencode({
    stream_url      = local.web_hls_stream_url
    stream2_url     = local.web_hls_stream2_url
    onvif_api_url   = local.web_onvif_api_url
    onvif_uri       = coalesce(var.vite_onvif_uri, "")
    onvif_uri_2     = coalesce(var.vite_onvif_uri_2, "")
    tapo_host_2     = coalesce(var.vite_tapo_host_2, "")
    onvif_user      = coalesce(var.vite_onvif_user, "")
    onvif_password  = coalesce(var.vite_onvif_password, "")
    hls_low_latency = var.vite_hls_low_latency
  }))
}

resource "null_resource" "web_deploy" {
  count = var.deploy_web ? 1 : 0

  depends_on = [
    azurerm_storage_account.web,
    azurerm_storage_account_static_website.web,
    azurerm_container_app.converter,
    azurerm_linux_web_app.onvif_api,
  ]

  triggers = {
    source_hash        = local.web_source_hash
    config_hash        = local.web_build_config_hash
    storage_account_id = azurerm_storage_account.web.id
  }

  provisioner "local-exec" {
    working_dir = path.module
    interpreter = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]
    command     = <<-EOT
      $ErrorActionPreference = 'Stop'
      $webDir = (Resolve-Path '..\web').Path
      $distDir = (Join-Path (Get-Location) '${replace(var.web_dist_dir, "/", "\\")}')
      if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }
      New-Item -ItemType Directory -Path $distDir -Force | Out-Null

      $envFile = Join-Path $webDir '.env'
      $envBackup = Join-Path $webDir '.env.terraform-bak'
      $prodEnv = Join-Path $webDir '.env.production.local'
      if (Test-Path $envFile) {
        Move-Item $envFile $envBackup -Force
      }
      Remove-Item -Recurse -Force (Join-Path $webDir 'dist'), (Join-Path $webDir 'node_modules\.vite') -ErrorAction SilentlyContinue

      @(
        'VITE_BASE_PATH=/',
        'VITE_STREAM_URL=${local.web_hls_stream_url}',
        'VITE_STREAM_URL_2=${local.web_hls_stream2_url}',
        'VITE_ONVIF_API_URL=${local.web_onvif_api_url}',
        'VITE_HLS_LOW_LATENCY=${var.vite_hls_low_latency}',
        'VITE_ONVIF_URI=${coalesce(var.vite_onvif_uri, "")}',
        'VITE_ONVIF_URI_2=${coalesce(var.vite_onvif_uri_2, "")}',
        'VITE_TAPO_HOST_2=${coalesce(var.vite_tapo_host_2, "")}',
        'VITE_ONVIF_USER=${coalesce(var.vite_onvif_user, "")}',
        'VITE_ONVIF_PASSWORD=${coalesce(var.vite_onvif_password, "")}'
      ) | Set-Content $prodEnv -Encoding utf8

      Push-Location $webDir
      try {
        if (-not (Test-Path 'node_modules\.bin\vite.cmd')) {
          npm install
          if ($LASTEXITCODE -ne 0) { throw "npm install failed (exit $LASTEXITCODE)" }
        }
        # Override any machine/user VITE_* vars — Vite prefers those over .env files.
        $env:VITE_BASE_PATH = '/'
        $env:VITE_STREAM_URL = '${local.web_hls_stream_url}'
        $env:VITE_STREAM_URL_2 = '${local.web_hls_stream2_url}'
        $env:VITE_ONVIF_API_URL = '${local.web_onvif_api_url}'
        $env:VITE_HLS_LOW_LATENCY = '${var.vite_hls_low_latency}'
        $env:VITE_ONVIF_URI = '${coalesce(var.vite_onvif_uri, "")}'
        $env:VITE_ONVIF_URI_2 = '${coalesce(var.vite_onvif_uri_2, "")}'
        $env:VITE_TAPO_HOST_2 = '${coalesce(var.vite_tapo_host_2, "")}'
        $env:VITE_ONVIF_USER = '${coalesce(var.vite_onvif_user, "")}'
        $env:VITE_ONVIF_PASSWORD = '${coalesce(var.vite_onvif_password, "")}'
        npx vite build
        if ($LASTEXITCODE -ne 0) { throw "vite build failed (exit $LASTEXITCODE)" }
      } finally {
        Pop-Location
        Remove-Item $prodEnv -Force -ErrorAction SilentlyContinue
        if (Test-Path $envBackup) {
          Move-Item $envBackup $envFile -Force
        }
      }

      Copy-Item -Path (Join-Path $webDir 'dist\*') -Destination $distDir -Recurse -Force

      az storage blob upload-batch `
        --account-name ${azurerm_storage_account.web.name} `
        --account-key '${azurerm_storage_account.web.primary_access_key}' `
        --destination '$web' `
        --source $distDir `
        --overwrite true `
        --content-cache-control "max-age=0, no-cache, must-revalidate"
    EOT
  }
}
