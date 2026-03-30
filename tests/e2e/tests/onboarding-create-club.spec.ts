import { test, expect } from '@playwright/test';
import { RegisterPage } from '../pages/RegisterPage';
import { SignInPage } from '../pages/SignInPage';
import { OnboardingPage } from '../pages/OnboardingPage';
import { DashboardPage } from '../pages/DashboardPage';
import { generateUniqueEmail, generateClubName } from '../helpers/test-data';

test.describe('Onboarding - Create Club Flow', () => {
  test('should complete full create club onboarding journey', async ({ page }) => {
    // Register a new user
    const registerPage = new RegisterPage(page);
    await registerPage.goto();

    const email = generateUniqueEmail('clubowner');
    const password = 'TestPassword123!';
    const fullName = 'Club Owner';
    const clubName = generateClubName();

    await registerPage.register(email, password, fullName);
    await expect(page).toHaveURL(/\/sign-in/);

    // Login
    const signInPage = new SignInPage(page);
    await signInPage.signIn(email, password);

    // Should redirect to onboarding
    await expect(page).toHaveURL(/\/onboarding/);

    // Complete onboarding by creating a club
    const onboardingPage = new OnboardingPage(page);
    await expect(onboardingPage.heading).toBeVisible();
    await expect(onboardingPage.createClubButton).toBeVisible();

    await onboardingPage.createClub(clubName);

    // Should redirect to dashboard after completion
    await expect(page).toHaveURL(/\/dashboard/);

    // Verify dashboard is accessible
    const dashboardPage = new DashboardPage(page);
    await expect(dashboardPage.heading).toBeVisible();
  });

  test('should show create club form when option selected', async ({ page }) => {
    // Setup: Register and login
    const email = generateUniqueEmail('clubui');
    await registerAndLogin(page, email);

    const onboardingPage = new OnboardingPage(page);
    await expect(onboardingPage.heading).toBeVisible();

    // Select create club option
    await onboardingPage.selectCreateClub();

    // Verify form is displayed
    await expect(onboardingPage.clubNameInput).toBeVisible();
    await expect(onboardingPage.createClubSubmitButton).toBeVisible();
    await expect(onboardingPage.backButton).toBeVisible();
  });

  test('should allow navigation back from create club form', async ({ page }) => {
    const email = generateUniqueEmail('clubback');
    await registerAndLogin(page, email);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.selectCreateClub();

    await expect(onboardingPage.clubNameInput).toBeVisible();

    // Go back
    await onboardingPage.goBack();

    // Should show options again
    await expect(onboardingPage.createClubButton).toBeVisible();
    await expect(onboardingPage.clubNameInput).not.toBeVisible();
  });

  test('should validate club name is required', async ({ page }) => {
    const email = generateUniqueEmail('clubvalidate');
    await registerAndLogin(page, email);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.selectCreateClub();

    // Try to submit without club name
    await onboardingPage.createClubSubmitButton.click();

    // Should show validation error
    const errorMessage = page.getByText(/required/i);
    await expect(errorMessage).toBeVisible();

    // Should not navigate away
    await expect(page).toHaveURL(/\/onboarding/);
  });

  test('should prevent duplicate club creation', async ({ page }) => {
    const email = generateUniqueEmail('clubdupe');
    const clubName = generateClubName();
    await registerAndLogin(page, email);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.createClub(clubName);

    await expect(page).toHaveURL(/\/dashboard/);

    // Try to create another club by navigating back to onboarding
    await page.goto('/onboarding');

    // Should redirect to dashboard (onboarding already complete)
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('should persist user session after club creation', async ({ page }) => {
    const email = generateUniqueEmail('clubsession');
    const clubName = generateClubName();
    await registerAndLogin(page, email);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.createClub(clubName);

    await expect(page).toHaveURL(/\/dashboard/);

    // Reload page
    await page.reload();

    // Should still be on dashboard
    await expect(page).toHaveURL(/\/dashboard/);
    const dashboardPage = new DashboardPage(page);
    await expect(dashboardPage.heading).toBeVisible();
  });
});

async function registerAndLogin(page: any, email: string): Promise<void> {
  const registerPage = new RegisterPage(page);
  await registerPage.goto();

  const password = 'TestPassword123!';
  const fullName = 'Test User';

  await registerPage.register(email, password, fullName);
  await expect(page).toHaveURL(/\/sign-in/);

  const signInPage = new SignInPage(page);
  await signInPage.signIn(email, password);

  await expect(page).toHaveURL(/\/onboarding/);
}
