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
  const { logout } = useAuth();
  const { isOnboarding, isLoading } = useOnboardingStatus();

  const handleLogout = async () => {
    await logout();
    navigate('/sign-in');
  };

  // Auto-select join-club mode if invite code present
  useEffect(() => {
    const pendingInvite = localStorage.getItem('pendingInviteCode');
    if (pendingInvite && mode === 'select') {
      setMode('join-club');
    }
  }, [mode]);

  // Redirect to dashboard if onboarding is already complete
  useEffect(() => {
    if (!isLoading && !isOnboarding) {
      navigate('/dashboard', { replace: true });
    }
  }, [isLoading, isOnboarding, navigate]);

  const handleOnboardingComplete = () => {
    // Navigate to dashboard - session will have updated tenant context
    navigate('/dashboard', { replace: true });
  };

  const handleIndividualMode = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/onboarding/individual`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include', // Send session cookie
      });

      if (!response.ok) {
        throw new Error('Failed to choose individual mode');
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
    return <div className="container-wide">Loading...</div>;
  }

  if (mode === 'create-club') {
    return (
      <div className="container-wide">
        <header>
          <h1 style={{ marginRight: '1rem', flex: 1 }}>Create Your Club</h1>
          <button onClick={handleLogout} style={{ flexShrink: 0 }}>
            Logout
          </button>
        </header>
        <button onClick={() => setMode('select')} className="back-button">
          ← Back
        </button>
        <CreateClubForm onComplete={handleOnboardingComplete} />
      </div>
    );
  }

  if (mode === 'join-club') {
    return (
      <div className="container-wide">
        <header>
          <h1 style={{ marginRight: '1rem', flex: 1 }}>Join a Club</h1>
          <button onClick={handleLogout} style={{ flexShrink: 0 }}>
            Logout
          </button>
        </header>
        <button onClick={() => setMode('select')} className="back-button">
          ← Back
        </button>
        <JoinClubForm onComplete={handleOnboardingComplete} />
      </div>
    );
  }

  return (
    <div className="container-wide">
      <header>
        <h1 style={{ marginRight: '1rem', flex: 1 }}>Welcome!</h1>
        <button onClick={handleLogout} style={{ flexShrink: 0 }}>
          Logout
        </button>
      </header>

      <div className="onboarding-header" style={{ marginTop: '1rem' }}>
        <p>Let's get you set up. Choose how you'd like to use the platform:</p>
      </div>

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
