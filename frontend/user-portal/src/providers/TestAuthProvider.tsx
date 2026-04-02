import { useState, ReactNode } from 'react';
import { AuthContext, AuthContextType, User } from '../contexts/AuthContext';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5137';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: User | null;
}

/**
 * Test authentication provider for E2E tests.
 * Bypasses Keycloak and authenticates directly against the backend API.
 */
export function TestAuthProvider({ children }: { children: ReactNode }) {
  const [authState, setAuthState] = useState<AuthState>(() => {
    // Check if we have stored auth
    const stored = localStorage.getItem('test_auth');
    if (stored) {
      try {
        return JSON.parse(stored);
      } catch {
        return { accessToken: null, refreshToken: null, user: null };
      }
    }
    return { accessToken: null, refreshToken: null, user: null };
  });
  const [isLoading, setIsLoading] = useState(false);

  const saveAuthState = (state: AuthState) => {
    setAuthState(state);
    if (state.accessToken) {
      localStorage.setItem('test_auth', JSON.stringify(state));
      // Also set in the format other parts of the app expect
      localStorage.setItem('accessToken', state.accessToken);
      localStorage.setItem('refreshToken', state.refreshToken || '');
      localStorage.setItem('user', JSON.stringify({
        ...state.user,
        onboardingCompleted: false // New users need onboarding
      }));
    } else {
      localStorage.removeItem('test_auth');
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
      localStorage.removeItem('user');
    }
  };

  const login = async (email: string, password: string) => {
    setIsLoading(true);
    try {
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
      saveAuthState({
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        user: {
          id: data.user.id,
          email: data.user.email,
          fullName: data.user.fullName,
        },
      });
    } finally {
      setIsLoading(false);
    }
  };

  const register = async (email: string, password: string, fullName: string) => {
    setIsLoading(true);
    try {
      const response = await fetch(`${API_URL}/api/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password, fullName }),
      });

      if (!response.ok) {
        const error = await response.text();
        throw new Error(`Registration failed: ${error}`);
      }
    } finally {
      setIsLoading(false);
    }
  };

  const logout = async () => {
    saveAuthState({ accessToken: null, refreshToken: null, user: null });
  };

  const getToken = () => authState.accessToken;

  const value: AuthContextType = {
    isAuthenticated: !!authState.accessToken,
    isLoading,
    user: authState.user,
    login,
    logout,
    register,
    getToken,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
