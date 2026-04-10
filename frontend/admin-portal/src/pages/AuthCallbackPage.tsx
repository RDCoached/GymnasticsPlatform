import { useEffect } from 'react';
import { useMsal } from '@azure/msal-react';
import { useNavigate } from 'react-router-dom';

export function AuthCallbackPage() {
  const { instance } = useMsal();
  const navigate = useNavigate();

  useEffect(() => {
    const handleCallback = async () => {
      try {
        const response = await instance.handleRedirectPromise();

        if (response && response.account) {
          // Successfully authenticated via OAuth
          const account = response.account;

          // Check if user has completed onboarding
          const tenantId = account.idTokenClaims?.extension_tenant_id;

          if (!tenantId || tenantId === '00000000-0000-0000-0000-000000000001') {
            // User needs to complete onboarding
            navigate('/onboarding');
          } else {
            // User is fully onboarded
            navigate('/dashboard');
          }
        } else {
          // No response - redirect to sign in
          navigate('/sign-in');
        }
      } catch (error) {
        console.error('Authentication callback failed:', error);
        navigate('/sign-in', { state: { error: 'Authentication failed' } });
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
