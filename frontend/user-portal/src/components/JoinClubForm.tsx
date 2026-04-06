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

      // Build headers - include Authorization only if token exists (OAuth)
      const headers: HeadersInit = {
        'Content-Type': 'application/json',
      };
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      const response = await fetch(`${API_BASE_URL}/api/onboarding/join-club`, {
        method: 'POST',
        headers,
        credentials: 'include', // Send session cookie
        body: JSON.stringify({ inviteCode: inviteCode.trim() }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.title || 'Invalid invite code');
      }

      // No localStorage updates - database is source of truth
      // Navigate to dashboard - session will have updated context
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
