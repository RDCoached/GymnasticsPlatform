import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { useOnboardingStatus } from './useOnboardingStatus';
import { useKeycloak } from '@react-keycloak/web';

const ONBOARDING_TENANT_ID = '00000000-0000-0000-0000-000000000001';

vi.mock('@react-keycloak/web');

describe('useOnboardingStatus', () => {
  const mockFetch = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    global.fetch = mockFetch;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should return isOnboarding true when API returns onboarding tenant', async () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        authenticated: true,
        token: 'mock-token',
      },
      initialized: true,
    } as never);

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

    await waitFor(() => {
      expect(result.current.isOnboarding).toBe(true);
    });

    expect(result.current.tenantId).toBe(ONBOARDING_TENANT_ID);
    expect(result.current.isLoading).toBe(false);
  });

  it('should return isOnboarding false when API returns different tenant', async () => {
    const differentTenantId = '12345678-1234-1234-1234-123456789012';

    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        authenticated: true,
        token: 'mock-token',
      },
      initialized: true,
    } as never);

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
  });

  it('should return isOnboarding false when API call fails', async () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        authenticated: true,
        token: 'mock-token',
      },
      initialized: true,
    } as never);

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

  it('should return isOnboarding false when not initialized', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        authenticated: false,
      },
      initialized: false,
    } as never);

    const { result } = renderHook(() => useOnboardingStatus());

    expect(result.current.isOnboarding).toBe(false);
    expect(result.current.isLoading).toBe(false);
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it('should return isOnboarding false when not authenticated', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        authenticated: false,
      },
      initialized: true,
    } as never);

    const { result } = renderHook(() => useOnboardingStatus());

    expect(result.current.isOnboarding).toBe(false);
    expect(result.current.isLoading).toBe(false);
    expect(mockFetch).not.toHaveBeenCalled();
  });
});
