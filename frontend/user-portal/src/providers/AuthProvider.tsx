import { ReactNode } from 'react';
import { TestAuthProvider } from './TestAuthProvider';
import { KeycloakAuthProvider } from './KeycloakAuthProvider';

interface AuthProviderProps {
  children: ReactNode;
}

/**
 * Main AuthProvider that chooses the appropriate authentication strategy.
 *
 * - If VITE_E2E_MODE is set: Use TestAuthProvider (no Keycloak)
 * - Otherwise: Use KeycloakAuthProvider (production with OAuth support)
 *
 * This abstraction allows the app to run without Keycloak for E2E testing.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const e2eEnvVar = import.meta.env.VITE_E2E_MODE;
  const isE2EMode = e2eEnvVar === 'true';

  console.log('[Auth] VITE_E2E_MODE:', e2eEnvVar, 'isE2EMode:', isE2EMode);

  if (isE2EMode) {
    console.log('[Auth] Running in E2E test mode - using TestAuthProvider');
    return <TestAuthProvider>{children}</TestAuthProvider>;
  }

  console.log('[Auth] Running in production mode - using KeycloakAuthProvider');
  return <KeycloakAuthProvider>{children}</KeycloakAuthProvider>;
}
