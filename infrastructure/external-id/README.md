# Microsoft Entra External ID (CIAM) Setup

This directory contains automated scripts to provision Microsoft Entra External ID (CIAM) for customer authentication with social identity providers.

## Prerequisites

1. **Azure Subscription** with Owner or Contributor role
2. **Service Principal** with permissions (created by `setup-terraform-sp.sh`)
3. **Google OAuth Credentials** from [Google Cloud Console](https://console.cloud.google.com/)
4. **terraform.tfvars** with Google credentials filled in

## Setup Process

### Step 1: Create Service Principal (if not done)

```bash
./setup-terraform-sp.sh
```

This creates a service principal with required permissions and saves credentials to `terraform.env`.

### Step 2: Configure Google OAuth

1. Copy `terraform.tfvars.example` to `terraform.tfvars`
2. Fill in your Google OAuth credentials

### Step 3: Create External ID Tenant

```bash
./setup-external-id.sh
```

This script:
- Creates a new External ID (CIAM) tenant via Azure REST API
- Registers applications via Microsoft Graph API
- Configures Google identity provider
- Generates `external-id-config.env`

### Step 4: Configure Applications

```bash
./configure-apps.sh
```

Updates backend user secrets and frontend .env.local files.

### Step 5: Restart Applications

```bash
./dev.sh api           # Restart API
./dev.sh user-portal   # Restart user portal
./dev.sh admin-portal  # Restart admin portal
```

## What Gets Created

- External ID (CIAM) tenant with domain `{tenant-name}.ciamlogin.com`
- API application with OAuth2 scopes
- User Portal SPA application
- Admin Portal SPA application
- Google identity provider configuration

## Sign-in Options

- Google account (any @gmail.com)
- Microsoft personal account (@live.com, @outlook.com, @hotmail.com)
- Email/password (local accounts)
