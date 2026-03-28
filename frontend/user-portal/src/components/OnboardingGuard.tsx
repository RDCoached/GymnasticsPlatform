import { useEffect } from 'react';
import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useKeycloak } from '@react-keycloak/web';
import { useOnboardingStatus } from '../hooks/useOnboardingStatus';

interface OnboardingGuardProps {
  children: ReactNode;
}

export function OnboardingGuard({ children }: OnboardingGuardProps) {
  const { keycloak, initialized } = useKeycloak();
  const { isOnboarding, isLoading } = useOnboardingStatus();
  const navigate = useNavigate();

  useEffect(() => {
    // Only redirect if user is authenticated and in onboarding tenant
    if (initialized && keycloak.authenticated && !isLoading && isOnboarding) {
      navigate('/onboarding', { replace: true });
    }
  }, [initialized, keycloak.authenticated, isLoading, isOnboarding, navigate]);

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
