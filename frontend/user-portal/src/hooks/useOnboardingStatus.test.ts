import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useOnboardingStatus } from './useOnboardingStatus';
import { useKeycloak } from '@react-keycloak/web';

const ONBOARDING_TENANT_ID = '00000000-0000-0000-0000-000000000001';

vi.mock('@react-keycloak/web');

describe('useOnboardingStatus', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should return isOnboarding true when user is in onboarding tenant', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        tokenParsed: {
          tenant_id: ONBOARDING_TENANT_ID,
        },
      },
      initialized: true,
    } as any);

    const { result } = renderHook(() => useOnboardingStatus());

    expect(result.current.isOnboarding).toBe(true);
  });

  it('should return isOnboarding false when user has a different tenant', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        tokenParsed: {
          tenant_id: '12345678-1234-1234-1234-123456789012',
        },
      },
      initialized: true,
    } as any);

    const { result } = renderHook(() => useOnboardingStatus());

    expect(result.current.isOnboarding).toBe(false);
  });

  it('should return isOnboarding false when tenant_id is not present', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        tokenParsed: {},
      },
      initialized: true,
    } as any);

    const { result } = renderHook(() => useOnboardingStatus());

    expect(result.current.isOnboarding).toBe(false);
  });

  it('should return isOnboarding false when not initialized', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {},
      initialized: false,
    } as any);

    const { result } = renderHook(() => useOnboardingStatus());

    expect(result.current.isOnboarding).toBe(false);
  });
});
