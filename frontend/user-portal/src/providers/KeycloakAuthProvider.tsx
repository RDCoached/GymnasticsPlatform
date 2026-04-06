import { ReactNode, useEffect, useState } from 'react';
import { ReactKeycloakProvider, useKeycloak } from '@react-keycloak/web';
import Keycloak from 'keycloak-js';
import { AuthContext, AuthContextType, User } from '../contexts/AuthContext';

const keycloakConfig = {
  url: import.meta.env.VITE_KEYCLOAK_URL || 'http://localhost:8080',
  realm: import.meta.env.VITE_KEYCLOAK_REALM || 'gymnastics',
  clientId: import.meta.env.VITE_KEYCLOAK_CLIENT_ID || 'user-portal',
};

const keycloak = new Keycloak(keycloakConfig);

interface KeycloakAuthProviderProps {
  children: ReactNode;
}

function KeycloakAuthContent({ children }: KeycloakAuthProviderProps) {
  const { keycloak, initialized } = useKeycloak();
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    if (initialized && keycloak.authenticated && keycloak.tokenParsed) {
      setUser({
        id: keycloak.tokenParsed.sub || '',
        email: keycloak.tokenParsed.email || '',
        fullName: keycloak.tokenParsed.name || keycloak.tokenParsed.preferred_username || '',
      });
    } else {
      setUser(null);
    }
  }, [initialized, keycloak.authenticated, keycloak.tokenParsed]);

  const login = async (email: string, password: string) => {
    // For email/password login, we call the backend API directly
    // This is handled by the backend /api/auth/login endpoint
    const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5001';

    const response = await fetch(`${API_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include', // Send/receive cookies
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(`Login failed: ${error}`);
    }

    const data = await response.json();

    // No token storage - cookie is set automatically by server
    setUser({
      id: data.user.id || '',
      email: data.user.email,
      fullName: data.user.fullName,
    });
  };

  const loginWithOAuth = async (provider: 'google') => {
    await keycloak.login({ idpHint: provider });
  };

  const logout = async () => {
    const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5001';

    // Call backend logout to clear session
    try {
      await fetch(`${API_URL}/api/auth/logout`, {
        method: 'POST',
        credentials: 'include', // Send cookie
      });
    } catch (error) {
      console.error('Logout error:', error);
    }

    // Logout from Keycloak if authenticated via OAuth
    if (keycloak.authenticated) {
      await keycloak.logout({
        redirectUri: window.location.origin + '/sign-in',
      });
    }

    setUser(null);
  };

  const register = async (email: string, password: string, fullName: string) => {
    const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5001';

    const response = await fetch(`${API_URL}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include', // Send/receive cookies
      body: JSON.stringify({ email, password, fullName }),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(`Registration failed: ${error}`);
    }
  };

  const getToken = () => {
    // Only return Keycloak token for OAuth authentication
    // Email/password auth uses HTTP-only cookies (no token needed in JS)
    if (keycloak.authenticated && keycloak.token) {
      return keycloak.token;
    }
    return null;
  };

  const value: AuthContextType = {
    isAuthenticated: keycloak.authenticated || !!user,
    isLoading: !initialized,
    user,
    login,
    loginWithOAuth,
    logout,
    register,
    getToken,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function KeycloakAuthProvider({ children }: KeycloakAuthProviderProps) {
  return (
    <ReactKeycloakProvider authClient={keycloak}>
      <KeycloakAuthContent>{children}</KeycloakAuthContent>
    </ReactKeycloakProvider>
  );
}
