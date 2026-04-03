import { ReactNode } from 'react';
import { KeycloakAuthProvider } from './KeycloakAuthProvider';

interface AuthProviderProps {
  children: ReactNode;
}

/**
 * Main AuthProvider using Keycloak authentication with OAuth support.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  return <KeycloakAuthProvider>{children}</KeycloakAuthProvider>;
}
