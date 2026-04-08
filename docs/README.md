# Documentation Index

This directory contains comprehensive documentation for the Gymnastics Session Planner platform.

## 🚀 Quick Start Guides

| Document | Purpose | When to Use |
|----------|---------|-------------|
| [KEYCLOAK_SETUP.md](./KEYCLOAK_SETUP.md) | Keycloak configuration with Google OAuth | Setting up local/staging Keycloak instance |
| [ONBOARDING_FLOW.md](./ONBOARDING_FLOW.md) | User onboarding flow (3-choice wizard) | Understanding tenant assignment logic |
| [OBSERVABILITY.md](./OBSERVABILITY.md) | OpenTelemetry setup (LGTM stack) | Configuring logging, metrics, traces |
| [SKILLS_SEEDING.md](./SKILLS_SEEDING.md) | Skills database seeding and RAG setup | Populating initial skills data |

## 🔐 Microsoft Entra ID Migration (Phase 2-7)

The platform is migrating from Keycloak to Microsoft Entra ID (formerly Azure AD) for authentication. These documents cover the migration process:

### Phase 2: Entra ID Setup
| Document | Purpose |
|----------|---------|
| [ENTRA_ID_SETUP.md](./ENTRA_ID_SETUP.md) | Complete Azure Portal setup guide (4 app registrations, extension attributes, Google federation) |
| [ENTRA_ID_CONFIG_TEMPLATE.md](./ENTRA_ID_CONFIG_TEMPLATE.md) | Configuration value tracker with verification checklists |

### Phase 5: Dual-Provider Testing
| Document | Purpose |
|----------|---------|
| [DUAL_PROVIDER_TESTING.md](./DUAL_PROVIDER_TESTING.md) | Comprehensive testing guide for validating both Keycloak and Entra ID providers |
| [DUAL_PROVIDER_TEST_RESULTS.md](./DUAL_PROVIDER_TEST_RESULTS.md) | Test results template with checkboxes for all scenarios |
| [TESTING_CHECKLIST.md](./TESTING_CHECKLIST.md) | Quick reference checklist for testing sessions |

## 🔒 Security & Quality

| Document | Purpose | When to Use |
|----------|---------|-------------|
| [SECURITY-AGENT.md](./SECURITY-AGENT.md) | Security scanning agent instructions | Automated security audits |
| [QA-AGENT.md](./QA-AGENT.md) | QA testing agent instructions | Automated quality assurance |

---

## Document Relationships

```
Migration Overview (in main plan)
    ↓
Phase 2: Setup
├── ENTRA_ID_SETUP.md ← Step-by-step Azure Portal configuration
└── ENTRA_ID_CONFIG_TEMPLATE.md ← Track your configuration values
    ↓
Phase 3-4: Implementation (see PRs)
    ↓
Phase 5: Testing
├── DUAL_PROVIDER_TESTING.md ← Testing procedures and scripts
├── TESTING_CHECKLIST.md ← Quick reference during tests
└── DUAL_PROVIDER_TEST_RESULTS.md ← Fill out as you test
    ↓
Phase 6: Migration (coming soon)
    ↓
Phase 7: Cleanup (coming soon)
```

---

## Usage by Role

### **Developer**
Start with:
1. [KEYCLOAK_SETUP.md](./KEYCLOAK_SETUP.md) - Set up local Keycloak
2. [ENTRA_ID_SETUP.md](./ENTRA_ID_SETUP.md) - Set up dev Entra ID tenant (Phase 2)
3. [DUAL_PROVIDER_TESTING.md](./DUAL_PROVIDER_TESTING.md) - Test both providers (Phase 5)

### **QA Engineer**
Start with:
1. [TESTING_CHECKLIST.md](./TESTING_CHECKLIST.md) - Quick test reference
2. [DUAL_PROVIDER_TESTING.md](./DUAL_PROVIDER_TESTING.md) - Full test procedures
3. [DUAL_PROVIDER_TEST_RESULTS.md](./DUAL_PROVIDER_TEST_RESULTS.md) - Document results

### **DevOps Engineer**
Start with:
1. [ENTRA_ID_SETUP.md](./ENTRA_ID_SETUP.md) - Production Entra ID setup
2. [OBSERVABILITY.md](./OBSERVABILITY.md) - Monitoring stack
3. [KEYCLOAK_SETUP.md](./KEYCLOAK_SETUP.md) - Existing Keycloak (for rollback)

### **Product Owner / Business**
Start with:
1. [ONBOARDING_FLOW.md](./ONBOARDING_FLOW.md) - User experience
2. [DUAL_PROVIDER_TEST_RESULTS.md](./DUAL_PROVIDER_TEST_RESULTS.md) - QA sign-off

---

## Contributing to Documentation

When adding new documentation:

1. **Add to this README** under the appropriate section
2. **Use clear headings** with consistent formatting
3. **Include examples** where helpful (code snippets, screenshots)
4. **Cross-reference** related documents
5. **Keep it current** - update when implementation changes

### Document Template

```markdown
# [Document Title]

## Overview
[What this document covers and who it's for]

## Prerequisites
[What you need before starting]

## Step-by-Step Guide
[Numbered steps with code examples]

## Troubleshooting
[Common issues and solutions]

## Next Steps
[What to do after completing this guide]

---

**Last Updated:** [YYYY-MM-DD]
**Maintained By:** [Team/Person]
```

---

## Version History

This documentation evolves with the platform. Major updates:

- **2026-04-08** - Added Phase 5 testing documentation (dual-provider testing)
- **2026-04-07** - Added Phase 2 Entra ID setup documentation
- **2026-04-06** - Initial documentation set (Keycloak, Onboarding, Observability)

---

**Need help?** Check the main [README.md](../README.md) or ask the platform team.
