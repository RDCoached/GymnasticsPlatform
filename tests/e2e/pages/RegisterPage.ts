import { Page, Locator } from '@playwright/test';

export class RegisterPage {
  readonly page: Page;
  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly confirmPasswordInput: Locator;
  readonly fullNameInput: Locator;
  readonly registerButton: Locator;
  readonly signInLink: Locator;
  readonly errorMessage: Locator;
  readonly successMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.emailInput = page.getByLabel(/email/i);
    this.passwordInput = page.getByLabel('Password', { exact: true });
    this.confirmPasswordInput = page.getByLabel(/confirm password/i);
    this.fullNameInput = page.getByLabel(/full name/i);
    this.registerButton = page.getByRole('button', { name: /register/i });
    this.signInLink = page.getByRole('link', { name: /sign in/i });
    this.errorMessage = page.getByRole('alert');
    this.successMessage = page.locator('[role="status"]');
  }

  async goto(): Promise<void> {
    await this.page.goto('/register');
  }

  async register(email: string, password: string, fullName: string): Promise<void> {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.confirmPasswordInput.fill(password);
    await this.fullNameInput.fill(fullName);
    await this.registerButton.click();
  }

  async navigateToSignIn(): Promise<void> {
    await this.signInLink.click();
  }
}
