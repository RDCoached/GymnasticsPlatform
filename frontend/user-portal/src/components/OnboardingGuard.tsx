import { useEffect } from 'react';
import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useOnboardingStatus } from '../hooks/useOnboardingStatus';

interface OnboardingGuardProps {
  children: ReactNode;
}

export function OnboardingGuard({ children }: OnboardingGuardProps) {
  const { isOnboarding, isLoading } = useOnboardingStatus();
  const navigate = useNavigate();

  useEffect(() => {
    // Redirect if user needs to complete onboarding
    if (!isLoading && isOnboarding) {
      navigate('/onboarding', { replace: true });
    }
  }, [isLoading, isOnboarding, navigate]);

  // Show loading while checking onboarding status
  if (isLoading) {
    return <div>Loading...</div>;
  }

  // Don't render children if user needs onboarding
  if (isOnboarding) {
    return null;
  }

  return <>{children}</>;
}
