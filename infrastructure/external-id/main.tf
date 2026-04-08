terraform {
  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    null = {
      source  = "hashicorp/null"
      version = "~> 3.0"
    }
  }
}

provider "azuread" {
  tenant_id = var.tenant_id
}

# API App Registration
resource "azuread_application" "api" {
  display_name = "Gymnastics Platform API"

  # Allow Microsoft personal accounts and organizational accounts
  sign_in_audience = "AzureADandPersonalMicrosoftAccount"

  api {
    # Required for multi-tenant apps with personal Microsoft accounts
    requested_access_token_version = 2

    oauth2_permission_scope {
      admin_consent_description  = "Allows access to the Gymnastics Platform API"
      admin_consent_display_name = "Access Gymnastics API"
      id                         = "00000000-0000-0000-0000-000000000001"
      type                       = "User"
      user_consent_description   = "Allows access to the Gymnastics Platform"
      user_consent_display_name  = "Access Gymnastics API"
      value                      = "user.access"
    }
  }

  identifier_uris = ["api://${var.tenant_id}/gymnastics-api"]
}

resource "azuread_service_principal" "api" {
  client_id = azuread_application.api.client_id
}

resource "azuread_application_password" "api_secret" {
  application_id = azuread_application.api.id
  display_name   = "API Graph Access Secret"
}

# User Portal SPA Registration
resource "azuread_application" "user_portal" {
  display_name = "Gymnastics User Portal"

  # Allow Microsoft personal accounts and organizational accounts
  sign_in_audience = "AzureADandPersonalMicrosoftAccount"

  # Required for multi-tenant apps with personal Microsoft accounts
  api {
    requested_access_token_version = 2
  }

  single_page_application {
    redirect_uris = compact([
      "http://localhost:5173/auth/callback",
      var.user_portal_production_url != "" ? "${var.user_portal_production_url}/auth/callback" : ""
    ])
  }

  required_resource_access {
    resource_app_id = azuread_application.api.client_id

    resource_access {
      id   = "00000000-0000-0000-0000-000000000001"
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "user_portal" {
  client_id = azuread_application.user_portal.client_id
}

# Admin Portal SPA Registration
resource "azuread_application" "admin_portal" {
  display_name = "Gymnastics Admin Portal"

  # Allow Microsoft personal accounts and organizational accounts
  sign_in_audience = "AzureADandPersonalMicrosoftAccount"

  # Required for multi-tenant apps with personal Microsoft accounts
  api {
    requested_access_token_version = 2
  }

  single_page_application {
    redirect_uris = compact([
      "http://localhost:3002/auth/callback",
      var.admin_portal_production_url != "" ? "${var.admin_portal_production_url}/auth/callback" : ""
    ])
  }

  required_resource_access {
    resource_app_id = azuread_application.api.client_id

    resource_access {
      id   = "00000000-0000-0000-0000-000000000001"
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "admin_portal" {
  client_id = azuread_application.admin_portal.client_id
}

# Configure Google as an identity provider using local-exec
# Note: This uses Azure CLI since the azuread provider doesn't yet support External ID identity providers
resource "null_resource" "google_identity_provider" {
  count = var.google_client_id != "" ? 1 : 0

  triggers = {
    google_client_id     = var.google_client_id
    google_client_secret = var.google_client_secret
  }

  provisioner "local-exec" {
    command = <<-EOT
      # Get access token using service principal
      TOKEN=$(curl -s -X POST \
        "https://login.microsoftonline.com/${var.tenant_id}/oauth2/v2.0/token" \
        -d "client_id=$ARM_CLIENT_ID" \
        -d "client_secret=$ARM_CLIENT_SECRET" \
        -d "scope=https://graph.microsoft.com/.default" \
        -d "grant_type=client_credentials" \
        | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)

      # Check if Google identity provider already exists
      EXISTING_IDP=$(curl -s -X GET \
        "https://graph.microsoft.com/beta/identity/identityProviders" \
        -H "Authorization: Bearer $TOKEN" \
        -H "Content-Type: application/json" \
        | grep -o '"id":"[^"]*","type":"Google"' | cut -d'"' -f4 || echo "")

      if [ -z "$EXISTING_IDP" ]; then
        echo "Creating Google identity provider..."
        curl -X POST \
          "https://graph.microsoft.com/beta/identity/identityProviders" \
          -H "Authorization: Bearer $TOKEN" \
          -H "Content-Type: application/json" \
          -d '{
            "@odata.type": "microsoft.graph.socialIdentityProvider",
            "type": "Google",
            "name": "Google",
            "clientId": "${var.google_client_id}",
            "clientSecret": "${var.google_client_secret}"
          }'
      else
        echo "Google identity provider already exists (ID: $EXISTING_IDP), updating..."
        curl -X PATCH \
          "https://graph.microsoft.com/beta/identity/identityProviders/$EXISTING_IDP" \
          -H "Authorization: Bearer $TOKEN" \
          -H "Content-Type: application/json" \
          -d '{
            "clientId": "${var.google_client_id}",
            "clientSecret": "${var.google_client_secret}"
          }'
      fi
    EOT

    environment = {
      ARM_CLIENT_ID     = var.tenant_id  # Placeholder - will be overridden by shell environment
      ARM_CLIENT_SECRET = ""              # Placeholder - will be overridden by shell environment
    }
  }

  depends_on = [
    azuread_application.api,
    azuread_application.user_portal,
    azuread_application.admin_portal
  ]
}
