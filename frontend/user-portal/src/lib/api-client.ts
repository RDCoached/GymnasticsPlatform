const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001';

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
}

export interface RegisterResponse {
  message: string;
  requiresEmailVerification: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  tokenType: string;
  user: {
    email: string;
    fullName: string;
    onboardingCompleted: boolean;
  };
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface RefreshTokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  tokenType: string;
}

export interface ProfileResponse {
  email: string;
  fullName: string;
  onboardingCompleted: boolean;
}

export interface UpdateProfileRequest {
  fullName: string;
}

export interface CurrentUserResponse {
  userId: string;
  email: string;
  name: string;
  tenantId: string;
  roles: string[];
  clubId?: string;
}


export interface InviteResponse {
  id: string;
  code: string;
  inviteType: number;
  maxUses: number;
  timesUsed: number;
  expiresAt: string;
  createdAt: string;
  description?: string | null;
  email?: string | null;
  sentAt?: string | null;
}

export interface SendEmailInviteRequest {
  email: string;
  inviteType: number; // 1 = Coach, 2 = Gymnast
  description?: string;
}

export interface EmailInviteResponse {
  id: string;
  code: string;
  email: string;
  inviteType: number;
  expiresAt: string;
  sentAt: string;
  description?: string | null;
}

export class ApiClient {
  private baseUrl: string;

  constructor(baseUrl = API_BASE_URL) {
    this.baseUrl = baseUrl;
  }

  private getAuthHeaders(token: string): HeadersInit {
    return {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    };
  }

  async register(request: RegisterRequest): Promise<RegisterResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.detail || 'Registration failed');
    }

    return response.json();
  }

  async login(request: LoginRequest): Promise<LoginResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.detail || 'Login failed');
    }

    return response.json();
  }

  async refreshToken(request: RefreshTokenRequest): Promise<RefreshTokenResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.detail || 'Token refresh failed');
    }

    return response.json();
  }

  async getCurrentUser(token: string): Promise<CurrentUserResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/me`, {
      headers: this.getAuthHeaders(token),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to fetch current user' }));
      throw new Error(error.detail || 'Failed to fetch current user');
    }

    return response.json();
  }

  async getProfile(token: string): Promise<ProfileResponse> {
    const response = await fetch(`${this.baseUrl}/api/profile`, {
      headers: this.getAuthHeaders(token),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to fetch profile' }));
      throw new Error(error.detail || 'Failed to fetch profile');
    }

    return response.json();
  }

  async updateProfile(token: string, request: UpdateProfileRequest): Promise<ProfileResponse> {
    const response = await fetch(`${this.baseUrl}/api/profile`, {
      method: 'PUT',
      headers: this.getAuthHeaders(token),
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to update profile' }));
      throw new Error(error.detail || 'Failed to update profile');
    }

    return response.json();
  }


  async listInvites(token: string, clubId: string): Promise<InviteResponse[]> {
    const response = await fetch(`${this.baseUrl}/api/clubs/${clubId}/invites`, {
      headers: this.getAuthHeaders(token),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to fetch invites' }));
      throw new Error(error.detail || 'Failed to fetch invites');
    }

    return response.json();
  }

  async sendEmailInvite(
    token: string,
    clubId: string,
    request: SendEmailInviteRequest
  ): Promise<EmailInviteResponse> {
    const response = await fetch(`${this.baseUrl}/api/clubs/${clubId}/invites/send-email`, {
      method: 'POST',
      headers: this.getAuthHeaders(token),
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to send email invite' }));
      throw new Error(error.detail || 'Failed to send email invite');
    }

    return response.json();
  }
}

export const apiClient = new ApiClient();
