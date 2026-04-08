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

        if (response) {
          // Check if user has completed onboarding
          // The tenant_id extension attribute indicates onboarding status
          const tenantId = response.account?.idTokenClaims?.['extension_c6de19aea13c4553917803f900adb147_tenant_id'];

          // If no tenant_id or it's the onboarding tenant, redirect to onboarding
          if (!tenantId || tenantId === '00000000-0000-0000-0000-000000000001') {
            navigate('/onboarding');
          } else {
            // User has completed onboarding, redirect to dashboard
            navigate('/dashboard');
          }
        } else {
          // No response, redirect to sign-in
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
