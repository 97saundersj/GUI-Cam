variable "location" {
  description = "Azure region for all resources."
  type        = string
  default     = "ukwest"
}

variable "resource_group_name" {
  description = "Name of the resource group."
  type        = string
  default     = "rg-gui-cam"
}

variable "tags" {
  description = "Tags applied to all resources."
  type        = map(string)
  default = {
    project = "gui-cam"
  }
}

variable "log_analytics_workspace_name" {
  description = "Name of the Log Analytics workspace backing the Container Apps environment."
  type        = string
  default     = "law-gui-cam"
}

variable "container_apps_environment_name" {
  description = "Name of the Azure Container Apps environment."
  type        = string
  default     = "cae-gui-cam"
}

variable "converter_app_name" {
  description = "Name of the MediaMTX stream converter Container App."
  type        = string
  default     = "gui-cam-converter"
}

variable "converter_image" {
  description = "Container image for the stream converter."
  type        = string
  default     = "bluenviron/mediamtx:latest"
}

variable "converter_cpu" {
  description = "vCPU cores allocated to the converter container."
  type        = number
  default     = 0.5
}

variable "converter_memory" {
  description = "Memory allocated to the converter container (e.g. 1Gi)."
  type        = string
  default     = "1Gi"
}

variable "converter_min_replicas" {
  description = "Minimum number of converter replicas (1 keeps the stream warm)."
  type        = number
  default     = 1
}

variable "converter_max_replicas" {
  description = "Maximum number of converter replicas."
  type        = number
  default     = 1
}

variable "mtx_hls_address" {
  description = "MediaMTX HLS listen address."
  type        = string
  default     = ":8888"
}

variable "mtx_paths_cam_source" {
  description = "RTSP URL for the primary camera path /cam (contains credentials — set via terraform.tfvars, never commit)."
  type        = string
  sensitive   = true
}

variable "mtx_paths_cam2_source" {
  description = "RTSP URL for the secondary camera path /cam2 (optional; omit or set null to disable)."
  type        = string
  sensitive   = true
  default     = null
}

variable "mtx_hls_variant" {
  description = "MediaMTX HLS variant (lowLatency for LL-HLS)."
  type        = string
  default     = "lowLatency"
}

variable "mtx_hls_segment_duration" {
  description = "MediaMTX HLS segment duration."
  type        = string
  default     = "1s"
}

variable "mtx_hls_part_duration" {
  description = "MediaMTX LL-HLS part duration."
  type        = string
  default     = "200ms"
}

variable "mtx_hls_always_remux" {
  description = "MediaMTX always remux setting (yes avoids first-viewer delay)."
  type        = string
  default     = "yes"
}

variable "onvif_api_app_name" {
  description = "Name of the ONVIF API App Service."
  type        = string
  default     = "gui-cam-onvif-api"
}

variable "onvif_api_service_plan_name" {
  description = "Name of the App Service plan hosting the ONVIF API."
  type        = string
  default     = "asp-gui-cam-onvif"
}

variable "onvif_api_service_plan_sku" {
  description = "App Service plan SKU (F1 = free, B1 = basic always-on)."
  type        = string
  default     = "F1"
}

variable "onvif_api_publish_dir" {
  description = "Path to dotnet publish output, relative to terraform/. Publish before apply."
  type        = string
  default     = ".deploy/onvif-api"
}

variable "deploy_onvif_api" {
  description = "Zip and deploy the pre-published API package to App Service on terraform apply."
  type        = bool
  default     = true
}

variable "tapo_service_base_url" {
  description = "Base URL of the PyTapo HTTP service (TapoService__BaseUrl on the ONVIF API App Service)."
  type        = string
  default     = "http://localhost:5246"
}

variable "tapo_password_cloud" {
  description = "Tapo cloud account password for SD-card access (TapoService__PasswordCloud)."
  type        = string
  sensitive   = true
  default     = ""
}

variable "web_storage_account_name" {
  description = "Globally unique name for the static web storage account (3–24 lowercase letters and numbers)."
  type        = string
  default     = "guicam"
}

variable "web_dist_dir" {
  description = "Path to the built Vue app, relative to terraform/. Populated by the web deploy step."
  type        = string
  default     = ".deploy/web-dist"
}

variable "deploy_web" {
  description = "Build the Vue app and upload it to Azure Storage static website on terraform apply."
  type        = bool
  default     = true
}

variable "vite_hls_low_latency" {
  description = "VITE_HLS_LOW_LATENCY value baked into the web build."
  type        = string
  default     = "true"
}

variable "vite_onvif_uri" {
  description = "VITE_ONVIF_URI for PTZ controls (optional)."
  type        = string
  sensitive   = true
  default     = null
}

variable "vite_onvif_user" {
  description = "VITE_ONVIF_USER for PTZ controls (optional; embedded in the built site)."
  type        = string
  sensitive   = true
  default     = null
}

variable "vite_onvif_password" {
  description = "VITE_ONVIF_PASSWORD for PTZ controls (optional; embedded in the built site)."
  type        = string
  sensitive   = true
  default     = null
}

variable "vite_onvif_uri_2" {
  description = "VITE_ONVIF_URI_2 for camera 2 recordings host (optional)."
  type        = string
  sensitive   = true
  default     = null
}

variable "vite_tapo_host_2" {
  description = "VITE_TAPO_HOST_2 override for camera 2 recordings (optional)."
  type        = string
  sensitive   = true
  default     = null
}
