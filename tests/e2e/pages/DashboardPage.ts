import { Page, Locator } from '@playwright/test';

export class DashboardPage {
  readonly page: Page;
  readonly heading: Locator;
  readonly profileLink: Locator;
  readonly clubInvitesLink: Locator;
  readonly welcomeMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.heading = page.getByRole('heading', { name: /dashboard/i });
    this.profileLink = page.getByRole('link', { name: /profile/i });
    this.clubInvitesLink = page.getByRole('link', { name: /club invites/i });
    this.welcomeMessage = page.getByText(/welcome/i);
  }

  async goto(): Promise<void> {
    await this.page.goto('/dashboard');
  }

  async navigateToProfile(): Promise<void> {
    await this.profileLink.click();
  }

  async navigateToClubInvites(): Promise<void> {
    await this.clubInvitesLink.click();
  }
}
