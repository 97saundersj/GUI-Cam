provider "azurerm" {
  features {}

  # Avoid registering dozens of unused providers on first plan (can take 10+ minutes).
  resource_provider_registrations = "none"
}
