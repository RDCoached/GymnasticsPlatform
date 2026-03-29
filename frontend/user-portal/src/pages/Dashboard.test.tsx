import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Dashboard } from './Dashboard';
import { useKeycloak } from '@react-keycloak/web';
import { useNavigate } from 'react-router-dom';
import { apiClient } from '../lib/api-client';

vi.mock('@react-keycloak/web');
vi.mock('react-router-dom');
vi.mock('../lib/api-client', () => ({
  apiClient: {
    getCurrentUser: vi.fn(),
  },
}));

describe('Dashboard', () => {
  const mockNavigate = vi.fn();
  const mockToken = 'mock-jwt-token';

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();

    vi.mocked(useNavigate).mockReturnValue(mockNavigate);
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: mockToken,
        authenticated: false,
        tokenParsed: {
          preferred_username: 'Test User',
          email: 'test@example.com',
          tenant_id: 'tenant-123',
          realm_access: { roles: ['user'] },
        },
        logout: vi.fn(),
      },
    } as never);
  });

  it('should render dashboard with user information', () => {
    render(<Dashboard />);

    expect(screen.getByRole('heading', { name: /gymnastics platform - user portal/i })).toBeInTheDocument();
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

    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: undefined,
        authenticated: false,
        tokenParsed: undefined,
        logout: vi.fn(),
      },
    } as never);

    render(<Dashboard />);

    expect(screen.getByText('Stored User')).toBeInTheDocument();
    expect(screen.getByText('stored@example.com')).toBeInTheDocument();
    expect(screen.getByText('Email/Password')).toBeInTheDocument();
  });

  it('should display Google OAuth auth method when authenticated via Keycloak', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: mockToken,
        authenticated: true,
        tokenParsed: {
          preferred_username: 'Google User',
          email: 'google@example.com',
          tenant_id: 'tenant-123',
        },
        logout: vi.fn(),
      },
    } as never);

    render(<Dashboard />);

    expect(screen.getByText('Google OAuth')).toBeInTheDocument();
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
      roles: [],
    };

    // Mock for useEffect call on mount
    vi.mocked(apiClient.getCurrentUser).mockResolvedValueOnce(mockApiResponse);
    // Mock for button click call
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
    // Mock for useEffect call on mount
    vi.mocked(apiClient.getCurrentUser).mockResolvedValueOnce({
      userId: 'user-123',
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
      roles: [],
    });
    // Mock for button click call - this one should fail
    vi.mocked(apiClient.getCurrentUser).mockRejectedValueOnce(new Error('Network error'));

    render(<Dashboard />);

    const testButton = screen.getByRole('button', { name: /test api call/i });
    fireEvent.click(testButton);

    await waitFor(() => {
      expect(screen.getByText('Error:')).toBeInTheDocument();
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });
  });

  it('should clear localStorage and redirect when logout is clicked (email/password auth)', () => {
    localStorage.setItem('accessToken', 'token');
    localStorage.setItem('refreshToken', 'refresh');
    localStorage.setItem('user', JSON.stringify({ email: 'test@example.com' }));

    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: undefined,
        authenticated: false,
        logout: vi.fn(),
      },
    } as never);

    const originalLocation = window.location;
    delete (window as { location?: Location }).location;
    window.location = { ...originalLocation, href: '' } as Location;

    render(<Dashboard />);

    const logoutButton = screen.getByRole('button', { name: /logout/i });
    fireEvent.click(logoutButton);

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(localStorage.getItem('user')).toBeNull();
    expect(window.location.href).toBe('/sign-in');

    window.location = originalLocation;
  });

  it('should call Keycloak logout when authenticated via OAuth', () => {
    const mockLogout = vi.fn();

    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: mockToken,
        authenticated: true,
        tokenParsed: {
          preferred_username: 'Test User',
          email: 'test@example.com',
        },
        logout: mockLogout,
      },
    } as never);

    render(<Dashboard />);

    const logoutButton = screen.getByRole('button', { name: /logout/i });
    fireEvent.click(logoutButton);

    expect(mockLogout).toHaveBeenCalledWith({ redirectUri: window.location.origin + '/sign-in' });
  });

  it('should show error when no token is available for API call', async () => {
    localStorage.clear();
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: undefined,
        authenticated: false,
        tokenParsed: undefined,
        logout: vi.fn(),
      },
    } as never);

    render(<Dashboard />);

    const testButton = screen.getByRole('button', { name: /test api call/i });
    fireEvent.click(testButton);

    await waitFor(() => {
      expect(screen.getByText('Error:')).toBeInTheDocument();
      expect(screen.getByText('No authentication token available')).toBeInTheDocument();
    });
  });

  it('should display tenant ID or fallback message', () => {
    render(<Dashboard />);

    expect(screen.getByText('tenant-123')).toBeInTheDocument();
  });

  it('should filter out system roles from display', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: mockToken,
        authenticated: true,
        tokenParsed: {
          preferred_username: 'Test User',
          email: 'test@example.com',
          realm_access: { roles: ['user', 'default-roles-gymnastics', 'uma_authorization', 'custom-role'] },
        },
        logout: vi.fn(),
      },
    } as never);

    render(<Dashboard />);

    expect(screen.getByText('user, custom-role')).toBeInTheDocument();
    expect(screen.queryByText(/default-roles/)).not.toBeInTheDocument();
    expect(screen.queryByText(/uma_/)).not.toBeInTheDocument();
  });
});
