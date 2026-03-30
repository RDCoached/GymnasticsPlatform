import { test, expect } from '@playwright/test';
import { RegisterPage } from '../pages/RegisterPage';
import { SignInPage } from '../pages/SignInPage';
import { OnboardingPage } from '../pages/OnboardingPage';
import { DashboardPage } from '../pages/DashboardPage';
import { generateUniqueEmail, generateClubName } from '../helpers/test-data';

test.describe('Navigation and Guards', () => {
  test('should redirect unauthenticated users to sign-in', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/sign-in/);

    await page.goto('/profile');
    await expect(page).toHaveURL(/\/sign-in/);

    await page.goto('/club/invites');
    await expect(page).toHaveURL(/\/sign-in/);
  });

  test('should redirect authenticated users without onboarding to onboarding page', async ({ page }) => {
    const email = generateUniqueEmail('navguard');
    const password = 'TestPassword123!';

    // Register and login
    const registerPage = new RegisterPage(page);
    await registerPage.goto();
    await registerPage.register(email, password, 'Nav Test User');

    const signInPage = new SignInPage(page);
    await signInPage.goto();
    await signInPage.signIn(email, password);

    // Should be on onboarding
    await expect(page).toHaveURL(/\/onboarding/);

    // Try to access protected routes
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/onboarding/);

    await page.goto('/profile');
    await expect(page).toHaveURL(/\/onboarding/);

    await page.goto('/club/invites');
    await expect(page).toHaveURL(/\/onboarding/);
  });

  test('should allow access to protected routes after onboarding', async ({ page }) => {
    const email = generateUniqueEmail('navaccess');
    const password = 'TestPassword123!';

    // Register, login, and complete onboarding
    const registerPage = new RegisterPage(page);
    await registerPage.goto();
    await registerPage.register(email, password, 'Access Test User');

    const signInPage = new SignInPage(page);
    await signInPage.goto();
    await signInPage.signIn(email, password);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.selectIndividualMode();

    await expect(page).toHaveURL(/\/dashboard/);

    // Navigate to profile
    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();
    await expect(page).toHaveURL(/\/profile/);

    // Navigate back to dashboard
    await page.goto('/dashboard');
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('should redirect root path to dashboard for onboarded users', async ({ page }) => {
    const email = generateUniqueEmail('navroot');
    const password = 'TestPassword123!';

    await completeFullOnboarding(page, email, password);

    await page.goto('/');
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('should handle browser back button correctly', async ({ page }) => {
    const email = generateUniqueEmail('navback');
    const password = 'TestPassword123!';

    await completeFullOnboarding(page, email, password);

    // Navigate to profile
    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();
    await expect(page).toHaveURL(/\/profile/);

    // Use browser back button
    await page.goBack();
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('should persist authentication across page reloads', async ({ page }) => {
    const email = generateUniqueEmail('navreload');
    const password = 'TestPassword123!';

    await completeFullOnboarding(page, email, password);

    await expect(page).toHaveURL(/\/dashboard/);

    // Reload page
    await page.reload();

    // Should still be authenticated
    await expect(page).toHaveURL(/\/dashboard/);
    const dashboardPage = new DashboardPage(page);
    await expect(dashboardPage.heading).toBeVisible();
  });

  test('should handle direct URL access for authenticated users', async ({ page }) => {
    const email = generateUniqueEmail('navdirect');
    const password = 'TestPassword123!';

    await completeFullOnboarding(page, email, password);

    // Access dashboard directly via URL
    await page.goto('http://localhost:5173/dashboard');
    await expect(page).toHaveURL(/\/dashboard/);

    // Access profile directly via URL
    await page.goto('http://localhost:5173/profile');
    await expect(page).toHaveURL(/\/profile/);
  });

  test('should prevent access to onboarding after completion', async ({ page }) => {
    const email = generateUniqueEmail('navnoreturn');
    const password = 'TestPassword123!';

    await completeFullOnboarding(page, email, password);

    // Try to access onboarding page
    await page.goto('/onboarding');

    // Should redirect to dashboard
    await expect(page).toHaveURL(/\/dashboard/);
  });
});

async function completeFullOnboarding(page: any, email: string, password: string): Promise<void> {
  const registerPage = new RegisterPage(page);
  await registerPage.goto();
  await registerPage.register(email, password, 'Test User');

  const signInPage = new SignInPage(page);
  await signInPage.goto();
  await signInPage.signIn(email, password);

  const onboardingPage = new OnboardingPage(page);
  await onboardingPage.selectIndividualMode();

  await expect(page).toHaveURL(/\/dashboard/);
}
