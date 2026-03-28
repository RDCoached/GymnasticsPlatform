import { useState, useEffect } from 'react';

interface OnboardingStatusResponse {
  completed: boolean;
  isOnboardingTenant: boolean;
  tenantId: string;
  onboardingChoice: string | null;
}

interface UseOnboardingStatusResult {
  isOnboarding: boolean;
  tenantId: string | undefined;
  isLoading: boolean;
}

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001';

export function useOnboardingStatus(): UseOnboardingStatusResult {
  const [status, setStatus] = useState<OnboardingStatusResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const accessToken = localStorage.getItem('accessToken');
    const userJson = localStorage.getItem('user');

    if (!accessToken || !userJson) {
      setIsLoading(false);
      return;
    }

    const user = JSON.parse(userJson);

    // If user.onboardingCompleted is false, they're in onboarding
    if (!user.onboardingCompleted) {
      setStatus({
        completed: false,
        isOnboardingTenant: true,
        tenantId: '00000000-0000-0000-0000-000000000001',
        onboardingChoice: null,
      });
      setIsLoading(false);
      return;
    }

    // Otherwise fetch full status from API
    const fetchStatus = async () => {
      try {
        const response = await fetch(`${API_BASE_URL}/api/onboarding/status`, {
          headers: {
            Authorization: `Bearer ${accessToken}`,
          },
        });

        if (response.ok) {
          const data: OnboardingStatusResponse = await response.json();
          setStatus(data);
        }
      } catch (error) {
        console.error('Failed to fetch onboarding status:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchStatus();
  }, []);

  const isOnboarding = status?.isOnboardingTenant ?? false;
  const tenantId = status?.tenantId;

  return {
    isOnboarding,
    tenantId,
    isLoading,
  };
}
