import { useState, useEffect } from 'react';
import { useKeycloak } from '@react-keycloak/web';

interface OnboardingStatusResponse {
  completed: boolean;
  isOnboardingTenant: boolean;
  tenantId: string;
  onboardingChoice: string | null;
}

interface UseOnboardingStatusResult {
  isOnboarding: boolean;
  tenantId: string | undefined;
  isLoading: boolean;
}

export function useOnboardingStatus(): UseOnboardingStatusResult {
  const { keycloak, initialized } = useKeycloak();
  const [status, setStatus] = useState<OnboardingStatusResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!initialized || !keycloak.authenticated) {
      setIsLoading(false);
      return;
    }

    const fetchStatus = async () => {
      try {
        const response = await fetch('http://localhost:5001/api/onboarding/status', {
          headers: {
            Authorization: `Bearer ${keycloak.token}`,
          },
        });

        if (response.ok) {
          const data: OnboardingStatusResponse = await response.json();
          setStatus(data);
        }
      } catch (error) {
        console.error('Failed to fetch onboarding status:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchStatus();
  }, [initialized, keycloak.authenticated, keycloak.token]);

  const isOnboarding = status?.isOnboardingTenant ?? false;
  const tenantId = status?.tenantId;

  return {
    isOnboarding,
    tenantId,
    isLoading,
  };
}
