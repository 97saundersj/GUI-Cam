output "resource_group_name" {
  description = "Name of the deployed resource group."
  value       = azurerm_resource_group.main.name
}

output "container_app_environment_id" {
  description = "ID of the Container Apps environment."
  value       = azurerm_container_app_environment.main.id
}

output "converter_app_name" {
  description = "Name of the stream converter Container App."
  value       = azurerm_container_app.converter.name
}

output "converter_fqdn" {
  description = "Public FQDN of the stream converter."
  value       = azurerm_container_app.converter.ingress[0].fqdn
}

output "hls_stream_url" {
  description = "Default HLS playlist URL for the web player (set as VITE_STREAM_URL in GitHub Actions)."
  value       = "https://${azurerm_container_app.converter.ingress[0].fqdn}/cam/index.m3u8"
}

output "hls_stream2_url" {
  description = "HLS playlist URL for the secondary stream (set as VITE_STREAM_URL_2 in GitHub Actions)."
  value       = "https://${azurerm_container_app.converter.ingress[0].fqdn}/cam2/index.m3u8"
}

output "onvif_api_app_name" {
  description = "Name of the ONVIF API App Service."
  value       = azurerm_linux_web_app.onvif_api.name
}

output "onvif_api_url" {
  description = "Base URL for the ONVIF API (set as VITE_ONVIF_API_URL in GitHub Actions)."
  value       = "https://${azurerm_linux_web_app.onvif_api.default_hostname}"
}

output "web_storage_account_name" {
  description = "Storage account hosting the static web UI."
  value       = azurerm_storage_account.web.name
}

output "web_url" {
  description = "Public HTTPS URL for the web UI (Azure Storage static website)."
  value       = azurerm_storage_account.web.primary_web_endpoint
}
