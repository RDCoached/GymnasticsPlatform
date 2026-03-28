import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { OnboardingGuard } from './OnboardingGuard';
import { useNavigate } from 'react-router-dom';
import { useOnboardingStatus } from '../hooks/useOnboardingStatus';

vi.mock('react-router-dom');
vi.mock('../hooks/useOnboardingStatus');

describe('OnboardingGuard', () => {
  const mockNavigate = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useNavigate).mockReturnValue(mockNavigate);
  });

  it('should render children when user is not in onboarding tenant', () => {
    vi.mocked(useOnboardingStatus).mockReturnValue({
      isOnboarding: false,
      isLoading: false,
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
    vi.mocked(useOnboardingStatus).mockReturnValue({
      isOnboarding: true,
      isLoading: false,
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
    vi.mocked(useOnboardingStatus).mockReturnValue({
      isOnboarding: true,
      isLoading: false,
      tenantId: '00000000-0000-0000-0000-000000000001',
    });

    render(
      <OnboardingGuard>
        <div>Protected Content</div>
      </OnboardingGuard>
    );

    expect(mockNavigate).toHaveBeenCalledWith('/onboarding', { replace: true });
  });

  it('should show loading while checking status', () => {
    vi.mocked(useOnboardingStatus).mockReturnValue({
      isOnboarding: false,
      isLoading: true,
      tenantId: undefined,
    });

    render(
      <OnboardingGuard>
        <div>Protected Content</div>
      </OnboardingGuard>
    );

    expect(screen.getByText('Loading...')).toBeInTheDocument();
    expect(screen.queryByText('Protected Content')).not.toBeInTheDocument();
  });
});
