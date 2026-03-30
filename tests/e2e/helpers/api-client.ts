import { APIRequestContext } from '@playwright/test';

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  user: {
    email: string;
    fullName: string;
    onboardingCompleted: boolean;
  };
}

export interface OnboardingStatusResponse {
  onboardingCompleted: boolean;
  onboardingChoice: string | null;
  tenantId: string;
}

const API_BASE_URL = 'http://localhost:5137';

export class ApiClient {
  constructor(private readonly request: APIRequestContext) {}

  async register(email: string, password: string, fullName: string): Promise<void> {
    const response = await this.request.post(`${API_BASE_URL}/api/auth/register`, {
      data: {
        email,
        password,
        fullName,
      },
    });

    if (!response.ok()) {
      const errorText = await response.text();
      throw new Error(`Registration failed: ${response.status()} - ${errorText}`);
    }
  }

  async login(email: string, password: string): Promise<LoginResponse> {
    const response = await this.request.post(`${API_BASE_URL}/api/auth/login`, {
      data: {
        email,
        password,
      },
    });

    if (!response.ok()) {
      const errorText = await response.text();
      throw new Error(`Login failed: ${response.status()} - ${errorText}`);
    }

    return await response.json();
  }

  async getOnboardingStatus(token: string): Promise<OnboardingStatusResponse> {
    const response = await this.request.get(`${API_BASE_URL}/api/onboarding/status`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok()) {
      const errorText = await response.text();
      throw new Error(`Get onboarding status failed: ${response.status()} - ${errorText}`);
    }

    return await response.json();
  }

  async createClub(token: string, clubName: string): Promise<unknown> {
    const response = await this.request.post(`${API_BASE_URL}/api/onboarding/create-club`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
      data: {
        name: clubName,
      },
    });

    if (!response.ok()) {
      const errorText = await response.text();
      throw new Error(`Create club failed: ${response.status()} - ${errorText}`);
    }

    return await response.json();
  }

  async chooseIndividualMode(token: string): Promise<unknown> {
    const response = await this.request.post(`${API_BASE_URL}/api/onboarding/individual`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok()) {
      const errorText = await response.text();
      throw new Error(`Choose individual mode failed: ${response.status()} - ${errorText}`);
    }

    return await response.json();
  }

  async joinClub(token: string, inviteCode: string): Promise<unknown> {
    const response = await this.request.post(`${API_BASE_URL}/api/onboarding/join-club`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
      data: {
        inviteCode,
      },
    });

    if (!response.ok()) {
      const errorText = await response.text();
      throw new Error(`Join club failed: ${response.status()} - ${errorText}`);
    }

    return await response.json();
  }
}
