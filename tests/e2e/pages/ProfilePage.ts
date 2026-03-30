import { Page, Locator } from '@playwright/test';

export class ProfilePage {
  readonly page: Page;
  readonly heading: Locator;
  readonly nameInput: Locator;
  readonly emailInput: Locator;
  readonly updateButton: Locator;
  readonly successMessage: Locator;
  readonly errorMessage: Locator;
  readonly backButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /update profile|your profile/i });
    this.nameInput = page.getByLabel(/full name/i);
    this.emailInput = page.getByLabel(/email/i);
    this.updateButton = page.getByRole('button', { name: /update|save profile/i });
    this.successMessage = page.locator('.success-message, [role="status"]').filter({ hasText: /successfully/i });
    this.errorMessage = page.getByRole('alert');
    this.backButton = page.getByRole('button', { name: /back to dashboard/i });
  }

  async goto(): Promise<void> {
    await this.page.goto('/profile');
  }

  async updateProfile(fullName: string): Promise<void> {
    await this.nameInput.clear();
    await this.nameInput.fill(fullName);
    await this.updateButton.click();

    // Wait for either success message or error
    await Promise.race([
      this.successMessage.waitFor({ state: 'visible', timeout: 10000 }),
      this.errorMessage.waitFor({ state: 'visible', timeout: 10000 })
    ]);
  }

  async goBackToDashboard(): Promise<void> {
    await this.backButton.click();
    await this.page.waitForURL(/\/dashboard/, { timeout: 10000 });
  }
}
