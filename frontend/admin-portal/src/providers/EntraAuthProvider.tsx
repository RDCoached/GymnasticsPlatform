import { ReactNode, useState, useEffect } from 'react';
import { AuthContext, User, AuthContextType } from '../contexts/AuthContext';

const API_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001';

interface EntraAuthProviderProps {
  children: ReactNode;
}

/**
 * EntraAuthProvider handles authentication using Microsoft Entra ID
 * with email/password and OAuth (Google) support.
 *
 * This implementation uses session cookies for email/password auth
 * and JWT tokens for OAuth flows.
 */
export function EntraAuthProvider({ children }: EntraAuthProviderProps) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Check for existing session on mount
  useEffect(() => {
    const checkAuth = async () => {
      try {
        const response = await fetch(`${API_URL}/api/auth/me`, {
          credentials: 'include', // Include session cookies
        });

        if (response.ok) {
          const data = await response.json();
          setUser({
            id: data.id,
            email: data.email,
            fullName: data.fullName,
            onboardingCompleted: data.onboardingCompleted,
          });
        }
      } catch (error) {
        console.error('Failed to check authentication:', error);
      } finally {
        setIsLoading(false);
      }
    };

    checkAuth();
  }, []);

  const login = async (email: string, password: string): Promise<void> => {
    const response = await fetch(`${API_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Login failed' }));
      throw new Error(error.message || 'Login failed');
    }

    const data = await response.json();
    setUser({
      id: data.userId || data.user?.id,
      email: data.user?.email || email,
      fullName: data.user?.fullName || 'User',
      onboardingCompleted: data.user?.onboardingCompleted,
    });
  };

  const loginWithOAuth = async (provider: 'google'): Promise<void> => {
    // OAuth flow would be handled by MSAL in full implementation
    // For now, redirect to OAuth endpoint
    const redirectUri = encodeURIComponent(window.location.origin + '/auth/callback');
    window.location.href = `${API_URL}/api/auth/oauth/${provider}?redirect_uri=${redirectUri}`;
  };

  const register = async (email: string, password: string, fullName: string): Promise<void> => {
    const response = await fetch(`${API_URL}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ email, password, fullName }),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Registration failed' }));
      throw new Error(error.message || 'Registration failed');
    }
  };

  const logout = async (): Promise<void> => {
    try {
      await fetch(`${API_URL}/api/auth/logout`, {
        method: 'POST',
        credentials: 'include',
      });
    } catch (error) {
      console.error('Logout error:', error);
    } finally {
      setUser(null);
    }
  };

  const getToken = (): string | null => {
    // For session-based auth, token is in HTTP-only cookie
    // For OAuth, token would be retrieved from MSAL cache
    // Returning null for now as tokens are handled server-side
    return null;
  };

  const value: AuthContextType = {
    isAuthenticated: user !== null,
    isLoading,
    user,
    login,
    loginWithOAuth,
    register,
    logout,
    getToken,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
