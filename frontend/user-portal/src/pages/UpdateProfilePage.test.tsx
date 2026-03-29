import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { UpdateProfilePage } from './UpdateProfilePage';
import { useKeycloak } from '@react-keycloak/web';
import { useNavigate } from 'react-router-dom';
import { apiClient } from '../lib/api-client';

vi.mock('@react-keycloak/web');
vi.mock('react-router-dom');
vi.mock('../lib/api-client', () => ({
  apiClient: {
    getProfile: vi.fn(),
    updateProfile: vi.fn(),
  },
}));

describe('UpdateProfilePage', () => {
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
        logout: vi.fn(),
      },
    } as never);
  });

  it('should show loading state initially', () => {
    vi.mocked(apiClient.getProfile).mockReturnValue(new Promise(() => {}) as never);

    render(<UpdateProfilePage />);

    expect(screen.getByText('Loading profile...')).toBeInTheDocument();
  });

  it('should load and display profile data', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('test@example.com')).toBeInTheDocument();
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    });

    expect(apiClient.getProfile).toHaveBeenCalledWith(mockToken);
  });

  it('should display error message when profile load fails', async () => {
    vi.mocked(apiClient.getProfile).mockRejectedValueOnce(new Error('Failed to fetch profile'));

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('Failed to fetch profile')).toBeInTheDocument();
    });
  });

  it('should render form with disabled email field', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);

    render(<UpdateProfilePage />);

    await waitFor(() => {
      const emailInput = screen.getByLabelText(/email/i);
      expect(emailInput).toBeDisabled();
      expect(screen.getByText('Email cannot be changed')).toBeInTheDocument();
    });
  });

  it('should update full name when input changes', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    });

    const fullNameInput = screen.getByLabelText(/full name/i);
    fireEvent.change(fullNameInput, { target: { value: 'Updated Name' } });

    expect(screen.getByDisplayValue('Updated Name')).toBeInTheDocument();
  });

  it('should show validation error when full name is empty', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    });

    const fullNameInput = screen.getByLabelText(/full name/i);
    const submitButton = screen.getByRole('button', { name: /update profile/i });

    fireEvent.change(fullNameInput, { target: { value: '   ' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Full name is required')).toBeInTheDocument();
    });

    expect(apiClient.updateProfile).not.toHaveBeenCalled();
  });

  it('should call API and show success message on successful update', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    const updatedProfile = {
      ...mockProfile,
      fullName: 'Updated Name',
    };

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);
    vi.mocked(apiClient.updateProfile).mockResolvedValueOnce(updatedProfile);

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    });

    const fullNameInput = screen.getByLabelText(/full name/i);
    const submitButton = screen.getByRole('button', { name: /update profile/i });

    fireEvent.change(fullNameInput, { target: { value: 'Updated Name' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Profile updated successfully! Redirecting...')).toBeInTheDocument();
    });

    expect(apiClient.updateProfile).toHaveBeenCalledWith(mockToken, { fullName: 'Updated Name' });
  });

  it('should trim whitespace from full name before submission', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);
    vi.mocked(apiClient.updateProfile).mockResolvedValueOnce({
      ...mockProfile,
      fullName: 'Updated Name',
    });

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    });

    const fullNameInput = screen.getByLabelText(/full name/i);
    const submitButton = screen.getByRole('button', { name: /update profile/i });

    fireEvent.change(fullNameInput, { target: { value: '  Updated Name  ' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(apiClient.updateProfile).toHaveBeenCalledWith(mockToken, { fullName: 'Updated Name' });
    });
  });

  it('should update localStorage after successful profile update', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    const existingUser = { email: 'test@example.com', fullName: 'Old Name' };
    localStorage.setItem('user', JSON.stringify(existingUser));

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);
    vi.mocked(apiClient.updateProfile).mockResolvedValueOnce({
      ...mockProfile,
      fullName: 'Updated Name',
    });

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    });

    const fullNameInput = screen.getByLabelText(/full name/i);
    const submitButton = screen.getByRole('button', { name: /update profile/i });

    fireEvent.change(fullNameInput, { target: { value: 'Updated Name' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      const storedUser = JSON.parse(localStorage.getItem('user') || '{}');
      expect(storedUser.fullName).toBe('Updated Name');
    });
  });

  it('should redirect to dashboard after 2 seconds on success', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);
    vi.mocked(apiClient.updateProfile).mockResolvedValueOnce({
      ...mockProfile,
      fullName: 'Updated Name',
    });

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    }, { timeout: 10000 });

    const fullNameInput = screen.getByLabelText(/full name/i);
    const submitButton = screen.getByRole('button', { name: /update profile/i });

    fireEvent.change(fullNameInput, { target: { value: 'Updated Name' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Profile updated successfully! Redirecting...')).toBeInTheDocument();
    }, { timeout: 10000 });

    // Wait for the setTimeout(2000) to complete
    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/dashboard');
    }, { timeout: 3000 });
  });

  it('should show loading state while submitting', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    let resolveRequest: (value: unknown) => void;
    const requestPromise = new Promise((resolve) => {
      resolveRequest = resolve;
    });

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);
    vi.mocked(apiClient.updateProfile).mockReturnValueOnce(requestPromise as never);

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    });

    const fullNameInput = screen.getByLabelText(/full name/i);
    const submitButton = screen.getByRole('button', { name: /update profile/i });

    fireEvent.change(fullNameInput, { target: { value: 'Updated Name' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /updating\.\.\./i })).toBeInTheDocument();
      expect(fullNameInput).toBeDisabled();
    });

    resolveRequest!(mockProfile);
  });

  it('should display error message when update fails', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);
    vi.mocked(apiClient.updateProfile).mockRejectedValueOnce(new Error('Update failed'));

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    }, { timeout: 10000 });

    const fullNameInput = screen.getByLabelText(/full name/i);
    const submitButton = screen.getByRole('button', { name: /update profile/i });

    fireEvent.change(fullNameInput, { target: { value: 'Updated Name' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Update failed')).toBeInTheDocument();
    }, { timeout: 10000 });
  });

  it('should navigate back to dashboard when back button is clicked', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    }, { timeout: 10000 });

    const backButton = screen.getByRole('button', { name: /back to dashboard/i });
    fireEvent.click(backButton);

    expect(mockNavigate).toHaveBeenCalledWith('/dashboard');
  });

  it('should clear localStorage and redirect when logout is clicked', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    localStorage.setItem('accessToken', 'token');
    localStorage.setItem('refreshToken', 'refresh');
    localStorage.setItem('user', JSON.stringify({ email: 'test@example.com' }));

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);

    const originalLocation = window.location;
    delete (window as { location?: Location }).location;
    window.location = { ...originalLocation, href: '' } as Location;

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
    }, { timeout: 10000 });

    const logoutButton = screen.getByRole('button', { name: /logout/i });
    fireEvent.click(logoutButton);

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
    expect(localStorage.getItem('user')).toBeNull();
    expect(window.location.href).toBe('/sign-in');

    window.location = originalLocation;
  });

  it('should use localStorage token if Keycloak token is not available', async () => {
    const mockProfile = {
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    };

    localStorage.setItem('accessToken', 'stored-token');

    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: undefined,
        authenticated: false,
        logout: vi.fn(),
      },
    } as never);

    vi.mocked(apiClient.getProfile).mockResolvedValueOnce(mockProfile);

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(apiClient.getProfile).toHaveBeenCalledWith('stored-token');
    }, { timeout: 10000 });
  });

  it('should show error when no token is available', async () => {
    localStorage.clear();

    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: undefined,
        authenticated: false,
        logout: vi.fn(),
      },
    } as never);

    vi.mocked(apiClient.getProfile).mockRejectedValueOnce(new Error('Not authenticated'));

    render(<UpdateProfilePage />);

    await waitFor(() => {
      expect(screen.getByText('Not authenticated')).toBeInTheDocument();
    }, { timeout: 10000 });
  });
});
