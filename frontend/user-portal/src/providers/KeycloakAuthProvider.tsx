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
    const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5137';

    const response = await fetch(`${API_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(`Login failed: ${error}`);
    }

    const data = await response.json();

    // Store tokens in localStorage
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    localStorage.setItem('user', JSON.stringify({
      id: data.user.id,
      email: data.user.email,
      fullName: data.user.fullName,
      onboardingCompleted: false,
    }));

    setUser({
      id: data.user.id,
      email: data.user.email,
      fullName: data.user.fullName,
    });
  };

  const loginWithOAuth = async (provider: 'google') => {
    await keycloak.login({ idpHint: provider });
  };

  const logout = async () => {
    // Clear localStorage for email/password auth
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
    localStorage.removeItem('clubId');

    // Logout from Keycloak if authenticated
    if (keycloak.authenticated) {
      await keycloak.logout({
        redirectUri: window.location.origin + '/sign-in',
      });
    }

    setUser(null);
  };

  const register = async (email: string, password: string, fullName: string) => {
    const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5137';

    const response = await fetch(`${API_URL}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, fullName }),
    });

    if (!response.ok) {
      const error = await response.text();
      throw new Error(`Registration failed: ${error}`);
    }
  };

  const getToken = () => {
    // Prefer Keycloak token if authenticated via OAuth
    if (keycloak.authenticated && keycloak.token) {
      return keycloak.token;
    }
    // Fall back to localStorage token for email/password auth
    return localStorage.getItem('accessToken');
  };

  const value: AuthContextType = {
    isAuthenticated: keycloak.authenticated || !!localStorage.getItem('accessToken'),
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
