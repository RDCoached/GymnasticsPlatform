import { useState } from 'react';
import { CreateClubForm } from '../components/CreateClubForm';
import { JoinClubForm } from '../components/JoinClubForm';
import { useOnboardingComplete } from '../hooks/useOnboardingComplete';
import { useKeycloak } from '@react-keycloak/web';
import { API_BASE_URL } from '../constants';

type OnboardingMode = 'select' | 'create-club' | 'join-club';

export function OnboardingScreen() {
  const [mode, setMode] = useState<OnboardingMode>('select');
  const { complete, isLoading } = useOnboardingComplete();
  const { keycloak } = useKeycloak();

  const handleIndividualMode = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/onboarding/individual`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${keycloak.token}`,
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error('Failed to choose individual mode');
      }

      // Trigger automatic re-authentication
      await complete();
    } catch (error) {
      console.error('Error choosing individual mode:', error);
      alert('Failed to complete onboarding. Please try again.');
    }
  };

  if (isLoading) {
    return (
      <div className="onboarding-container">
        <div className="loading-message">
          <h2>Setting up your account...</h2>
          <p>Please wait while we complete your setup.</p>
        </div>
      </div>
    );
  }

  if (mode === 'create-club') {
    return (
      <div className="onboarding-container">
        <button onClick={() => setMode('select')} className="back-button">
          ← Back
        </button>
        <CreateClubForm onComplete={complete} />
      </div>
    );
  }

  if (mode === 'join-club') {
    return (
      <div className="onboarding-container">
        <button onClick={() => setMode('select')} className="back-button">
          ← Back
        </button>
        <JoinClubForm onComplete={complete} />
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
          <button className="option-button">Create Club</button>
        </div>

        <div className="option-card" onClick={() => setMode('join-club')}>
          <h2>Join a Club</h2>
          <p>Have an invite code? Join an existing gymnastics club.</p>
          <button className="option-button">Join Club</button>
        </div>

        <div className="option-card" onClick={handleIndividualMode}>
          <h2>Individual Mode</h2>
          <p>Use the platform on your own without a club.</p>
          <button className="option-button">Go Individual</button>
        </div>
      </div>
    </div>
  );
}
