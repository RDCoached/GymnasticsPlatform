import { useState, useEffect } from 'react';
import type { FormEvent } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { API_BASE_URL } from '../constants';

interface JoinClubFormProps {
  onComplete: () => void;
}

export function JoinClubForm({ onComplete }: JoinClubFormProps) {
  const [inviteCode, setInviteCode] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { getToken } = useAuth();

  // Auto-fill invite code from localStorage
  useEffect(() => {
    const pendingCode = localStorage.getItem('pendingInviteCode');
    if (pendingCode) {
      setInviteCode(pendingCode);
      localStorage.removeItem('pendingInviteCode');
    }
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!inviteCode.trim()) {
      setError('Invite code is required');
      return;
    }

    setIsSubmitting(true);

    try {
      const token = getToken();

      if (!token) {
        throw new Error('Not authenticated');
      }

      const response = await fetch(`${API_BASE_URL}/api/onboarding/join-club`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ inviteCode: inviteCode.trim() }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.title || 'Invalid invite code');
      }

      // Update localStorage to mark onboarding as completed
      const userJson = localStorage.getItem('user');
      if (userJson) {
        const user = JSON.parse(userJson);
        user.onboardingCompleted = true;
        localStorage.setItem('user', JSON.stringify(user));
      }

      // Navigate to dashboard - next request will pick up new tenant from database
      onComplete();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to join club');
      setIsSubmitting(false);
    }
  };

  return (
    <div className="form-container-wide">
      <h2>Join a Club</h2>
      <p>Enter the invite code you received to join a club.</p>

      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label htmlFor="inviteCode">Invite Code</label>
          <input
            id="inviteCode"
            type="text"
            value={inviteCode}
            onChange={(e) => setInviteCode(e.target.value.toUpperCase())}
            placeholder="Enter invite code"
            disabled={isSubmitting}
            required
            maxLength={20}
            autoFocus={!inviteCode}
          />
        </div>

        {error && <div className="error-message" role="alert">{error}</div>}

        <button type="submit" disabled={isSubmitting || !inviteCode.trim()} className="submit-button">
          {isSubmitting ? 'Joining Club...' : 'Join Club'}
        </button>
      </form>
    </div>
  );
}
