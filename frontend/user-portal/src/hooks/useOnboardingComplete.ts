import { useKeycloak } from '@react-keycloak/web';
import { useState, useCallback } from 'react';

interface UseOnboardingCompleteResult {
  complete: () => Promise<void>;
  isLoading: boolean;
}

export function useOnboardingComplete(): UseOnboardingCompleteResult {
  const { keycloak } = useKeycloak();
  const [isLoading, setIsLoading] = useState(false);

  const complete = useCallback(async () => {
    setIsLoading(true);
    // Force re-login to get a new token with updated tenant_id
    // Using login() instead of logout() for seamless re-authentication
    await keycloak.login({
      redirectUri: window.location.origin,
    });
  }, [keycloak]);

  return { complete, isLoading };
}
