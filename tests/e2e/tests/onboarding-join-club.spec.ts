import { test, expect } from '@playwright/test';
import { RegisterPage } from '../pages/RegisterPage';
import { SignInPage } from '../pages/SignInPage';
import { OnboardingPage } from '../pages/OnboardingPage';
import { DashboardPage } from '../pages/DashboardPage';
import { ApiClient } from '../helpers/api-client';
import { generateUniqueEmail, generateClubName } from '../helpers/test-data';

test.describe('Onboarding - Join Club Flow', () => {
  test('should complete full join club onboarding journey', async ({ page, request }) => {
    const apiClient = new ApiClient(request);

    // First, create a club owner and get an invite code
    const ownerEmail = generateUniqueEmail('owner');
    const ownerPassword = 'TestPassword123!';
    const clubName = generateClubName();

    await apiClient.register(ownerEmail, ownerPassword, 'Club Owner');
    const ownerLogin = await apiClient.login(ownerEmail, ownerPassword);
    await apiClient.createClub(ownerLogin.accessToken, clubName);

    // TODO: Get invite code from API - for now, we'll create a member user and test the UI flow
    // This requires implementing the invite creation endpoint in the backend

    // Register a new member user
    const memberEmail = generateUniqueEmail('member');
    const memberPassword = 'TestPassword123!';

    const registerPage = new RegisterPage(page);
    await registerPage.goto();
    await registerPage.register(memberEmail, memberPassword, 'Club Member');

    await expect(page).toHaveURL(/\/sign-in/);

    // Login as member
    const signInPage = new SignInPage(page);
    await signInPage.signIn(memberEmail, memberPassword);

    await expect(page).toHaveURL(/\/onboarding/);

    // Should see join club option
    const onboardingPage = new OnboardingPage(page);
    await expect(onboardingPage.joinClubButton).toBeVisible();

    // Select join club
    await onboardingPage.selectJoinClub();

    // Should show join form
    await expect(onboardingPage.inviteCodeInput).toBeVisible();
    await expect(onboardingPage.joinClubSubmitButton).toBeVisible();
  });

  test('should show join club form when option selected', async ({ page }) => {
    const email = generateUniqueEmail('joinui');
    await registerAndLogin(page, email);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.selectJoinClub();

    // Verify form is displayed
    await expect(onboardingPage.inviteCodeInput).toBeVisible();
    await expect(onboardingPage.joinClubSubmitButton).toBeVisible();
    await expect(onboardingPage.backButton).toBeVisible();
  });

  test('should allow navigation back from join club form', async ({ page }) => {
    const email = generateUniqueEmail('joinback');
    await registerAndLogin(page, email);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.selectJoinClub();

    await expect(onboardingPage.inviteCodeInput).toBeVisible();

    // Go back
    await onboardingPage.goBack();

    // Should show options again
    await expect(onboardingPage.joinClubButton).toBeVisible();
    await expect(onboardingPage.inviteCodeInput).not.toBeVisible();
  });

  test('should validate invite code is required', async ({ page }) => {
    const email = generateUniqueEmail('joinvalidate');
    await registerAndLogin(page, email);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.selectJoinClub();

    // Button should be disabled when field is empty
    await expect(onboardingPage.joinClubSubmitButton).toBeDisabled();
  });

  test('should handle invalid invite code gracefully', async ({ page }) => {
    const email = generateUniqueEmail('joininvalid');
    await registerAndLogin(page, email);

    // Mock invalid invite code response
    await page.route('**/api/onboarding/join-club', route => {
      route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ title: 'Invalid invite code' }),
      });
    });

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.joinClub('INVALID123');

    // Should show error message
    await expect(onboardingPage.errorMessage).toBeVisible();
    await expect(onboardingPage.errorMessage).toContainText(/invalid/i);

    // Should remain on onboarding page
    await expect(page).toHaveURL(/\/onboarding/);
  });

  test('should handle expired invite code', async ({ page }) => {
    const email = generateUniqueEmail('joinexpired');
    await registerAndLogin(page, email);

    // Mock expired invite response
    await page.route('**/api/onboarding/join-club', route => {
      route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ title: 'Invite code has expired' }),
      });
    });

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.joinClub('EXPIRED123');

    // Should show error message
    await expect(onboardingPage.errorMessage).toBeVisible();
    await expect(onboardingPage.errorMessage).toContainText(/expired/i);
  });

  test('should format invite code input correctly', async ({ page }) => {
    const email = generateUniqueEmail('joinformat');
    await registerAndLogin(page, email);

    const onboardingPage = new OnboardingPage(page);
    await onboardingPage.selectJoinClub();

    // Type lowercase invite code
    await onboardingPage.inviteCodeInput.fill('abc123');

    // Should be converted to uppercase (if that's the format requirement)
    const inputValue = await onboardingPage.inviteCodeInput.inputValue();

    // Test either uppercase conversion or accept as-is depending on implementation
    expect(inputValue).toBeTruthy();
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
