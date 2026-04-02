# Page Object Standards

## Core Principle: Wait for Async Operations

**MANDATORY:** Every page object method that triggers navigation or API calls MUST wait for completion before returning.

### Why This Matters

Without explicit waits, tests create race conditions:
- Test tries to interact with elements before page loads
- Test checks credentials before user is created in backend
- Test expects URL change before navigation completes

These race conditions cause intermittent test failures that are difficult to debug.

## Pattern 1: Wait for Navigation

Any method that causes navigation MUST wait for the new URL:

```typescript
async register(email: string, password: string, fullName: string): Promise<void> {
  await this.emailInput.fill(email);
  await this.passwordInput.fill(password);
  await this.confirmPasswordInput.fill(password);
  await this.fullNameInput.fill(fullName);
  await this.registerButton.click();

  // ✅ REQUIRED: Wait for navigation to complete
  await Promise.race([
    this.page.waitForURL(/\/sign-in/, { timeout: 10000 }),
    this.errorMessage.waitFor({ state: 'visible', timeout: 10000 })
  ]);

  // ✅ REQUIRED: Verify no error occurred
  const errorVisible = await this.errorMessage.isVisible().catch(() => false);
  if (errorVisible) {
    const errorText = await this.errorMessage.textContent();
    throw new Error(`Registration failed: ${errorText}`);
  }
}
```

### Examples

```typescript
// ❌ BAD: No wait - race condition
async login(email: string, password: string): Promise<void> {
  await this.emailInput.fill(email);
  await this.passwordInput.fill(password);
  await this.submitButton.click();
  // Test continues immediately - dashboard might not be loaded!
}

// ✅ GOOD: Explicit wait for navigation
async login(email: string, password: string): Promise<void> {
  await this.emailInput.fill(email);
  await this.passwordInput.fill(password);
  await this.submitButton.click();
  await this.page.waitForURL(/\/dashboard/, { timeout: 10000 });
}
```

## Pattern 2: Wait for Element State Changes

When clicking a button that shows/hides elements:

```typescript
async openModal(): Promise<void> {
  await this.openButton.click();
  // ✅ REQUIRED: Wait for modal to be visible
  await this.modal.waitFor({ state: 'visible', timeout: 5000 });
}

async closeModal(): Promise<void> {
  await this.closeButton.click();
  // ✅ REQUIRED: Wait for modal to be hidden
  await this.modal.waitFor({ state: 'hidden', timeout: 5000 });
}
```

## Pattern 3: Wait for API Responses

When an action triggers an API call that affects UI:

```typescript
async deleteItem(itemId: string): Promise<void> {
  await this.deleteButton.click();
  await this.confirmButton.click();

  // ✅ REQUIRED: Wait for success message or list update
  await Promise.race([
    this.successMessage.waitFor({ state: 'visible', timeout: 10000 }),
    this.page.waitForResponse(resp => resp.url().includes(`/items/${itemId}`) && resp.status() === 204)
  ]);
}
```

## Pattern 4: Selector Accuracy

Selectors must match actual DOM elements. When tests fail with "element not found":

1. **Check the error screenshot** - See what's actually on the page
2. **Verify role and name** - Use Playwright Inspector to find correct selector
3. **Update selector** - Match what the component actually renders

```typescript
// ❌ BAD: Selector doesn't match actual element
this.profileLink = page.getByRole('link', { name: /profile/i });
// Actual DOM: <button>Update Profile</button>

// ✅ GOOD: Selector matches actual element
this.profileLink = page.getByRole('button', { name: /update profile/i });
```

## Testing Your Page Object

Before marking a page object complete, verify:

1. **Run the test 10 times** - Race conditions are intermittent
   ```bash
   npx playwright test path/to/test.spec.ts --repeat-each=10
   ```

2. **Check for explicit waits** - Every navigation/API action should have a corresponding wait

3. **Verify selectors** - Open Playwright Inspector and confirm selectors match DOM

4. **Review error screenshots** - Failed tests often reveal incorrect selectors

## Common Mistakes

### ❌ Assuming Playwright auto-waits for everything
```typescript
async submit(): Promise<void> {
  await this.submitButton.click();
  // Playwright waits for click, but NOT for navigation!
}
```

### ❌ Using arbitrary timeouts instead of waiting for conditions
```typescript
async submit(): Promise<void> {
  await this.submitButton.click();
  await this.page.waitForTimeout(2000); // BAD: Magic number
}
```

### ❌ Not handling both success and error cases
```typescript
async register(...): Promise<void> {
  await this.registerButton.click();
  await this.page.waitForURL(/\/sign-in/); // What if registration fails?
}
```

## Checklist for New Page Objects

- [ ] Every method that navigates has `waitForURL()`
- [ ] Every method that shows/hides elements has `waitFor({ state })`
- [ ] Every async action has error handling
- [ ] All selectors verified against actual DOM
- [ ] Test runs successfully 10 times in a row
- [ ] No arbitrary timeouts (use explicit waits instead)

## Port Configuration

**DO NOT CHANGE** these ports without updating all configuration files:

- **Backend API**: 5001 (src/GymnasticsPlatform.Api/Properties/launchSettings.json)
- **Frontend (User Portal)**: 3001 (frontend/user-portal/vite.config.ts)
- **Frontend (Admin Portal)**: 3002 (frontend/admin-portal/vite.config.ts)
- **Keycloak**: 8080 (docker-compose.yml)
- **PostgreSQL**: 5432 (docker-compose.yml)

Configuration files that reference ports:
- `tests/e2e/playwright.config.ts` - baseURL and webServer health checks
- `frontend/user-portal/.env` - VITE_API_BASE_URL
- `src/GymnasticsPlatform.Api/appsettings.json` - Keycloak BaseUrl

## Resources

- [Playwright Auto-waiting](https://playwright.dev/docs/actionability) - What Playwright waits for automatically
- [Playwright Assertions](https://playwright.dev/docs/test-assertions) - Built-in waits in expect()
- [Page Object Model Best Practices](https://playwright.dev/docs/pom) - Official Playwright guide
