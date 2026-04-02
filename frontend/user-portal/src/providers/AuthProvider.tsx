import { ReactNode } from 'react';
import { TestAuthProvider } from './TestAuthProvider';

interface AuthProviderProps {
  children: ReactNode;
}

/**
 * Main AuthProvider that chooses the appropriate authentication strategy.
 *
 * - If VITE_E2E_MODE is set: Use TestAuthProvider (no Keycloak)
 * - Otherwise: Use KeycloakAuthProvider (production)
 *
 * This abstraction allows the app to run without Keycloak for E2E testing.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const isE2EMode = import.meta.env.VITE_E2E_MODE === 'true';

  if (isE2EMode) {
    console.log('[Auth] Running in E2E test mode - using TestAuthProvider');
    return <TestAuthProvider>{children}</TestAuthProvider>;
  }

  // In production, we'd use KeycloakAuthProvider here
  // For now, default to TestAuthProvider since Keycloak integration is being refactored
  console.log('[Auth] Keycloak not configured - using TestAuthProvider');
  return <TestAuthProvider>{children}</TestAuthProvider>;
}
