import { test, expect } from '@playwright/test';
import { RegisterPage } from '../pages/RegisterPage';
import { SignInPage } from '../pages/SignInPage';
import { OnboardingPage } from '../pages/OnboardingPage';
import { DashboardPage } from '../pages/DashboardPage';
import { generateUniqueEmail } from '../helpers/test-data';

test.describe('User Profile Management', () => {
  test('should display user profile information', async ({ page }) => {
    const email = generateUniqueEmail('profile');
    const password = 'TestPassword123!';
    const fullName = 'Profile Test User';

    await completeOnboarding(page, email, password, fullName);

    // Navigate to profile page
    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    await expect(page).toHaveURL(/\/profile/);

    // Should display user information
    await expect(page.getByText(fullName)).toBeVisible();
    await expect(page.getByText(email)).toBeVisible();
  });

  test('should allow updating profile information', async ({ page }) => {
    const email = generateUniqueEmail('profileupdate');
    const password = 'TestPassword123!';

    await completeOnboarding(page, email, password, 'Original Name');

    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    // Find and update name field
    const nameInput = page.getByLabel(/name/i);
    await expect(nameInput).toBeVisible();

    await nameInput.clear();
    await nameInput.fill('Updated Name');

    // Submit form
    const updateButton = page.getByRole('button', { name: /update|save/i });
    await updateButton.click();

    // Should show success message
    const successMessage = page.getByText(/updated|saved successfully/i);
    await expect(successMessage).toBeVisible();
  });

  test('should validate profile update form', async ({ page }) => {
    const email = generateUniqueEmail('profilevalidate');
    const password = 'TestPassword123!';

    await completeOnboarding(page, email, password, 'Validate User');

    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    // Clear required field
    const nameInput = page.getByLabel(/name/i);
    await nameInput.clear();

    // Try to submit
    const updateButton = page.getByRole('button', { name: /update|save/i });
    await updateButton.click();

    // Should show validation error
    const errorMessage = page.getByText(/required/i);
    await expect(errorMessage).toBeVisible();
  });

  test('should handle profile update errors gracefully', async ({ page }) => {
    const email = generateUniqueEmail('profileerror');
    const password = 'TestPassword123!';

    await completeOnboarding(page, email, password, 'Error Test');

    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    // Mock error response
    await page.route('**/api/profile/**', route => {
      route.fulfill({
        status: 500,
        body: JSON.stringify({ error: 'Internal server error' }),
      });
    });

    const nameInput = page.getByLabel(/name/i);
    await nameInput.fill('New Name');

    const updateButton = page.getByRole('button', { name: /update|save/i });
    await updateButton.click();

    // Should show error message
    const errorMessage = page.getByText(/failed|error/i);
    await expect(errorMessage).toBeVisible();
  });

  test('should navigate back to dashboard from profile', async ({ page }) => {
    const email = generateUniqueEmail('profilenav');
    const password = 'TestPassword123!';

    await completeOnboarding(page, email, password, 'Nav Test');

    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    await expect(page).toHaveURL(/\/profile/);

    // Navigate back to dashboard
    const backButton = page.getByRole('link', { name: /dashboard|back/i });
    if (await backButton.isVisible()) {
      await backButton.click();
    } else {
      await page.goto('/dashboard');
    }

    await expect(page).toHaveURL(/\/dashboard/);
  });
});

async function completeOnboarding(
  page: any,
  email: string,
  password: string,
  fullName: string
): Promise<void> {
  const registerPage = new RegisterPage(page);
  await registerPage.goto();
  await registerPage.register(email, password, fullName);

  const signInPage = new SignInPage(page);
  await signInPage.goto();
  await signInPage.signIn(email, password);

  const onboardingPage = new OnboardingPage(page);
  await onboardingPage.selectIndividualMode();

  await expect(page).toHaveURL(/\/dashboard/);
}
