variable "tenant_id" {
  description = "Azure AD tenant ID"
  type        = string
}

variable "google_client_id" {
  description = "Google OAuth client ID"
  type        = string
  default     = ""
}

variable "google_client_secret" {
  description = "Google OAuth client secret"
  type        = string
  sensitive   = true
  default     = ""
}

variable "user_portal_production_url" {
  description = "Production URL for user portal"
  type        = string
  default     = ""
}

variable "admin_portal_production_url" {
  description = "Production URL for admin portal"
  type        = string
  default     = ""
}
