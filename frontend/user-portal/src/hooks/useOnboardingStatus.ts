import { useKeycloak } from '@react-keycloak/web';
import { ONBOARDING_TENANT_ID } from '../constants';

interface UseOnboardingStatusResult {
  isOnboarding: boolean;
  tenantId: string | undefined;
}

export function useOnboardingStatus(): UseOnboardingStatusResult {
  const { keycloak } = useKeycloak();

  const tenantId = keycloak.tokenParsed?.tenant_id as string | undefined;
  const isOnboarding = tenantId === ONBOARDING_TENANT_ID;

  return {
    isOnboarding,
    tenantId,
  };
}
