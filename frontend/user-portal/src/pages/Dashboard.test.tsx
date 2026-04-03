import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Dashboard } from './Dashboard';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import { apiClient } from '../lib/api-client';

vi.mock('../contexts/AuthContext');
vi.mock('react-router-dom');
vi.mock('../lib/api-client', () => ({
  apiClient: {
    getCurrentUser: vi.fn(),
  },
}));

describe('Dashboard', () => {
  const mockNavigate = vi.fn();
  const mockLogout = vi.fn();
  const mockGetToken = vi.fn();
  const mockToken = 'mock-jwt-token';

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();

    vi.mocked(useNavigate).mockReturnValue(mockNavigate);
    mockGetToken.mockReturnValue(mockToken);

    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: true,
      isLoading: false,
      user: {
        id: 'user-123',
        email: 'test@example.com',
        fullName: 'Test User',
      },
      login: vi.fn(),
      logout: mockLogout,
      register: vi.fn(),
      getToken: mockGetToken,
    });
  });

  it('should render dashboard with user information', () => {
    render(<Dashboard />);

    expect(screen.getByRole('heading', { name: /dashboard/i })).toBeInTheDocument();
    expect(screen.getByText('User Information')).toBeInTheDocument();
    expect(screen.getByText('Test User')).toBeInTheDocument();
    expect(screen.getByText('test@example.com')).toBeInTheDocument();
  });

  it('should display user info from localStorage when available', () => {
    const userFromStorage = {
      email: 'stored@example.com',
      fullName: 'Stored User',
    };
    localStorage.setItem('user', JSON.stringify(userFromStorage));
    localStorage.setItem('accessToken', 'stored-token');

    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: true,
      isLoading: false,
      user: null,
      login: vi.fn(),
      logout: mockLogout,
      register: vi.fn(),
      getToken: mockGetToken,
    });

    render(<Dashboard />);

    expect(screen.getByText('Stored User')).toBeInTheDocument();
    expect(screen.getByText('stored@example.com')).toBeInTheDocument();
    expect(screen.getByText('Email/Password')).toBeInTheDocument();
  });

  it('should render quick action cards', () => {
    render(<Dashboard />);

    expect(screen.getByText('Quick Actions')).toBeInTheDocument();
    expect(screen.getByText('Update My Profile')).toBeInTheDocument();
    expect(screen.getByText('Manage Club')).toBeInTheDocument();
    expect(screen.getByText('View Sessions')).toBeInTheDocument();
  });

  it('should navigate to profile page when update profile card is clicked', () => {
    render(<Dashboard />);

    const profileCard = screen.getByText('Update My Profile').closest('.option-card');
    fireEvent.click(profileCard!);

    expect(mockNavigate).toHaveBeenCalledWith('/profile');
  });

  it('should call API and display response when test button is clicked', async () => {
    const mockApiResponse = {
      userId: 'user-123',
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
      tenantId: '00000000-0000-0000-0000-000000000000',
      roles: [],
    };

    vi.mocked(apiClient.getCurrentUser).mockResolvedValueOnce(mockApiResponse);
    vi.mocked(apiClient.getCurrentUser).mockResolvedValueOnce(mockApiResponse);

    render(<Dashboard />);

    const testButton = screen.getByRole('button', { name: /test api call/i });
    fireEvent.click(testButton);

    await waitFor(() => {
      expect(screen.getByText('API Response:')).toBeInTheDocument();
      expect(screen.getByText(/"userId": "user-123"/)).toBeInTheDocument();
    });

    expect(apiClient.getCurrentUser).toHaveBeenCalledWith(mockToken);
  });

  it('should display loading state while calling API', async () => {
    let resolveRequest: () => void;
    const requestPromise = new Promise((resolve) => {
      resolveRequest = () => resolve({
        userId: 'user-123',
        email: 'test@example.com',
        fullName: 'Test User',
        onboardingCompleted: true,
        tenantId: '00000000-0000-0000-0000-000000000000',
        roles: [],
      });
    });

    vi.mocked(apiClient.getCurrentUser).mockReturnValueOnce(requestPromise as never);

    render(<Dashboard />);

    const testButton = screen.getByRole('button', { name: /test api call/i });
    fireEvent.click(testButton);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /calling api\.\.\./i })).toBeInTheDocument();
      expect(testButton).toBeDisabled();
    });

    resolveRequest!();
  });

  it('should display error message when API call fails', async () => {
    vi.mocked(apiClient.getCurrentUser).mockResolvedValueOnce({
      userId: 'user-123',
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
      tenantId: '00000000-0000-0000-0000-000000000000',
      roles: [],
    });
    vi.mocked(apiClient.getCurrentUser).mockRejectedValueOnce(new Error('Network error'));

    render(<Dashboard />);

    const testButton = screen.getByRole('button', { name: /test api call/i });
    fireEvent.click(testButton);

    await waitFor(() => {
      expect(screen.getByText('Error:')).toBeInTheDocument();
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('should call logout and navigate when logout button is clicked', async () => {
    render(<Dashboard />);

    const logoutButton = screen.getByRole('button', { name: /logout/i });
    fireEvent.click(logoutButton);

    await waitFor(() => {
      expect(mockLogout).toHaveBeenCalled();
      expect(mockNavigate).toHaveBeenCalledWith('/sign-in');
    });
  });

  it('should show error when no token is available for API call', async () => {
    mockGetToken.mockReturnValue(null);

    render(<Dashboard />);

    const testButton = screen.getByRole('button', { name: /test api call/i });
    fireEvent.click(testButton);

    await waitFor(() => {
      expect(screen.getByText('Error:')).toBeInTheDocument();
      expect(screen.getByText('No authentication token available')).toBeInTheDocument();
    });
  });

  it('should fetch and display current user with roles on mount', async () => {
    const mockApiResponse = {
      userId: 'user-123',
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
      tenantId: 'tenant-456',
      roles: ['ClubAdmin', 'Coach'],
    };

    vi.mocked(apiClient.getCurrentUser).mockResolvedValueOnce(mockApiResponse);

    render(<Dashboard />);

    await waitFor(() => {
      expect(screen.getByText('tenant-456')).toBeInTheDocument();
      expect(screen.getByText('ClubAdmin, Coach')).toBeInTheDocument();
    });
  });

  it('should display fallback when user has no tenant', async () => {
    const mockApiResponse = {
      userId: 'user-123',
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
      tenantId: '00000000-0000-0000-0000-000000000000',
      roles: [],
    };

    vi.mocked(apiClient.getCurrentUser).mockResolvedValueOnce(mockApiResponse);

    render(<Dashboard />);

    await waitFor(() => {
      expect(screen.queryByText(/from-api/)).toBeInTheDocument();
    });
  });

  it('should navigate to club invites when user is ClubAdmin', async () => {
    const mockApiResponse = {
      userId: 'user-123',
      email: 'test@example.com',
      name: 'Test User',
      tenantId: 'tenant-456',
      roles: ['ClubAdmin'],
      clubId: 'club-789',
    };

    vi.mocked(apiClient.getCurrentUser).mockResolvedValueOnce(mockApiResponse);

    render(<Dashboard />);

    await waitFor(() => {
      const manageClubButton = screen.getByRole('button', { name: /manage club/i });
      const manageClubCard = manageClubButton.closest('.option-card');
      expect(manageClubCard).not.toHaveStyle({ opacity: '0.6' });
    });

    const manageClubButton = screen.getByRole('button', { name: /manage club/i });
    const manageClubCard = manageClubButton.closest('.option-card');
    fireEvent.click(manageClubCard!);

    expect(mockNavigate).toHaveBeenCalledWith('/club/invites?clubId=club-789');
  });

  it('should display disabled manage club card when user is not ClubAdmin', async () => {
    const mockApiResponse = {
      userId: 'user-123',
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
      tenantId: 'tenant-456',
      roles: ['Coach'],
    };

    vi.mocked(apiClient.getCurrentUser).mockResolvedValueOnce(mockApiResponse);

    render(<Dashboard />);

    await waitFor(() => {
      const manageClubHeading = screen.getByRole('heading', { name: /manage club/i });
      const manageClubCard = manageClubHeading.closest('.option-card');
      expect(manageClubCard).toHaveStyle({ opacity: '0.6' });
    });
  });
});
