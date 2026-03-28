# User Onboarding Flow

## Multi-Tenancy Model

- **Tenant** = Gymnastics Club (organization) OR Individual User
- **Tenant ID Type**: UUID/GUID
- Multiple users can belong to the same club (shared data)
- Individual users get their own isolated tenant

## New User Journey

### 1. Sign Up / Sign In
User clicks "Sign in with Google" → Keycloak authenticates → User account created

**Auto-assigned:**
- `tenant_id`: `00000000-0000-0000-0000-000000000001` (onboarding tenant GUID)
- `role`: `user`

### 2. Onboarding Screen (First Login)
User sees: "Welcome! Let's set up your account"

**Options:**
```
┌─────────────────────────────────────────┐
│  How do you want to use the platform?  │
├─────────────────────────────────────────┤
│                                         │
│  🏢 Join/Create a Gymnastics Club      │
│     → Share data with your team         │
│     → Coaches, athletes, schedules      │
│                                         │
│  👤 Individual/Personal Use             │
│     → Private data, just for you        │
│     → Personal training logs            │
│                                         │
└─────────────────────────────────────────┘
```

### 3A. Club Path
**Create New Club:**
1. User enters club name: "Elite Gymnastics Academy"
2. System generates new tenant GUID (e.g., `a1b2c3d4-e5f6-7890-abcd-ef1234567890`)
3. User becomes `organization_owner`
4. User can invite others (they get same tenant_id)

**Join Existing Club:**
1. User enters invite code: `CLUB-XYZ123`
2. System assigns them to club's tenant_id (GUID)
3. User gets `user` role (or coach/admin based on invite)

### 3B. Individual Path
1. System generates unique tenant GUID for this user
2. User keeps `user` role
3. All data is private to them

### 4. Database Schema Changes Needed

```sql
-- Clubs/Organizations table
CREATE TABLE clubs (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    tenant_id UUID UNIQUE NOT NULL, -- GUID tenant identifier
    created_at TIMESTAMPTZ DEFAULT NOW(),
    owner_user_id VARCHAR(255) NOT NULL
);

-- Invite codes table
CREATE TABLE club_invites (
    id UUID PRIMARY KEY,
    club_id UUID REFERENCES clubs(id),
    invite_code VARCHAR(50) UNIQUE NOT NULL,
    role VARCHAR(50) NOT NULL, -- 'user', 'coach', 'admin'
    created_by VARCHAR(255) NOT NULL,
    expires_at TIMESTAMPTZ,
    max_uses INT,
    used_count INT DEFAULT 0
);

-- User onboarding status
ALTER TABLE users ADD COLUMN onboarding_completed BOOLEAN DEFAULT FALSE;
ALTER TABLE users ADD COLUMN onboarding_choice VARCHAR(50); -- 'club' or 'individual'
```

**Note**: The `tenant_id` column is now a UUID/GUID type, not a string.

## API Endpoints Needed

### Check Onboarding Status
```http
GET /api/onboarding/status
Response: {
  "completed": false,
  "tenant_id": "00000000-0000-0000-0000-000000000001"
}
```

### Complete Onboarding - Create Club
```http
POST /api/onboarding/create-club
Body: { "clubName": "Elite Gymnastics" }
Response: {
  "tenant_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "role": "organization_owner"
}
```

### Complete Onboarding - Individual Mode
```http
POST /api/onboarding/individual
Response: {
  "tenant_id": "f950d943-8e05-49d0-b3ce-a92aa64e2ea4",
  "role": "user"
}
```

### Complete Onboarding - Join Club
```http
POST /api/onboarding/join-club
Body: { "inviteCode": "CLUB-XYZ123" }
Response: {
  "tenant_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "clubName": "Elite Gymnastics",
  "role": "user"
}
```

## Technical Implementation Notes

1. **Frontend Route Guard**: Check if `tenant_id === "00000000-0000-0000-0000-000000000001"` → redirect to onboarding
2. **Update Keycloak**: After onboarding, update the hardcoded tenant_id mapper to the new tenant GUID
3. **Force Re-login**: After onboarding, user must log out/in to get new token with updated tenant_id
4. **Validation**: Prevent access to main app features while in onboarding tenant

## Example Frontend Code

```typescript
// In App.tsx or route guard
const { keycloak } = useKeycloak();
const tenantId = keycloak.tokenParsed?.tenant_id;

const ONBOARDING_TENANT_ID = '00000000-0000-0000-0000-000000000001';

useEffect(() => {
  if (tenantId === ONBOARDING_TENANT_ID) {
    // Redirect to onboarding screen
    navigate('/onboarding');
  }
}, [tenantId]);
```

## Keycloak Tenant ID Management

The tenant_id is currently set via a hardcoded claim mapper in Keycloak. After onboarding:
1. Either update the user's attributes in Keycloak (for federated users, this may not persist)
2. Or implement a custom claim mapper that reads from the clubs table
3. Or use a different approach like storing tenant assignments in the API database and setting it via a custom endpoint
