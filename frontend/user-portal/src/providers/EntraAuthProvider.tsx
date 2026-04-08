import { ReactNode, useState, useEffect } from 'react';
import { PublicClientApplication, InteractionRequiredAuthError } from '@azure/msal-browser';
import { MsalProvider, useMsal, useIsAuthenticated } from '@azure/msal-react';
import { AuthContext, User, AuthContextType } from '../contexts/AuthContext';
import { msalConfig, loginRequest, googleLoginRequest, microsoftLoginRequest } from '../msal-config';

const API_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001';

// Create MSAL instance
const msalInstance = new PublicClientApplication(msalConfig);

interface EntraAuthProviderProps {
  children: ReactNode;
}

/**
 * Inner provider that uses MSAL hooks
 * Must be wrapped by MsalProvider
 */
function EntraAuthProviderInner({ children }: EntraAuthProviderProps) {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Fetch user profile from backend when authenticated
  useEffect(() => {
    const fetchUserProfile = async () => {
      if (!isAuthenticated || accounts.length === 0) {
        setUser(null);
        setIsLoading(false);
        return;
      }

      try {
        // Get access token for API calls
        const response = await instance.acquireTokenSilent({
          ...loginRequest,
          account: accounts[0],
        });

        // Fetch user profile from backend
        const profileResponse = await fetch(`${API_URL}/api/auth/me`, {
          headers: {
            'Authorization': `Bearer ${response.accessToken}`,
          },
        });

        if (profileResponse.ok) {
          const data = await profileResponse.json();
          setUser({
            id: data.id,
            email: data.email,
            fullName: data.fullName,
            onboardingCompleted: data.onboardingCompleted,
          });
        }
      } catch (error) {
        console.error('Failed to fetch user profile:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchUserProfile();
  }, [isAuthenticated, accounts, instance]);

  const login = async (email: string, password: string): Promise<void> => {
    // Email/password login via backend API (not MSAL)
    const response = await fetch(`${API_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
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
    try {
      const request = provider === 'google' ? googleLoginRequest : microsoftLoginRequest;

      await instance.loginPopup(request);

      // User profile will be fetched by the useEffect above
    } catch (error) {
      console.error('OAuth login failed:', error);
      throw new Error('OAuth login failed');
    }
  };

  const register = async (email: string, password: string, fullName: string): Promise<void> => {
    const response = await fetch(`${API_URL}/api/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, fullName }),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Registration failed' }));
      throw new Error(error.message || 'Registration failed');
    }
  };

  const logout = async (): Promise<void> => {
    try {
      // Logout from MSAL if authenticated via OAuth
      if (isAuthenticated && accounts.length > 0) {
        await instance.logoutPopup({
          account: accounts[0],
        });
      }

      // Also logout from backend session
      await fetch(`${API_URL}/api/auth/logout`, {
        method: 'POST',
      });
    } catch (error) {
      console.error('Logout error:', error);
    } finally {
      setUser(null);
    }
  };

  const getToken = async (): Promise<string | null> => {
    if (!isAuthenticated || accounts.length === 0) {
      return null;
    }

    try {
      const response = await instance.acquireTokenSilent({
        ...loginRequest,
        account: accounts[0],
      });
      return response.accessToken;
    } catch (error) {
      if (error instanceof InteractionRequiredAuthError) {
        // Fallback to interactive method
        try {
          const response = await instance.acquireTokenPopup(loginRequest);
          return response.accessToken;
        } catch (popupError) {
          console.error('Token acquisition failed:', popupError);
          return null;
        }
      }
      console.error('Token acquisition failed:', error);
      return null;
    }
  };

  const value: AuthContextType = {
    isAuthenticated: isAuthenticated || user !== null,
    isLoading,
    user,
    login,
    loginWithOAuth,
    register,
    logout,
    getToken: () => {
      // Synchronous wrapper - returns null, actual token fetching happens in async calls
      return null;
    },
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/**
 * EntraAuthProvider wraps children with MSAL and auth context
 */
export function EntraAuthProvider({ children }: EntraAuthProviderProps) {
  return (
    <MsalProvider instance={msalInstance}>
      <EntraAuthProviderInner>{children}</EntraAuthProviderInner>
    </MsalProvider>
  );
}
