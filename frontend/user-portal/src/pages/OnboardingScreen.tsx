import { useState, useEffect } from 'react';

import { useNavigate } from 'react-router-dom';
import { CreateClubForm } from '../components/CreateClubForm';
import { JoinClubForm } from '../components/JoinClubForm';
import { useAuth } from '../contexts/AuthContext';
import { useOnboardingStatus } from '../hooks/useOnboardingStatus';
import { API_BASE_URL } from '../constants';

type OnboardingMode = 'select' | 'create-club' | 'join-club';

export function OnboardingScreen() {
  const [mode, setMode] = useState<OnboardingMode>('select');
  const navigate = useNavigate();
  const { getToken } = useAuth();
  const { isOnboarding, isLoading } = useOnboardingStatus();

  // Redirect to dashboard if onboarding is already complete
  useEffect(() => {
    if (!isLoading && !isOnboarding) {
      navigate('/dashboard', { replace: true });
    }
  }, [isLoading, isOnboarding, navigate]);

  const handleOnboardingComplete = () => {
    // Navigate to dashboard - middleware will pick up new tenant from database
    navigate('/dashboard');
  };

  const handleIndividualMode = async () => {
    try {
      const token = getToken();

      if (!token) {
        throw new Error('Not authenticated');
      }

      const response = await fetch(`${API_BASE_URL}/api/onboarding/individual`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error('Failed to choose individual mode');
      }

      // Update localStorage to mark onboarding as completed
      const userJson = localStorage.getItem('user');
      if (userJson) {
        const user = JSON.parse(userJson);
        user.onboardingCompleted = true;
        localStorage.setItem('user', JSON.stringify(user));
      }

      // Navigate to dashboard - next request will pick up new tenant
      handleOnboardingComplete();
    } catch (error) {
      console.error('Error choosing individual mode:', error);
      alert('Failed to complete onboarding. Please try again.');
    }
  };

  // Show loading while checking onboarding status
  if (isLoading) {
    return <div className="onboarding-container">Loading...</div>;
  }

  if (mode === 'create-club') {
    return (
      <div className="onboarding-container">
        <button onClick={() => setMode('select')} className="back-button">
          ← Back
        </button>
        <CreateClubForm onComplete={handleOnboardingComplete} />
      </div>
    );
  }

  if (mode === 'join-club') {
    return (
      <div className="onboarding-container">
        <button onClick={() => setMode('select')} className="back-button">
          ← Back
        </button>
        <JoinClubForm onComplete={handleOnboardingComplete} />
      </div>
    );
  }

  return (
    <div className="onboarding-container">
      <header className="onboarding-header">
        <h1>Welcome to Gymnastics Platform!</h1>
        <p>Let's get you set up. Choose how you'd like to use the platform:</p>
      </header>

      <div className="onboarding-options">
        <div className="option-card" onClick={() => setMode('create-club')}>
          <h2>Create a Club</h2>
          <p>Start your own gymnastics club and invite members to join.</p>
          <button className="option-button">Create a New Club</button>
        </div>

        <div className="option-card" onClick={() => setMode('join-club')}>
          <h2>Join a Club</h2>
          <p>Have an invite code? Join an existing gymnastics club.</p>
          <button className="option-button">Join Club</button>
        </div>

        <div className="option-card" onClick={handleIndividualMode}>
          <h2>Individual Mode</h2>
          <p>Use the platform on your own without a club.</p>
          <button className="option-button">Use Individual Mode</button>
        </div>
      </div>
    </div>
  );
}
