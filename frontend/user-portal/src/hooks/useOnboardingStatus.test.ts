import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { useOnboardingStatus } from './useOnboardingStatus';

const ONBOARDING_TENANT_ID = '00000000-0000-0000-0000-000000000001';

describe('useOnboardingStatus', () => {
  const mockFetch = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    global.fetch = mockFetch;
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('should return isOnboarding true when user has not completed onboarding', () => {
    // Setup: User is authenticated but onboarding not completed
    localStorage.setItem('accessToken', 'mock-token');
    localStorage.setItem('user', JSON.stringify({
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: false,
    }));

    const { result } = renderHook(() => useOnboardingStatus());

    // Should immediately return onboarding status from localStorage
    expect(result.current.isOnboarding).toBe(true);
    expect(result.current.tenantId).toBe(ONBOARDING_TENANT_ID);
    expect(result.current.isLoading).toBe(false);

    // Should not call API for incomplete onboarding
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('should fetch from API when user has completed onboarding', async () => {
    const differentTenantId = '12345678-1234-1234-1234-123456789012';

    // Setup: User authenticated with completed onboarding
    localStorage.setItem('accessToken', 'mock-token');
    localStorage.setItem('user', JSON.stringify({
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    }));

    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        completed: true,
        isOnboardingTenant: false,
        tenantId: differentTenantId,
        onboardingChoice: 'individual',
      }),
    } as Response);

    const { result } = renderHook(() => useOnboardingStatus());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.isOnboarding).toBe(false);
    expect(result.current.tenantId).toBe(differentTenantId);
    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/onboarding/status'),
      expect.objectContaining({
        headers: {
          Authorization: 'Bearer mock-token',
        },
      })
    );
  });

  it('should handle API call failure gracefully', async () => {
    localStorage.setItem('accessToken', 'mock-token');
    localStorage.setItem('user', JSON.stringify({
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true,
    }));

    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 500,
    } as Response);

    const { result } = renderHook(() => useOnboardingStatus());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.isOnboarding).toBe(false);
    expect(result.current.tenantId).toBeUndefined();
  });

  it('should return defaults when not authenticated (no token)', () => {
    // No localStorage setup - user not authenticated

    const { result } = renderHook(() => useOnboardingStatus());

    expect(result.current.isOnboarding).toBe(false);
    expect(result.current.isLoading).toBe(false);
    expect(result.current.tenantId).toBeUndefined();
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('should return defaults when user data is missing', () => {
    // Token exists but no user data
    localStorage.setItem('accessToken', 'mock-token');

    const { result } = renderHook(() => useOnboardingStatus());

    expect(result.current.isOnboarding).toBe(false);
    expect(result.current.isLoading).toBe(false);
    expect(result.current.tenantId).toBeUndefined();
    expect(mockFetch).not.toHaveBeenCalled();
  });
});
