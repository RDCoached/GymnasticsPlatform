const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001';

export interface UserProfileResponse {
  id: string;
  keycloakUserId: string;
  email: string;
  fullName: string;
  tenantId: string;
  onboardingCompleted: boolean;
  onboardingChoice: string | null;
}

export interface SyncTenantResponse {
  userId: string;
  email: string;
  tenantId: string;
  message: string;
}

export interface CreateInviteRequest {
  inviteType: 'Coach' | 'Gymnast';
  maxUses: number;
  expiryDays: number;
  description?: string;
}

export interface InviteResponse {
  id: string;
  code: string;
  inviteType: 'Coach' | 'Gymnast';
  maxUses: number;
  timesUsed: number;
  expiresAt: string;
  createdAt: string;
  description?: string;
}

export class AdminApiClient {
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

  async listUsers(token: string): Promise<UserProfileResponse[]> {
    const response = await fetch(`${this.baseUrl}/api/admin/users`, {
      headers: this.getAuthHeaders(token),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to fetch users' }));
      throw new Error(error.detail || 'Failed to fetch users');
    }

    return response.json();
  }

  async syncUserTenant(token: string, userId: string): Promise<SyncTenantResponse> {
    const response = await fetch(`${this.baseUrl}/api/admin/users/${userId}/sync-tenant`, {
      method: 'POST',
      headers: this.getAuthHeaders(token),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to sync tenant' }));
      throw new Error(error.detail || 'Failed to sync tenant');
    }

    return response.json();
  }

  async createInvite(token: string, clubId: string, request: CreateInviteRequest): Promise<InviteResponse> {
    const response = await fetch(`${this.baseUrl}/api/clubs/${clubId}/invites`, {
      method: 'POST',
      headers: this.getAuthHeaders(token),
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ detail: 'Failed to create invite' }));
      throw new Error(error.detail || 'Failed to create invite');
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
}

export const adminApiClient = new AdminApiClient();
