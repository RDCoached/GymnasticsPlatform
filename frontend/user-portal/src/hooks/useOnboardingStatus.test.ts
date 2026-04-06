import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { useOnboardingStatus } from './useOnboardingStatus';

const API_BASE_URL = 'http://localhost:5001';
const ONBOARDING_TENANT_ID = '00000000-0000-0000-0000-000000000001';

describe('useOnboardingStatus', () => {
  const mockFetch = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    global.fetch = mockFetch;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should return isOnboarding true when user has not completed onboarding', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        completed: false,
        isOnboardingTenant: true,
        tenantId: ONBOARDING_TENANT_ID,
        onboardingChoice: null,
      }),
    } as Response);

    const { result } = renderHook(() => useOnboardingStatus());

    // Initially loading
    expect(result.current.isLoading).toBe(true);

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    // Should fetch from API with credentials
    expect(mockFetch).toHaveBeenCalledWith(
      `${API_BASE_URL}/api/onboarding/status`,
      {
        credentials: 'include',
      }
    );

    // Should return onboarding status from API
    expect(result.current.isOnboarding).toBe(true);
    expect(result.current.tenantId).toBe(ONBOARDING_TENANT_ID);
  });

  it('should return isOnboarding false when user has completed onboarding', async () => {
    const differentTenantId = '12345678-1234-1234-1234-123456789012';

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
      `${API_BASE_URL}/api/onboarding/status`,
      {
        credentials: 'include',
      }
    );
  });

  it('should handle API call failure gracefully', async () => {
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

  it('should return defaults when not authenticated (401)', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 401,
    } as Response);

    const { result } = renderHook(() => useOnboardingStatus());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.isOnboarding).toBe(false);
    expect(result.current.tenantId).toBeUndefined();
    expect(mockFetch).toHaveBeenCalledOnce();
  });

  it('should handle network errors gracefully', async () => {
    mockFetch.mockRejectedValueOnce(new Error('Network error'));

    const { result } = renderHook(() => useOnboardingStatus());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.isOnboarding).toBe(false);
    expect(result.current.tenantId).toBeUndefined();
  });
});
