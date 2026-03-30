import { test, expect } from '@playwright/test';
import { SignInPage } from '../pages/SignInPage';
import { RegisterPage } from '../pages/RegisterPage';
import { OnboardingPage } from '../pages/OnboardingPage';
import { generateUniqueEmail } from '../helpers/test-data';

test.describe('Authentication Flow', () => {
  test('should display sign-in page for unauthenticated users', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/\/sign-in/);
  });

  test('should register a new user successfully', async ({ page }) => {
    const registerPage = new RegisterPage(page);
    await registerPage.goto();

    const email = generateUniqueEmail('newuser');
    const password = 'TestPassword123!';
    const fullName = 'New Test User';

    await registerPage.register(email, password, fullName);

    await expect(page).toHaveURL(/\/sign-in/);
  });

  test.skip('should prevent registration with invalid email', async ({ page }) => {
    const registerPage = new RegisterPage(page);
    await registerPage.goto();

    await registerPage.register('invalid-email', 'TestPassword123!', 'Test User');

    await expect(registerPage.errorMessage).toBeVisible();
  });

  test.skip('should prevent registration with weak password', async ({ page }) => {
    const registerPage = new RegisterPage(page);
    await registerPage.goto();

    const email = generateUniqueEmail('weakpass');
    await registerPage.register(email, 'weak', 'Test User');

    await expect(registerPage.errorMessage).toBeVisible();
  });

  test('should login with valid credentials', async ({ page }) => {
    // First register a user
    const registerPage = new RegisterPage(page);
    await registerPage.goto();

    const email = generateUniqueEmail('logintest');
    const password = 'TestPassword123!';
    const fullName = 'Login Test User';

    await registerPage.register(email, password, fullName);
    await expect(page).toHaveURL(/\/sign-in/);

    // Then login
    const signInPage = new SignInPage(page);
    await signInPage.signIn(email, password);

    // Should redirect to onboarding for new users
    await expect(page).toHaveURL(/\/onboarding/);
  });

  test.skip('should show error with invalid credentials', async ({ page }) => {
    const signInPage = new SignInPage(page);
    await signInPage.goto();

    await signInPage.signIn('nonexistent@test.com', 'WrongPassword123!');

    await expect(signInPage.errorMessage).toBeVisible();
    await expect(page).toHaveURL(/\/sign-in/);
  });

  test.skip('should navigate between sign-in and register pages', async ({ page }) => {
    const signInPage = new SignInPage(page);
    await signInPage.goto();

    await signInPage.navigateToRegister();
    await expect(page).toHaveURL(/\/register/);

    const registerPage = new RegisterPage(page);
    await registerPage.navigateToSignIn();
    await expect(page).toHaveURL(/\/sign-in/);
  });

  test.skip('should redirect authenticated user to onboarding if incomplete', async ({ page }) => {
    const registerPage = new RegisterPage(page);
    await registerPage.goto();

    const email = generateUniqueEmail('redirect');
    const password = 'TestPassword123!';

    await registerPage.register(email, password, 'Redirect Test');
    await expect(page).toHaveURL(/\/sign-in/);

    const signInPage = new SignInPage(page);
    await signInPage.signIn(email, password);

    await expect(page).toHaveURL(/\/onboarding/);

    // Try to navigate to dashboard directly
    await page.goto('/dashboard');

    // Should redirect back to onboarding
    await expect(page).toHaveURL(/\/onboarding/);
  });
});
