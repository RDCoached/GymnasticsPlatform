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

export interface GymnastResponse {
  id: string;
  name: string;
  email: string;
}

export interface CreateGymnastRequest {
  email: string;
  fullName: string;
}

export interface UpdateGymnastRequest {
  fullName: string;
}

export interface StartProgrammeBuilderRequest {
  gymnastId: string;
  goals: string;
  ragScope?: 'gymnast' | 'tenant';
}

export interface BuilderSessionResult {
  sessionId: string;
  suggestion: string;
}

export interface ContinueProgrammeBuilderRequest {
  message: string;
}

export class ApiClient {
  private baseUrl: string;

  constructor(baseUrl = API_BASE_URL) {
    this.baseUrl = baseUrl;
  }

  private getAuthHeaders(token?: string | null): HeadersInit {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
    };

    // Only add Authorization header if token is provided (for OAuth)
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    return headers;
  }

  async register(request: RegisterRequest): Promise<RegisterResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
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
      credentials: 'include',
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
      credentials: 'include',
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.detail || 'Token refresh failed');
    }

    return response.json();
  }

  async getCurrentUser(token?: string | null): Promise<CurrentUserResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/me`, {
      headers: this.getAuthHeaders(token),
      credentials: 'include',
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to fetch current user' }));
      throw new Error(error.detail || 'Failed to fetch current user');
    }

    return response.json();
  }

  async getProfile(token?: string | null): Promise<ProfileResponse> {
    const response = await fetch(`${this.baseUrl}/api/profile`, {
      headers: this.getAuthHeaders(token),
      credentials: 'include',
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to fetch profile' }));
      throw new Error(error.detail || 'Failed to fetch profile');
    }

    return response.json();
  }

  async updateProfile(request: UpdateProfileRequest, token?: string | null): Promise<ProfileResponse> {
    const response = await fetch(`${this.baseUrl}/api/profile`, {
      method: 'PUT',
      headers: this.getAuthHeaders(token),
      credentials: 'include',
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to update profile' }));
      throw new Error(error.detail || 'Failed to update profile');
    }

    return response.json();
  }


  async listInvites(token: string | null | undefined, clubId: string): Promise<InviteResponse[]> {
    const response = await fetch(`${this.baseUrl}/api/clubs/${clubId}/invites`, {
      headers: this.getAuthHeaders(token),
      credentials: 'include',
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to fetch invites' }));
      throw new Error(error.detail || 'Failed to fetch invites');
    }

    return response.json();
  }

  async sendEmailInvite(
    token: string | null | undefined,
    clubId: string,
    request: SendEmailInviteRequest
  ): Promise<EmailInviteResponse> {
    const response = await fetch(`${this.baseUrl}/api/clubs/${clubId}/invites/send-email`, {
      method: 'POST',
      headers: this.getAuthHeaders(token),
      credentials: 'include',
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to send email invite' }));
      throw new Error(error.detail || 'Failed to send email invite');
    }

    return response.json();
  }

  async listGymnasts(token: string): Promise<GymnastResponse[]> {
    const response = await fetch(`${this.baseUrl}/api/gymnasts`, {
      headers: this.getAuthHeaders(token),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to fetch gymnasts' }));
      throw new Error(error.detail || 'Failed to fetch gymnasts');
    }

    return response.json();
  }

  async createGymnast(token: string, request: CreateGymnastRequest): Promise<GymnastResponse> {
    const response = await fetch(`${this.baseUrl}/api/gymnasts`, {
      method: 'POST',
      headers: this.getAuthHeaders(token),
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to create gymnast' }));
      throw new Error(error.detail || 'Failed to create gymnast');
    }

    return response.json();
  }

  async updateGymnast(token: string, id: string, request: UpdateGymnastRequest): Promise<GymnastResponse> {
    const response = await fetch(`${this.baseUrl}/api/gymnasts/${id}`, {
      method: 'PUT',
      headers: this.getAuthHeaders(token),
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to update gymnast' }));
      throw new Error(error.detail || 'Failed to update gymnast');
    }

    return response.json();
  }

  async deleteGymnast(token: string, id: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/gymnasts/${id}`, {
      method: 'DELETE',
      headers: this.getAuthHeaders(token),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to delete gymnast' }));
      throw new Error(error.detail || 'Failed to delete gymnast');
    }
  }

  async startProgrammeBuilder(
    token: string,
    request: StartProgrammeBuilderRequest
  ): Promise<BuilderSessionResult> {
    const response = await fetch(`${this.baseUrl}/api/programme-builder/start`, {
      method: 'POST',
      headers: this.getAuthHeaders(token),
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to start programme builder' }));
      throw new Error(error.detail || 'Failed to start programme builder');
    }

    return response.json();
  }

  async continueProgrammeBuilder(
    token: string,
    sessionId: string,
    request: ContinueProgrammeBuilderRequest
  ): Promise<BuilderSessionResult> {
    const response = await fetch(`${this.baseUrl}/api/programme-builder/continue/${sessionId}`, {
      method: 'POST',
      headers: this.getAuthHeaders(token),
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to continue session' }));
      throw new Error(error.detail || 'Failed to continue session');
    }

    return response.json();
  }

  async acceptProgrammeBuilder(
    token: string,
    sessionId: string
  ): Promise<string> {
    const response = await fetch(`${this.baseUrl}/api/programme-builder/accept/${sessionId}`, {
      method: 'POST',
      headers: this.getAuthHeaders(token),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to accept programme' }));
      throw new Error(error.detail || 'Failed to accept programme');
    }

    const result = await response.json();
    return result; // Returns programmeId (string)
  }
}

export const apiClient = new ApiClient();
