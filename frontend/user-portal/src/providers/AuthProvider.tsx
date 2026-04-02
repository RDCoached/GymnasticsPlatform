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
  const isE2EMode = import.meta.env.VITE_E2E_MODE === 'true';

  if (isE2EMode) {
    console.log('[Auth] Running in E2E test mode - using TestAuthProvider');
    return <TestAuthProvider>{children}</TestAuthProvider>;
  }

  console.log('[Auth] Running in production mode - using KeycloakAuthProvider');
  return <KeycloakAuthProvider>{children}</KeycloakAuthProvider>;
}
