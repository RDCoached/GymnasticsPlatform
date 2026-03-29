import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ClubInvitesPage } from './ClubInvitesPage';
import { useKeycloak } from '@react-keycloak/web';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiClient } from '../lib/api-client';
import type { InviteResponse } from '../lib/api-client';

vi.mock('@react-keycloak/web');
vi.mock('react-router-dom', () => ({
  useNavigate: vi.fn(),
  useSearchParams: vi.fn(),
}));
vi.mock('../lib/api-client', () => ({
  apiClient: {
    createInvite: vi.fn(),
    listInvites: vi.fn(),
  },
}));

// Mock clipboard API
Object.defineProperty(navigator, 'clipboard', {
  value: {
    writeText: vi.fn(() => Promise.resolve()),
  },
  writable: true,
});

describe('ClubInvitesPage', () => {
  const mockNavigate = vi.fn();
  const mockToken = 'mock-jwt-token';
  const clubId = 'club-123';

  const mockInvite: InviteResponse = {
    id: 'invite-1',
    code: 'ABC123',
    inviteType: 1, // Coach
    maxUses: 10,
    timesUsed: 3,
    expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
    createdAt: new Date().toISOString(),
    description: 'Test invite',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    localStorage.setItem('accessToken', mockToken);

    vi.mocked(useNavigate).mockReturnValue(mockNavigate);
    vi.mocked(useSearchParams).mockReturnValue([
      new URLSearchParams(`clubId=${clubId}`),
      vi.fn(),
    ] as never);
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: mockToken,
        authenticated: false,
        logout: vi.fn(),
      },
    } as never);
  });

  it('should render error when clubId is missing', () => {
    vi.mocked(useSearchParams).mockReturnValue([
      new URLSearchParams(),
      vi.fn(),
    ] as never);

    render(<ClubInvitesPage />);

    expect(screen.getByText(/club id is required/i)).toBeInTheDocument();
  });

  it('should render the page with form and empty invites list', async () => {
    vi.mocked(apiClient.listInvites).mockResolvedValue([]);

    render(<ClubInvitesPage />);

    expect(screen.getByRole('heading', { name: /club invites/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /create new invite/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /active invites/i })).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText(/no invites created yet/i)).toBeInTheDocument();
    });
  });

  it('should display existing invites in a table', async () => {
    vi.mocked(apiClient.listInvites).mockResolvedValue([mockInvite]);

    render(<ClubInvitesPage />);

    // Wait for table row to appear
    expect(await screen.findByText('ABC123')).toBeInTheDocument();
    expect(screen.getByText('3 / 10')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Test invite')).toBeInTheDocument();

    // Check that Coach appears in the table (not just in the dropdown)
    const tableRows = screen.getAllByRole('row');
    const inviteRow = tableRows.find(row => row.textContent?.includes('ABC123'));
    expect(inviteRow?.textContent).toContain('Coach');
  });

  it('should create a new invite when form is submitted', async () => {
    vi.mocked(apiClient.listInvites).mockResolvedValue([]);
    vi.mocked(apiClient.createInvite).mockResolvedValue(mockInvite);

    render(<ClubInvitesPage />);

    await waitFor(() => {
      expect(screen.getByText(/no invites created yet/i)).toBeInTheDocument();
    });

    // Fill form
    const inviteTypeSelect = screen.getByLabelText(/invite type/i);
    const maxUsesInput = screen.getByLabelText(/max uses/i);
    const expiryDaysInput = screen.getByLabelText(/expiry days/i);
    const descriptionInput = screen.getByLabelText(/description/i);

    fireEvent.change(inviteTypeSelect, { target: { value: '2' } }); // Gymnast
    fireEvent.change(maxUsesInput, { target: { value: '20' } });
    fireEvent.change(expiryDaysInput, { target: { value: '30' } });
    fireEvent.change(descriptionInput, { target: { value: 'New invite' } });

    // Submit form
    const submitButton = screen.getByRole('button', { name: /create invite/i });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(apiClient.createInvite).toHaveBeenCalledWith(
        mockToken,
        clubId,
        {
          inviteType: 2,
          maxUses: 20,
          expiryDays: 30,
          description: 'New invite',
        }
      );
    });

    // Verify form was reset
    await waitFor(() => {
      expect(maxUsesInput).toHaveValue(10);
      expect(expiryDaysInput).toHaveValue(7);
      expect(descriptionInput).toHaveValue('');
    });
  });

  it('should copy invite code to clipboard when copy button is clicked', async () => {
    vi.mocked(apiClient.listInvites).mockResolvedValue([mockInvite]);

    render(<ClubInvitesPage />);

    await waitFor(() => {
      expect(screen.getByText('ABC123')).toBeInTheDocument();
    });

    const copyButton = screen.getByRole('button', { name: /copy code/i });
    fireEvent.click(copyButton);

    await waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('ABC123');
      expect(screen.getByText(/✓ copied/i)).toBeInTheDocument();
    });
  });

  it('should mark expired invites with correct status', async () => {
    const expiredInvite: InviteResponse = {
      ...mockInvite,
      expiresAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(), // Yesterday
    };

    vi.mocked(apiClient.listInvites).mockResolvedValue([expiredInvite]);

    render(<ClubInvitesPage />);

    await waitFor(() => {
      expect(screen.getByText('Expired')).toBeInTheDocument();
    });
  });

  it('should mark full invites with correct status', async () => {
    const fullInvite: InviteResponse = {
      ...mockInvite,
      timesUsed: 10, // Same as maxUses
    };

    vi.mocked(apiClient.listInvites).mockResolvedValue([fullInvite]);

    render(<ClubInvitesPage />);

    await waitFor(() => {
      expect(screen.getByText('Full')).toBeInTheDocument();
    });
  });

  it('should display Gymnast invite type correctly', async () => {
    const gymnastInvite: InviteResponse = {
      ...mockInvite,
      inviteType: 2, // Gymnast
    };

    vi.mocked(apiClient.listInvites).mockResolvedValue([gymnastInvite]);

    render(<ClubInvitesPage />);

    await waitFor(() => {
      expect(screen.getByText('Gymnast')).toBeInTheDocument();
    });
  });

  it('should navigate back to dashboard when back button is clicked', async () => {
    vi.mocked(apiClient.listInvites).mockResolvedValue([]);

    render(<ClubInvitesPage />);

    const backButton = screen.getByRole('button', { name: /← back to dashboard/i });
    fireEvent.click(backButton);

    expect(mockNavigate).toHaveBeenCalledWith('/dashboard');
  });

  it('should display error when API call fails', async () => {
    vi.mocked(apiClient.listInvites).mockRejectedValue(new Error('API Error'));

    render(<ClubInvitesPage />);

    await waitFor(() => {
      expect(screen.getByText(/api error/i)).toBeInTheDocument();
    });
  });

  it('should show loading state while creating invite', async () => {
    vi.mocked(apiClient.listInvites).mockResolvedValue([]);
    vi.mocked(apiClient.createInvite).mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve(mockInvite), 100))
    );

    render(<ClubInvitesPage />);

    await waitFor(() => {
      expect(screen.getByText(/no invites created yet/i)).toBeInTheDocument();
    });

    const submitButton = screen.getByRole('button', { name: /create invite/i });
    fireEvent.click(submitButton);

    expect(screen.getByText(/creating\.\.\./i)).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText(/create invite/i)).toBeInTheDocument();
    });
  });

  it('should logout when logout button is clicked', () => {
    const mockLogout = vi.fn();
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: mockToken,
        authenticated: false,
        logout: mockLogout,
      },
    } as never);

    vi.mocked(apiClient.listInvites).mockResolvedValue([]);

    render(<ClubInvitesPage />);

    const logoutButton = screen.getByRole('button', { name: /logout/i });
    fireEvent.click(logoutButton);

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(localStorage.getItem('user')).toBeNull();
  });
});
