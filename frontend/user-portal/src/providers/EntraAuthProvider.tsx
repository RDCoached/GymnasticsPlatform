import { ReactNode, useState, useEffect } from 'react';
import { MsalProvider, useMsal } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import { AuthContext, User, AuthContextType } from '../contexts/AuthContext';
import { msalConfig, loginRequestGoogle, loginRequestMicrosoft } from '../msal-config';

const API_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001';

const msalInstance = new PublicClientApplication(msalConfig);

interface EntraAuthProviderProps {
  children: ReactNode;
}

/**
 * EntraAuthProvider wraps the app with MSAL and provides authentication context.
 * Supports both email/password (via backend API) and OAuth (Google, Microsoft) via MSAL.
 */
export function EntraAuthProvider({ children }: EntraAuthProviderProps) {
  return (
    <MsalProvider instance={msalInstance}>
      <AuthProviderInner>{children}</AuthProviderInner>
    </MsalProvider>
  );
}

function AuthProviderInner({ children }: { children: ReactNode }) {
  const { instance, accounts } = useMsal();
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Initialize MSAL and handle OAuth redirects (MUST run on every page load)
  useEffect(() => {
    const initMsal = async () => {
      try {
        await instance.initialize();
        const response = await instance.handleRedirectPromise();

        // If OAuth redirect just completed, create session
        if (response && response.accessToken) {
          // Extract email and name from ID token (account object has ID token claims)
          const account = response.account;

          // Extract user info from ID token
          const providerUserId = account?.localAccountId || account?.idTokenClaims?.oid || '';
          const email = account?.idTokenClaims?.email || account?.username || '';
          const fullName = account?.name || account?.idTokenClaims?.name || 'User';

          const sessionResponse = await fetch(`${API_URL}/api/auth/session`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({
              accessToken: response.accessToken,
              providerUserId: providerUserId,
              email: email,
              fullName: fullName
            }),
          });

          if (sessionResponse.ok) {
            const userData = await sessionResponse.json();
            setUser({
              id: userData.id,
              email: userData.email,
              fullName: userData.fullName,
              onboardingCompleted: userData.onboardingCompleted,
            });

            // Always redirect to onboarding after OAuth
            // OnboardingScreen will decide whether to redirect to dashboard
            window.location.href = '/onboarding';
          } else {
            console.error('Failed to create session:', await sessionResponse.text());
          }
        }
      } catch (error) {
        console.error('MSAL initialization failed:', error);
      } finally {
        setIsLoading(false);
      }
    };

    initMsal();
  }, [instance]);

  // Check for existing session on mount
  useEffect(() => {
    const checkAuth = async () => {
      try {
        // First check if we have an MSAL account
        if (accounts.length > 0) {
          const account = accounts[0];

          // Extract user info from MSAL account
          const response = await fetch(`${API_URL}/api/auth/me`, {
            credentials: 'include',
          });

          if (response.ok) {
            const data = await response.json();
            setUser({
              id: data.id,
              email: data.email || account.username,
              fullName: data.fullName || account.name || 'User',
              onboardingCompleted: data.onboardingCompleted,
            });
          }
          // If 401, don't set user - OAuth flow will handle session creation
        } else {
          // Check for session-based auth (email/password)
          const response = await fetch(`${API_URL}/api/auth/me`, {
            credentials: 'include',
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
        }
      } catch (error) {
        console.error('Failed to check authentication:', error);
      } finally {
        setIsLoading(false);
      }
    };

    checkAuth();
  }, [accounts]);

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

  const loginWithOAuth = async (provider: 'google' | 'microsoft'): Promise<void> => {
    try {
      // Use MSAL's built-in redirect flow (handles PKCE automatically)
      const loginRequest = provider === 'google' ? loginRequestGoogle : loginRequestMicrosoft;
      await instance.loginRedirect(loginRequest);
    } catch (error) {
      console.error('OAuth login failed:', error);
      throw new Error(`${provider} login failed`);
    }
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
      // Logout from backend
      await fetch(`${API_URL}/api/auth/logout`, {
        method: 'POST',
        credentials: 'include',
      });

      // Logout from MSAL
      if (accounts.length > 0) {
        await instance.logoutRedirect({
          postLogoutRedirectUri: window.location.origin + '/sign-in',
        });
      }
    } catch (error) {
      console.error('Logout error:', error);
    } finally {
      setUser(null);
    }
  };

  const getToken = async (): Promise<string | null> => {
    if (accounts.length > 0) {
      try {
        const response = await instance.acquireTokenSilent({
          scopes: [`api://${import.meta.env.VITE_API_CLIENT_ID}/user.access`],
          account: accounts[0],
        });
        return response.accessToken;
      } catch (error) {
        console.error('Failed to acquire token:', error);
        return null;
      }
    }
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
    getToken: () => getToken().then((token) => token),
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
