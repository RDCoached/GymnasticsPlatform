import { test, expect } from '@playwright/test';
import { generateUniqueEmail, generateClubName } from '../helpers/test-data';

/**
 * Critical Path E2E Tests
 *
 * Following the test pyramid, we have only 3 essential E2E tests
 * that cover the most important user journeys.
 */

test.describe('Critical User Journeys', () => {
  test('Complete Individual User Journey', async ({ page }) => {
    // This test covers: Register → Login → Individual Onboarding → Dashboard
    const email = generateUniqueEmail('individual');
    const password = 'TestPassword123!';
    const fullName = 'Individual Test User';

    // Step 1: Register
    await page.goto('/register');
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.fill('input[id="confirmPassword"]', password);
    await page.fill('input[id="fullName"]', fullName);

    // Click submit and wait for navigation
    await Promise.all([
      page.waitForURL(/\/sign-in/, { timeout: 10000 }),
      page.click('button[type="submit"]')
    ]);

    // Step 2: Login
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.click('button[type="submit"]');

    // Should reach onboarding
    await expect(page).toHaveURL(/\/onboarding/);

    // Step 3: Complete Individual Mode Onboarding
    await page.click('button:has-text("Use Individual Mode")');

    // Should land on dashboard
    await expect(page).toHaveURL(/\/dashboard/);
    await expect(page.locator('h1')).toContainText('Dashboard');
  });

  test('Complete Create Club Journey', async ({ page }) => {
    // This test covers: Register → Login → Create Club Onboarding → Dashboard
    const email = generateUniqueEmail('clubowner');
    const password = 'TestPassword123!';
    const fullName = 'Club Owner User';
    const clubName = generateClubName();

    // Step 1: Register
    await page.goto('/register');
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.fill('input[id="confirmPassword"]', password);
    await page.fill('input[id="fullName"]', fullName);

    // Click submit and wait for navigation
    await Promise.all([
      page.waitForURL(/\/sign-in/, { timeout: 10000 }),
      page.click('button[type="submit"]')
    ]);

    // Step 2: Login
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.click('button[type="submit"]');

    await expect(page).toHaveURL(/\/onboarding/);

    // Step 3: Create Club
    await page.click('button:has-text("Create a New Club")');
    await page.fill('input[id="clubName"]', clubName);
    await page.click('button[type="submit"]:has-text("Create Club")');

    // Should land on dashboard
    await expect(page).toHaveURL(/\/dashboard/);
    await expect(page.locator('h1')).toContainText('Dashboard');
  });

  test('Unauthenticated Access Redirect', async ({ page }) => {
    // This test ensures protected routes redirect to sign-in
    const protectedRoutes = ['/dashboard', '/profile', '/club/invites', '/onboarding'];

    for (const route of protectedRoutes) {
      await page.goto(route);
      await expect(page).toHaveURL(/\/sign-in/, {
        timeout: 5000
      });
    }
  });
});
