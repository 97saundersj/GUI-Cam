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
  description = "RTSP URL for the camera (contains credentials — set via terraform.tfvars, never commit)."
  type        = string
  sensitive   = true
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
