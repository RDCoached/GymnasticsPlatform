import { useState } from 'react';
import type { FormEvent } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { API_BASE_URL } from '../constants';

interface CreateClubFormProps {
  onComplete: () => void;
}

export function CreateClubForm({ onComplete }: CreateClubFormProps) {
  const [clubName, setClubName] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { getToken } = useAuth();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!clubName.trim()) {
      setError('Club name is required');
      return;
    }

    setIsSubmitting(true);

    try {
      const token = getToken();

      if (!token) {
        throw new Error('Not authenticated');
      }

      const response = await fetch(`${API_BASE_URL}/api/onboarding/create-club`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ name: clubName }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.title || 'Failed to create club');
      }

      const data = await response.json();

      // Store club ID if present
      if (data.clubId) {
        localStorage.setItem('clubId', data.clubId);
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
      setError(err instanceof Error ? err.message : 'Failed to create club');
      setIsSubmitting(false);
    }
  };

  return (
    <div className="form-container-wide">
      <h2>Create Your Club</h2>
      <p>Give your gymnastics club a name to get started.</p>

      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label htmlFor="clubName">Club Name</label>
          <input
            id="clubName"
            type="text"
            value={clubName}
            onChange={(e) => setClubName(e.target.value)}
            placeholder="Enter club name"
            disabled={isSubmitting}
            required
          />
        </div>

        {error && <div className="error-message" role="alert">{error}</div>}

        <button type="submit" disabled={isSubmitting || !clubName.trim()} className="submit-button">
          {isSubmitting ? 'Creating Club...' : 'Create Club'}
        </button>
      </form>
    </div>
  );
}
