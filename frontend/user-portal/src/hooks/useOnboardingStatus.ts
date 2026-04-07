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
    // Always fetch from API - database is single source of truth
    const fetchStatus = async () => {
      try {
        const response = await fetch(`${API_BASE_URL}/api/onboarding/status`, {
          credentials: 'include', // Send session cookie
        });

        if (response.ok) {
          const data: OnboardingStatusResponse = await response.json();
          setStatus(data);
        } else if (response.status === 401) {
          // Not authenticated - no onboarding status
          setStatus(null);
        }
      } catch (error) {
        console.error('Failed to fetch onboarding status:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchStatus();
  }, []);

  // Use 'completed' from database as source of truth
  const isOnboarding = status ? !status.completed : false;
  const tenantId = status?.tenantId;

  return {
    isOnboarding,
    tenantId,
    isLoading,
  };
}
