import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useOnboardingComplete } from './useOnboardingComplete';
import { useKeycloak } from '@react-keycloak/web';

vi.mock('@react-keycloak/web');

describe('useOnboardingComplete', () => {
  const mockLogout = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockLogout.mockResolvedValue(undefined);
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        logout: mockLogout,
      },
    } as any);
  });

  it('should initialize with isLoading false', () => {
    const { result } = renderHook(() => useOnboardingComplete());

    expect(result.current.isLoading).toBe(false);
    expect(result.current.complete).toBeDefined();
  });

  it('should call keycloak logout with correct redirectUri when complete is called', async () => {
    const { result } = renderHook(() => useOnboardingComplete());

    await act(async () => {
      await result.current.complete();
    });

    expect(mockLogout).toHaveBeenCalledWith({
      redirectUri: window.location.origin,
    });
  });

  it('should set isLoading to true when complete is called', async () => {
    const { result } = renderHook(() => useOnboardingComplete());

    expect(result.current.isLoading).toBe(false);

    // Start the complete process
    act(() => {
      result.current.complete();
    });

    // After starting, isLoading should be true
    await waitFor(() => {
      expect(result.current.isLoading).toBe(true);
    });
  });

  it('should maintain the same complete function reference', () => {
    const { result, rerender } = renderHook(() => useOnboardingComplete());

    const firstComplete = result.current.complete;

    rerender();

    // useCallback should maintain function reference
    expect(result.current.complete).toBe(firstComplete);
  });
});
