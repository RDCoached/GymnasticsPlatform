import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { SyncUsersPage } from './SyncUsersPage';
import { useKeycloak } from '@react-keycloak/web';
import { BrowserRouter } from 'react-router-dom';
import { adminApiClient } from '../lib/api-client';

vi.mock('@react-keycloak/web');
vi.mock('../lib/api-client', () => ({
  adminApiClient: {
    listUsers: vi.fn(),
    syncUserTenant: vi.fn(),
  },
}));

const renderWithRouter = (component: React.ReactElement) => {
  return render(<BrowserRouter>{component}</BrowserRouter>);
};

describe('SyncUsersPage', () => {
  const mockToken = 'mock-admin-token';
  const mockLogout = vi.fn();

  const mockUsers = [
    {
      id: '1',
      keycloakUserId: 'keycloak-user-1',
      email: 'user1@example.com',
      fullName: 'User One',
      tenantId: 'tenant-1',
      onboardingCompleted: true,
      onboardingChoice: 'club',
    },
    {
      id: '2',
      keycloakUserId: 'keycloak-user-2',
      email: 'user2@example.com',
      fullName: 'User Two',
      tenantId: 'tenant-2',
      onboardingCompleted: false,
      onboardingChoice: null,
    },
    {
      id: '3',
      keycloakUserId: 'keycloak-user-3',
      email: 'user3@example.com',
      fullName: 'User Three',
      tenantId: 'tenant-3',
      onboardingCompleted: true,
      onboardingChoice: 'individual',
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: mockToken,
        logout: mockLogout,
      },
    } as never);
  });

  it('should show loading state initially', () => {
    vi.mocked(adminApiClient.listUsers).mockReturnValue(new Promise(() => {}) as never);

    renderWithRouter(<SyncUsersPage />);

    expect(screen.getByText('Loading users...')).toBeInTheDocument();
  });

  it('should load and display users', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
      expect(screen.getByText('User One')).toBeInTheDocument();
      expect(screen.getByText('user2@example.com')).toBeInTheDocument();
      expect(screen.getByText('User Two')).toBeInTheDocument();
      expect(screen.getByText('user3@example.com')).toBeInTheDocument();
      expect(screen.getByText('User Three')).toBeInTheDocument();
    });

    expect(adminApiClient.listUsers).toHaveBeenCalledWith(mockToken);
  });

  it('should display user count', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('Users (3)')).toBeInTheDocument();
    });
  });

  it('should display onboarding status and choice', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('club')).toBeInTheDocument();
      expect(screen.getByText('individual')).toBeInTheDocument();
    });
  });

  it('should display error message when fetching users fails', async () => {
    vi.mocked(adminApiClient.listUsers).mockRejectedValueOnce(new Error('Failed to fetch users'));

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('Error:')).toBeInTheDocument();
      expect(screen.getByText('Failed to fetch users')).toBeInTheDocument();
    });
  });

  it('should show "No users found" when user list is empty', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce([]);

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('No users found')).toBeInTheDocument();
    });
  });

  it('should sync individual user when sync button is clicked', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);
    vi.mocked(adminApiClient.syncUserTenant).mockResolvedValueOnce({
      userId: 'keycloak-user-1',
      email: 'user1@example.com',
      tenantId: 'tenant-1',
      message: 'User synced successfully',
    });

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const syncButtons = screen.getAllByRole('button', { name: /^sync$/i });
    fireEvent.click(syncButtons[0]);

    await waitFor(() => {
      expect(adminApiClient.syncUserTenant).toHaveBeenCalledWith(mockToken, 'keycloak-user-1');
    });

    await waitFor(() => {
      expect(screen.getByText('✓ Synced')).toBeInTheDocument();
    });
  });

  it('should show loading state while syncing user', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);

    let resolveRequest: () => void;
    const requestPromise = new Promise((resolve) => {
      resolveRequest = () => resolve({
        userId: 'keycloak-user-1',
        email: 'user1@example.com',
        tenantId: 'tenant-1',
        message: 'User synced successfully',
      });
    });

    vi.mocked(adminApiClient.syncUserTenant).mockReturnValueOnce(requestPromise as never);

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const syncButtons = screen.getAllByRole('button', { name: /^sync$/i });
    fireEvent.click(syncButtons[0]);

    await waitFor(() => {
      const syncingButtons = screen.getAllByRole('button', { name: /syncing\.\.\./i });
      expect(syncingButtons.length).toBeGreaterThan(0);
      expect(syncingButtons[syncingButtons.length - 1]).toBeInTheDocument();
    });

    resolveRequest!();
  });

  it('should display error message when sync fails', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);
    vi.mocked(adminApiClient.syncUserTenant).mockRejectedValueOnce(new Error('Sync failed'));

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const syncButtons = screen.getAllByRole('button', { name: /^sync$/i });
    fireEvent.click(syncButtons[0]);

    await waitFor(() => {
      expect(screen.getByText('✗ Failed')).toBeInTheDocument();
    });
  });

  it('should sync all users when sync all button is clicked', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);
    vi.mocked(adminApiClient.syncUserTenant)
      .mockResolvedValueOnce({
        userId: 'keycloak-user-1',
        email: 'user1@example.com',
        tenantId: 'tenant-1',
        message: 'User synced successfully',
      })
      .mockResolvedValueOnce({
        userId: 'keycloak-user-2',
        email: 'user2@example.com',
        tenantId: 'tenant-2',
        message: 'User synced successfully',
      })
      .mockResolvedValueOnce({
        userId: 'keycloak-user-3',
        email: 'user3@example.com',
        tenantId: 'tenant-3',
        message: 'User synced successfully',
      });

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const syncAllButton = screen.getByRole('button', { name: /sync all users/i });
    fireEvent.click(syncAllButton);

    await waitFor(() => {
      expect(adminApiClient.syncUserTenant).toHaveBeenCalledTimes(3);
      expect(adminApiClient.syncUserTenant).toHaveBeenCalledWith(mockToken, 'keycloak-user-1');
      expect(adminApiClient.syncUserTenant).toHaveBeenCalledWith(mockToken, 'keycloak-user-2');
      expect(adminApiClient.syncUserTenant).toHaveBeenCalledWith(mockToken, 'keycloak-user-3');
    });
  });

  it('should disable sync all button while syncing', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);

    let resolveRequest: () => void;
    const requestPromise = new Promise((resolve) => {
      resolveRequest = () => resolve({
        userId: 'keycloak-user-1',
        email: 'user1@example.com',
        tenantId: 'tenant-1',
        message: 'User synced successfully',
      });
    });

    vi.mocked(adminApiClient.syncUserTenant).mockReturnValue(requestPromise as never);

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const syncAllButton = screen.getByRole('button', { name: /sync all users/i });
    fireEvent.click(syncAllButton);

    await waitFor(() => {
      const syncingButtons = screen.getAllByRole('button', { name: /^syncing\.\.\.$/i });
      // The first button should be the "Sync All" button that changed to "Syncing..."
      expect(syncingButtons[0]).toBeInTheDocument();
      expect(syncingButtons[0]).toBeDisabled();
    });

    resolveRequest!();
  });

  it('should refresh user list when refresh button is clicked', async () => {
    vi.mocked(adminApiClient.listUsers)
      .mockResolvedValueOnce(mockUsers)
      .mockResolvedValueOnce([...mockUsers, {
        id: '4',
        keycloakUserId: 'keycloak-user-4',
        email: 'user4@example.com',
        fullName: 'User Four',
        tenantId: 'tenant-4',
        onboardingCompleted: true,
        onboardingChoice: 'club',
      }]);

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('Users (3)')).toBeInTheDocument();
    });

    const refreshButton = screen.getByRole('button', { name: /refresh list/i });
    fireEvent.click(refreshButton);

    await waitFor(() => {
      expect(screen.getByText('Users (4)')).toBeInTheDocument();
      expect(screen.getByText('user4@example.com')).toBeInTheDocument();
    });

    expect(adminApiClient.listUsers).toHaveBeenCalledTimes(2);
  });

  it('should render navigation link to home', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      const backLink = screen.getByText('← Back to Home');
      expect(backLink).toBeInTheDocument();
      expect(backLink).toHaveAttribute('href', '/');
    });
  });

  it('should call Keycloak logout when logout button is clicked', async () => {
    vi.mocked(adminApiClient.listUsers).mockResolvedValueOnce(mockUsers);

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('user1@example.com')).toBeInTheDocument();
    });

    const logoutButton = screen.getByRole('button', { name: /logout/i });
    fireEvent.click(logoutButton);

    expect(mockLogout).toHaveBeenCalled();
  });

  it('should show error when not authenticated', async () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: undefined,
        logout: mockLogout,
      },
    } as never);

    vi.mocked(adminApiClient.listUsers).mockRejectedValueOnce(new Error('Not authenticated'));

    renderWithRouter(<SyncUsersPage />);

    await waitFor(() => {
      expect(screen.getByText('Not authenticated')).toBeInTheDocument();
    });
  });

});
