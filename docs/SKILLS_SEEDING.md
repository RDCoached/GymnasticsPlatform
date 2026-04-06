# Skills Catalog Seeding

This document explains how to seed the skills catalog with common gymnastics skills.

## Overview

The skills catalog can be populated with 43+ pre-defined gymnastics skills covering all sections:
- **Bars** (5 skills): Pull-ups, Kip, Cast to Handstand, Giant Swing, Clear Hip Circle
- **Floor** (11 skills): Rolls, Cartwheels, Handsprings, Tucks, Leaps, Bridges
- **Beam** (7 skills): Walks, Turns, Handstands, Cartwheels, Walkovers, Leaps, Scales
- **Vault/Range** (5 skills): Run/Hurdle, Handstand Flat Back, Front Handspring, Tsukahara, Yurchenko
- **Strength & Conditioning** (15 skills): Core work, Pull-ups, Holds, Flexibility, Power training

Many skills span multiple sections (e.g., Handstand applies to Floor, Bars, and Strength & Conditioning).

## Seeding Methods

### Option 1: Admin API Endpoint (Recommended)

Use the admin endpoint to seed skills via HTTP:

```bash
# Requires AdminPolicy authorization (platform_admin role)
POST /api/admin/seed-skills
```

**Using curl:**
```bash
curl -X POST https://your-api-url/api/admin/seed-skills \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN"
```

**Response:**
```json
{
  "message": "Skills catalog seeded successfully",
  "systemTenantId": "00000000-0000-0000-0000-000000000001",
  "systemUserId": "00000000-0000-0000-0000-000000000001"
}
```

### Option 2: Programmatic Seeding

Call the seeder directly from code:

```csharp
// Inject SkillSeeder into your service
public class YourService
{
    private readonly SkillSeeder _seeder;

    public YourService(SkillSeeder seeder)
    {
        _seeder = seeder;
    }

    public async Task SeedAsync()
    {
        var systemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        await _seeder.SeedAsync(systemTenantId, systemUserId);
    }
}
```

## Idempotency

The seeder is **idempotent** - it can be run multiple times safely:
- Uses semantic search to check if skills already exist
- Skips skills with matching titles
- Only creates new skills that don't exist
- Logs seeded vs skipped counts

Running the seeder multiple times will:
1. Check each skill by title using semantic search
2. Skip existing skills
3. Only create missing skills

## Skill Attribution

All seeded skills are attributed to:
- **CreatedByTenantId:** `00000000-0000-0000-0000-000000000001` (system tenant)
- **CreatedByUserId:** `00000000-0000-0000-0000-000000000001` (system user)

This identifies them as system-seeded skills vs user-created skills.

## Skill Details

Each seeded skill includes:
- **Title:** Skill name
- **Description:** Detailed explanation of technique and benefits
- **Effectiveness Rating:** 1-5 scale indicating importance/difficulty
- **Sections:** One or more gymnastics sections the skill applies to
- **Embedding Vector:** Auto-generated 384-dim vector for semantic search

## Customization

To customize the seeded skills:

1. Edit `src/Modules/Training/Training.Infrastructure/Seeders/SkillSeeder.cs`
2. Modify the `GetSkillSeedData()` method
3. Add, remove, or update skills as needed
4. Rebuild and redeploy

## Example Seeded Skills

### Bars
```
Pull-ups (Rating: 5)
- Hang from bar with arms fully extended, pull body up until chin is over the bar
- Sections: Bars, Strength & Conditioning

Kip (Rating: 4)
- Fundamental bars skill using momentum to transition from hanging to support
- Sections: Bars
```

### Floor
```
Handstand (Rating: 5)
- Hold inverted vertical position on hands for 30+ seconds
- Sections: Floor, Bars, Strength & Conditioning

Back Handspring (Rating: 5)
- Jump backward onto hands then snap down to feet
- Sections: Floor
```

### Beam
```
Handstand on Beam (Rating: 5)
- Hold handstand on 4-inch beam surface
- Sections: Beam

Cartwheel on Beam (Rating: 4)
- Perform cartwheel along beam length maintaining straight line
- Sections: Beam
```

### Strength & Conditioning
```
Hollow Body Hold (Rating: 5)
- Lie on back, press lower back to floor, raise shoulders and legs slightly
- Sections: Strength & Conditioning

L-Sit Hold (Rating: 5)
- Hold body in L-shape with legs parallel to floor while supporting on hands
- Sections: Strength & Conditioning, Bars
```

## Verification

After seeding, verify via API:

```bash
# List all skills
GET /api/skills?pageSize=50

# Search for specific skills
POST /api/skills/search
{
  "query": "handstand",
  "maxResults": 10
}

# Filter by section
GET /api/skills?section=Floor&pageSize=20
```

## Notes

- Seeding requires the database to be migrated with the `AddSkillsCatalog` migration
- The Ollama embedding service must be running to generate embeddings
- Skills can be edited or deleted after seeding via the standard API endpoints
- Seeded skills have `UsageCount: 0` initially
- Usage counts increment automatically when skills are used in programmes
