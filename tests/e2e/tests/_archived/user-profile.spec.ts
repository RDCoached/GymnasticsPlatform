import { test, expect } from '@playwright/test';
import { RegisterPage } from '../pages/RegisterPage';
import { SignInPage } from '../pages/SignInPage';
import { OnboardingPage } from '../pages/OnboardingPage';
import { DashboardPage } from '../pages/DashboardPage';
import { ProfilePage } from '../pages/ProfilePage';
import { generateUniqueEmail } from '../helpers/test-data';

test.describe('User Profile Management', () => {
  test.skip('should display user profile information', async ({ page }) => {
    const email = generateUniqueEmail('profile');
    const password = 'TestPassword123!';
    const fullName = 'Profile Test User';

    await completeOnboarding(page, email, password, fullName);

    // Navigate to profile page
    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    await expect(page).toHaveURL(/\/profile/);

    // Should display user information
    const profilePage = new ProfilePage(page);
    await expect(profilePage.nameInput).toHaveValue(fullName);
    await expect(profilePage.emailInput).toHaveValue(email);
  });

  test.skip('should allow updating profile information', async ({ page }) => {
    const email = generateUniqueEmail('profileupdate');
    const password = 'TestPassword123!';

    await completeOnboarding(page, email, password, 'Original Name');

    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    const profilePage = new ProfilePage(page);
    await profilePage.updateProfile('Updated Name');

    // Should show success message
    await expect(profilePage.successMessage).toBeVisible();
  });

  test.skip('should validate profile update form', async ({ page }) => {
    const email = generateUniqueEmail('profilevalidate');
    const password = 'TestPassword123!';

    await completeOnboarding(page, email, password, 'Validate User');

    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    const profilePage = new ProfilePage(page);

    // Clear required field
    await profilePage.nameInput.clear();

    // Button should be disabled when field is empty
    await expect(profilePage.updateButton).toBeDisabled();
  });

  test.skip('should handle profile update errors gracefully', async ({ page }) => {
    const email = generateUniqueEmail('profileerror');
    const password = 'TestPassword123!';

    await completeOnboarding(page, email, password, 'Error Test');

    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    // Mock error response
    await page.route('**/api/profile', route => {
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ detail: 'Internal server error' }),
      });
    });

    const profilePage = new ProfilePage(page);
    await profilePage.nameInput.fill('New Name');
    await profilePage.updateButton.click();

    // Should show error message
    await expect(profilePage.errorMessage).toBeVisible();
  });

  test.skip('should navigate back to dashboard from profile', async ({ page }) => {
    const email = generateUniqueEmail('profilenav');
    const password = 'TestPassword123!';

    await completeOnboarding(page, email, password, 'Nav Test');

    const dashboardPage = new DashboardPage(page);
    await dashboardPage.navigateToProfile();

    await expect(page).toHaveURL(/\/profile/);

    const profilePage = new ProfilePage(page);
    await profilePage.goBackToDashboard();

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
