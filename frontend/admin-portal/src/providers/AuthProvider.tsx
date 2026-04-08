import { ReactNode } from 'react';
import { EntraAuthProvider } from './EntraAuthProvider';

interface AuthProviderProps {
  children: ReactNode;
}

/**
 * Main AuthProvider using Microsoft Entra ID authentication with OAuth support.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  return <EntraAuthProvider>{children}</EntraAuthProvider>;
}
