import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';

const API_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001';

export function AuthCallbackPage() {
  const navigate = useNavigate();
  const { instance } = useMsal();

  useEffect(() => {
    const handleCallback = async () => {
      try {
        // Let MSAL handle the redirect and exchange the authorization code for tokens
        const response = await instance.handleRedirectPromise();

        if (!response) {
          // No response means this wasn't an OAuth callback
          navigate('/sign-in');
          return;
        }

        // MSAL successfully exchanged the code for tokens
        // Use the access token directly from the response (no need for acquireTokenSilent)
        const accessToken = response.accessToken;

        // Send access token to backend to create session
        const sessionResponse = await fetch(`${API_URL}/api/auth/session`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          credentials: 'include',
          body: JSON.stringify({
            accessToken: accessToken,
          }),
        });

        if (!sessionResponse.ok) {
          const errorData = await sessionResponse.json().catch(() => ({ detail: 'Failed to create session' }));
          throw new Error(errorData.detail || 'Failed to create session');
        }

        // Session created successfully - clear URL parameters
        window.history.replaceState({}, document.title, window.location.pathname);

        // Always redirect to onboarding after OAuth
        // OnboardingScreen will decide whether to redirect to dashboard
        navigate('/onboarding');
      } catch (error) {
        console.error('Authentication callback failed:', error);
        navigate('/sign-in', {
          state: { error: error instanceof Error ? error.message : 'Authentication failed' }
        });
      }
    };

    handleCallback();
  }, [instance, navigate]);

  return (
    <div className="flex items-center justify-center min-h-screen">
      <div className="text-center">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4"></div>
        <p className="text-gray-600">Processing login...</p>
      </div>
    </div>
  );
}
