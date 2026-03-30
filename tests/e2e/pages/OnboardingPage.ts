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
  }

  async createClub(clubName: string): Promise<void> {
    await this.selectCreateClub();
    await this.clubNameInput.fill(clubName);
    await this.createClubSubmitButton.click();
  }

  async joinClub(inviteCode: string): Promise<void> {
    await this.selectJoinClub();
    await this.inviteCodeInput.fill(inviteCode);
    await this.joinClubSubmitButton.click();
  }

  async goBack(): Promise<void> {
    await this.backButton.click();
  }
}
