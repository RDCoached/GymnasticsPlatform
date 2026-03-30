import { test, expect } from '@playwright/test';
import { RegisterPage } from '../pages/RegisterPage';
import { SignInPage } from '../pages/SignInPage';
import { OnboardingPage } from '../pages/OnboardingPage';
import { DashboardPage } from '../pages/DashboardPage';
import { generateUniqueEmail } from '../helpers/test-data';

test.describe('Onboarding - Individual Mode Flow', () => {
  test('should complete full individual mode onboarding journey', async ({ page }) => {
    // Register a new user
    const registerPage = new RegisterPage(page);
    await registerPage.goto();

    const email = generateUniqueEmail('individual');
    const password = 'TestPassword123!';
    const fullName = 'Individual User';

    await registerPage.register(email, password, fullName);
    await expect(page).toHaveURL(/\/sign-in/);

    // Login
    const signInPage = new SignInPage(page);
    await signInPage.signIn(email, password);

    // Should redirect to onboarding
    await expect(page).toHaveURL(/\/onboarding/);

    // Complete onboarding by selecting individual mode
    const onboardingPage = new OnboardingPage(page);
    await expect(onboardingPage.heading).toBeVisible();
    await expect(onboardingPage.individualModeButton).toBeVisible();

    await onboardingPage.selectIndividualMode();

    // Should redirect to dashboard after completion
    await expect(page).toHaveURL(/\/dashboard/);

    // Verify dashboard is accessible
    const dashboardPage = new DashboardPage(page);
    await expect(dashboardPage.heading).toBeVisible();
  });

  test.skip('should prevent returning to onboarding after individual mode selection', async ({ page }) => {
    const email = generateUniqueEmail('individualonce');
    const password = 'TestPassword123!';

    await registerAndLogin(page, email, password);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.selectIndividualMode();

    await expect(page).toHaveURL(/\/dashboard/);

    // Try to navigate back to onboarding
    await page.goto('/onboarding');

    // Should redirect to dashboard
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test.skip('should maintain session after individual mode selection', async ({ page }) => {
    const email = generateUniqueEmail('individualsession');
    const password = 'TestPassword123!';

    await registerAndLogin(page, email, password);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.selectIndividualMode();

    await expect(page).toHaveURL(/\/dashboard/);

    // Reload page
    await page.reload();

    // Should still be authenticated and on dashboard
    await expect(page).toHaveURL(/\/dashboard/);
    const dashboardPage = new DashboardPage(page);
    await expect(dashboardPage.heading).toBeVisible();
  });

  test.skip('should display all three onboarding options', async ({ page }) => {
    const email = generateUniqueEmail('alloptions');
    const password = 'TestPassword123!';

    await registerAndLogin(page, email, password);

    const onboardingPage = new OnboardingPage(page);

    // All three options should be visible
    await expect(onboardingPage.createClubButton).toBeVisible();
    await expect(onboardingPage.joinClubButton).toBeVisible();
    await expect(onboardingPage.individualModeButton).toBeVisible();
  });

  test.skip('should handle individual mode selection errors gracefully', async ({ page, context }) => {
    const email = generateUniqueEmail('individualerror');
    const password = 'TestPassword123!';

    await registerAndLogin(page, email, password);

    // Intercept API call and simulate failure
    await page.route('**/api/onboarding/individual', route => {
      route.fulfill({
        status: 500,
        body: JSON.stringify({ error: 'Internal server error' }),
      });
    });

    const onboardingPage = new OnboardingPage(page);

    // Listen for dialog
    page.on('dialog', async dialog => {
      expect(dialog.message()).toContain('Failed');
      await dialog.accept();
    });

    await onboardingPage.selectIndividualMode();

    // Should remain on onboarding page
    await expect(page).toHaveURL(/\/onboarding/);
  });
});

async function registerAndLogin(page: any, email: string, password: string): Promise<void> {
  const registerPage = new RegisterPage(page);
  await registerPage.goto();

  const fullName = 'Test User';
  await registerPage.register(email, password, fullName);
  await expect(page).toHaveURL(/\/sign-in/);

  const signInPage = new SignInPage(page);
  await signInPage.signIn(email, password);

  await expect(page).toHaveURL(/\/onboarding/);
}
