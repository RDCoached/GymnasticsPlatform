import { Page, Locator } from '@playwright/test';

export class OnboardingPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly createClubButton: Locator;
  readonly joinClubButton: Locator;
  readonly individualModeButton: Locator;
  readonly backButton: Locator;

  // Create Club Form
  readonly clubNameInput: Locator;
  readonly createClubSubmitButton: Locator;

  // Join Club Form
  readonly inviteCodeInput: Locator;
  readonly joinClubSubmitButton: Locator;

  // Error message
  readonly errorMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /welcome to gymnastics platform/i });
    this.createClubButton = page.getByRole('button', { name: /create club/i });
    this.joinClubButton = page.getByRole('button', { name: /join club/i });
    this.individualModeButton = page.getByRole('button', { name: /go individual/i });
    this.backButton = page.getByRole('button', { name: /back/i });

    // Create Club Form fields
    this.clubNameInput = page.getByLabel(/club name/i);
    this.createClubSubmitButton = page.getByRole('button', { name: /create/i });

    // Join Club Form fields
    this.inviteCodeInput = page.getByLabel(/invite code/i);
    this.joinClubSubmitButton = page.getByRole('button', { name: /join/i });

    // Error message
    this.errorMessage = page.getByRole('alert');
  }

  async goto(): Promise<void> {
    await this.page.goto('/onboarding');
  }

  async selectCreateClub(): Promise<void> {
    await this.createClubButton.click();
  }

  async selectJoinClub(): Promise<void> {
    await this.joinClubButton.click();
  }

  async selectIndividualMode(): Promise<void> {
    await this.individualModeButton.click();

    // Wait for navigation to dashboard (success case)
    // If error occurs, alert will be shown and navigation won't happen
    try {
      await this.page.waitForURL(/\/dashboard/, { timeout: 10000 });
    } catch {
      // Navigation timeout is expected on error - alert will have been shown
      // Test should verify the alert was displayed
    }
  }

  async createClub(clubName: string): Promise<void> {
    await this.selectCreateClub();
    await this.clubNameInput.fill(clubName);
    await this.createClubSubmitButton.click();

    // Wait for either successful navigation to dashboard or error message
    await Promise.race([
      this.page.waitForURL(/\/dashboard/, { timeout: 10000 }),
      this.errorMessage.waitFor({ state: 'visible', timeout: 10000 })
    ]);
  }

  async joinClub(inviteCode: string): Promise<void> {
    await this.selectJoinClub();
    await this.inviteCodeInput.fill(inviteCode);
    await this.joinClubSubmitButton.click();

    // Wait for either successful navigation to dashboard or error message
    await Promise.race([
      this.page.waitForURL(/\/dashboard/, { timeout: 10000 }),
      this.errorMessage.waitFor({ state: 'visible', timeout: 10000 })
    ]);
  }

  async goBack(): Promise<void> {
    await this.backButton.click();
  }
}
