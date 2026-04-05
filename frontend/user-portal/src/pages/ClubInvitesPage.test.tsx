import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ClubInvitesPage } from './ClubInvitesPage';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiClient } from '../lib/api-client';
import type { InviteResponse } from '../lib/api-client';

vi.mock('../contexts/AuthContext');
vi.mock('react-router-dom', () => ({
  useNavigate: vi.fn(),
  useSearchParams: vi.fn(),
}));
vi.mock('../lib/api-client', () => ({
  apiClient: {
    listInvites: vi.fn(),
    sendEmailInvite: vi.fn(),
  },
}));

Object.defineProperty(navigator, 'clipboard', {
  value: {
    writeText: vi.fn(() => Promise.resolve()),
  },
  writable: true,
});

describe('ClubInvitesPage', () => {
  const mockNavigate = vi.fn();
  const mockLogout = vi.fn();
  const mockGetToken = vi.fn();
  const mockToken = 'mock-jwt-token';
  const clubId = 'club-123';

  const mockInvite: InviteResponse = {
    id: 'invite-1',
    code: 'ABC123',
    inviteType: 1,
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

    mockGetToken.mockReturnValue(mockToken);

    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: true,
      isLoading: false,
      user: null,
      login: vi.fn(),
      logout: mockLogout,
      register: vi.fn(),
      getToken: mockGetToken,
    });

    vi.mocked(useNavigate).mockReturnValue(mockNavigate);
    vi.mocked(useSearchParams).mockReturnValue([
      new URLSearchParams(`clubId=${clubId}`),
      vi.fn(),
    ] as never);
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
    expect(screen.getByRole('heading', { name: /send email invitation/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /active invites/i })).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText(/no invites created yet/i)).toBeInTheDocument();
    });
  });

  it('should display existing invites in a table', async () => {
    vi.mocked(apiClient.listInvites).mockResolvedValue([mockInvite]);

    render(<ClubInvitesPage />);

    const codes = await screen.findAllByText('ABC123');
    expect(codes.length).toBeGreaterThan(0);
    expect(screen.getAllByText('3 / 10').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Active').length).toBeGreaterThan(0);

    const tableRows = screen.getAllByRole('row');
    const inviteRow = tableRows.find(row => row.textContent?.includes('ABC123'));
    expect(inviteRow?.textContent).toContain('Coach');
  });


  it('should copy invite code to clipboard when copy button is clicked', async () => {
    vi.mocked(apiClient.listInvites).mockResolvedValue([mockInvite]);

    render(<ClubInvitesPage />);

    await waitFor(() => {
      const codes = screen.getAllByText('ABC123');
      expect(codes.length).toBeGreaterThan(0);
    });

    const copyButtons = screen.getAllByRole('button', { name: /copy/i });
    fireEvent.click(copyButtons[0]);

    await waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('ABC123');
      expect(screen.getAllByText(/✓ copied/i).length).toBeGreaterThan(0);
    });
  });

  it('should mark expired invites with correct status', async () => {
    const expiredInvite: InviteResponse = {
      ...mockInvite,
      expiresAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
    };

    vi.mocked(apiClient.listInvites).mockResolvedValue([expiredInvite]);

    render(<ClubInvitesPage />);

    await waitFor(() => {
      expect(screen.getAllByText('Expired').length).toBeGreaterThan(0);
    });
  });

  it('should mark full invites with correct status', async () => {
    const fullInvite: InviteResponse = {
      ...mockInvite,
      timesUsed: 10,
    };

    vi.mocked(apiClient.listInvites).mockResolvedValue([fullInvite]);

    render(<ClubInvitesPage />);

    await waitFor(() => {
      expect(screen.getAllByText('Full').length).toBeGreaterThan(0);
    });
  });

  it('should display Gymnast invite type correctly', async () => {
    const gymnastInvite: InviteResponse = {
      ...mockInvite,
      inviteType: 2,
    };

    vi.mocked(apiClient.listInvites).mockResolvedValue([gymnastInvite]);

    render(<ClubInvitesPage />);

    await waitFor(() => {
      const tableRows = screen.getAllByRole('row');
      const inviteRow = tableRows.find(row => row.textContent?.includes('ABC123'));
      expect(inviteRow?.textContent).toContain('Gymnast');
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


  it('should logout when logout button is clicked', async () => {
    vi.mocked(apiClient.listInvites).mockResolvedValue([]);

    render(<ClubInvitesPage />);

    const logoutButton = screen.getByRole('button', { name: /logout/i });
    fireEvent.click(logoutButton);

    await waitFor(() => {
      expect(mockLogout).toHaveBeenCalled();
      expect(mockNavigate).toHaveBeenCalledWith('/sign-in');
    });
  });
});
