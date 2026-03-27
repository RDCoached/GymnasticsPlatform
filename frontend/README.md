# Gymnastics Platform - Frontend Applications

This directory contains the React TypeScript frontends for the Gymnastics Platform.

## Applications

### User Portal (Port 3001)
- **URL**: http://localhost:3001
- **Keycloak Client**: `user-portal`
- **Purpose**: Customer-facing portal for gymnastics organizations and users
- **Test Users**:
  - `user@tenanta.com` / `Test123!` (Tenant A user)
  - `owner@tenantb.com` / `Test123!` (Tenant B org owner)

### Admin Portal (Port 3002)
- **URL**: http://localhost:3002
- **Keycloak Client**: `admin-portal`
- **Purpose**: Platform administration portal
- **Test Users**:
  - `admin@platform.com` / `Test123!` (Platform admin)

## Tech Stack

- **Framework**: React 18 with TypeScript
- **Build Tool**: Vite
- **Authentication**: Keycloak via `@react-keycloak/web`
- **Styling**: CSS (can be upgraded to Tailwind/MUI later)

## Development

### Local Development (Without Docker)

```bash
# User Portal
cd user-portal
npm install
npm run dev

# Admin Portal
cd admin-portal
npm install
npm run dev
```

### Docker Compose (Recommended)

```bash
# From project root
docker-compose up user-portal admin-portal
```

The frontends will automatically connect to Keycloak at http://localhost:8080

## Features

### Current Features
- ✅ Keycloak authentication with PKCE
- ✅ Automatic token management and refresh
- ✅ Display user info and tenant ID from JWT claims
- ✅ Login/logout flows
- ✅ JWT token display for API integration

### Coming Soon
- API integration with .NET backend
- Role-based UI components
- Tenant-specific data views
- Dashboard and analytics

## Authentication Flow

1. User visits portal
2. Clicks "Login" button
3. Redirects to Keycloak login page
4. After successful login, redirects back with authorization code
5. React app exchanges code for tokens (handled by @react-keycloak/web)
6. JWT token includes `tenant_id` claim for multi-tenancy
7. Token is automatically included in API requests

## Testing Multi-Tenancy

1. Open http://localhost:3001 (user-portal)
2. Login with `user@tenanta.com` / `Test123!`
3. Note the Tenant ID: `a1111111-1111-1111-1111-111111111111`
4. Logout
5. Login with `owner@tenantb.com` / `Test123!`
6. Note the different Tenant ID: `b2222222-2222-2222-2222-222222222222`

Each user's JWT token contains their tenant ID, which the API will use to filter data.

## Project Structure

```
user-portal/
├── src/
│   ├── App.tsx           # Main app component with auth logic
│   ├── App.css           # Styles
│   ├── main.tsx          # Entry point with Keycloak provider
│   ├── keycloak.ts       # Keycloak configuration
│   └── ...
├── vite.config.ts        # Vite config (port 3001)
└── package.json

admin-portal/
├── src/
│   ├── App.tsx           # Main app component with auth logic
│   ├── App.css           # Styles
│   ├── main.tsx          # Entry point with Keycloak provider
│   ├── keycloak.ts       # Keycloak configuration
│   └── ...
├── vite.config.ts        # Vite config (port 3002)
└── package.json
```

## Environment Variables

Currently configured for local development:
- Keycloak URL: `http://localhost:8080`
- Keycloak Realm: `gymnastics`

For production, these should be configured via environment variables.

## Adding API Integration

To call the .NET API with authentication:

```typescript
const { keycloak } = useKeycloak();

const response = await fetch('http://localhost:5000/api/endpoint', {
  headers: {
    'Authorization': `Bearer ${keycloak.token}`,
    'Content-Type': 'application/json',
  },
});
```

The API will validate the JWT token and extract the `tenant_id` claim for multi-tenancy.
