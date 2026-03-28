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
    // Logout and redirect back to the app root
    // Keycloak will automatically redirect to login, then back here with new token
    await keycloak.logout({
      redirectUri: window.location.origin,
    });
  }, [keycloak]);

  return { complete, isLoading };
}
