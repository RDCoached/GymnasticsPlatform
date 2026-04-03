# E2E Tests - Gymnastics Platform

Comprehensive end-to-end tests for the Gymnastics Platform using Playwright.

## Quick Start

### Option 1: Container-Based Testing (Recommended)

Run tests in a containerized environment that exactly mirrors CI:

```bash
# From project root
./scripts/run-e2e-local.sh
```

This is the recommended approach as it eliminates environment-specific issues. See [CONTAINER_TESTING.md](./CONTAINER_TESTING.md) for details.

### Option 2: Local Development Testing

Run tests against locally running services:

1. Ensure backend is running: `dotnet run --project src/GymnasticsPlatform.Api`
2. Ensure frontend is running: `cd frontend/user-portal && npm run dev`
3. Run tests: `cd tests/e2e && npm test`

## Setup

```bash
cd tests/e2e
npm install
npx playwright install
```

## Running Tests

```bash
# Run all tests
npm test

# Run tests in headed mode (see browser)
npm run test:headed

# Run tests in UI mode (interactive)
npm run test:ui

# Run tests in debug mode
npm run test:debug

# Run specific test file
npx playwright test tests/auth.spec.ts

# Run tests in specific browser
npx playwright test --project=chromium
npx playwright test --project=firefox
npx playwright test --project=webkit
```

## Test Coverage

### Authentication Flow (`auth.spec.ts`)
- User registration with validation
- User login/logout
- Invalid credentials handling
- Session persistence
- Authentication guards

### Onboarding - Create Club (`onboarding-create-club.spec.ts`)
- Complete create club journey
- Form validation
- Navigation between steps
- Preventing duplicate onboarding
- Session persistence after club creation

### Onboarding - Individual Mode (`onboarding-individual.spec.ts`)
- Complete individual mode journey
- Preventing duplicate onboarding
- Session persistence
- Error handling

### Onboarding - Join Club (`onboarding-join-club.spec.ts`)
- Join club form UI
- Invite code validation
- Invalid/expired invite handling
- Form navigation

### Navigation & Guards (`navigation.spec.ts`)
- Route protection for unauthenticated users
- Onboarding guards
- Browser back/forward navigation
- Direct URL access
- Page reload persistence

### User Profile (`user-profile.spec.ts`)
- Viewing profile information
- Updating profile
- Form validation
- Error handling

## Prerequisites

Before running E2E tests, ensure:
1. Frontend is running on `http://localhost:3001` (user portal) or `http://localhost:3002` (admin portal)
2. Backend API is running on `http://localhost:5001`

The Playwright config will automatically start these if not running.

## Project Structure

```
tests/e2e/
├── tests/               # Test suites
├── pages/               # Page Object Models
├── helpers/             # Utility functions
├── playwright.config.ts # Playwright configuration
├── package.json
└── README.md
```

## Debugging

```bash
# Run with Playwright Inspector
npm run test:debug

# View test report after run
npm run test:report

# Run with trace viewer
npx playwright test --trace on
npx playwright show-trace trace.zip
```

## CI/CD

For CI environments:
```bash
npm run test:ci
```

This will:
- Run tests with 2 retries
- Use single worker for stability
- Generate HTML report
