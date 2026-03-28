import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { OnboardingGuard } from './OnboardingGuard';
import { useKeycloak } from '@react-keycloak/web';
import { useNavigate } from 'react-router-dom';
import { useOnboardingStatus } from '../hooks/useOnboardingStatus';

vi.mock('@react-keycloak/web');
vi.mock('react-router-dom');
vi.mock('../hooks/useOnboardingStatus');

describe('OnboardingGuard', () => {
  const mockNavigate = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useNavigate).mockReturnValue(mockNavigate);
  });

  it('should render children when user is not in onboarding tenant', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: { authenticated: true },
      initialized: true,
    } as any);

    vi.mocked(useOnboardingStatus).mockReturnValue({
      isOnboarding: false,
      tenantId: '12345678-1234-1234-1234-123456789012',
    });

    render(
      <OnboardingGuard>
        <div>Protected Content</div>
      </OnboardingGuard>
    );

    expect(screen.getByText('Protected Content')).toBeInTheDocument();
    expect(mockNavigate).not.toHaveBeenCalled();
  });

  it('should not render children when user is in onboarding tenant', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: { authenticated: true },
      initialized: true,
    } as any);

    vi.mocked(useOnboardingStatus).mockReturnValue({
      isOnboarding: true,
      tenantId: '00000000-0000-0000-0000-000000000001',
    });

    render(
      <OnboardingGuard>
        <div>Protected Content</div>
      </OnboardingGuard>
    );

    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
  });

  it('should navigate to /onboarding when user is in onboarding tenant', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: { authenticated: true },
      initialized: true,
    } as any);

    vi.mocked(useOnboardingStatus).mockReturnValue({
      isOnboarding: true,
      tenantId: '00000000-0000-0000-0000-000000000001',
    });

    render(
      <OnboardingGuard>
        <div>Protected Content</div>
      </OnboardingGuard>
    );

    expect(mockNavigate).toHaveBeenCalledWith('/onboarding', { replace: true });
  });

  it('should not navigate when not initialized', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: { authenticated: false },
      initialized: false,
    } as any);

    vi.mocked(useOnboardingStatus).mockReturnValue({
      isOnboarding: true,
      tenantId: '00000000-0000-0000-0000-000000000001',
    });

    render(
      <OnboardingGuard>
        <div>Protected Content</div>
      </OnboardingGuard>
    );

    expect(mockNavigate).not.toHaveBeenCalled();
  });

  it('should not navigate when not authenticated', () => {
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: { authenticated: false },
      initialized: true,
    } as any);

    vi.mocked(useOnboardingStatus).mockReturnValue({
      isOnboarding: true,
      tenantId: '00000000-0000-0000-0000-000000000001',
    });

    render(
      <OnboardingGuard>
        <div>Protected Content</div>
      </OnboardingGuard>
    );

    expect(mockNavigate).not.toHaveBeenCalled();
  });
});
