output "external_id_tenant_id" {
  value       = var.tenant_id
  description = "External ID tenant ID (same as Azure AD tenant for now)"
}

output "api_client_id" {
  value       = azuread_application.api.client_id
  description = "API application client ID"
}

output "api_client_secret" {
  value       = azuread_application_password.api_secret.value
  sensitive   = true
  description = "API application client secret"
}

output "user_portal_client_id" {
  value       = azuread_application.user_portal.client_id
  description = "User portal application client ID"
}

output "admin_portal_client_id" {
  value       = azuread_application.admin_portal.client_id
  description = "Admin portal application client ID"
}

output "authority_url" {
  value       = "https://login.microsoftonline.com/${var.tenant_id}"
  description = "Authority URL for authentication"
}
