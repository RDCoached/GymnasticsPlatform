import { useState } from 'react';
import type { FormEvent } from 'react';
import { useKeycloak } from '@react-keycloak/web';
import { API_BASE_URL } from '../constants';

interface CreateClubFormProps {
  onComplete: () => void;
}

export function CreateClubForm({ onComplete }: CreateClubFormProps) {
  const [clubName, setClubName] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { keycloak } = useKeycloak();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!clubName.trim()) {
      setError('Club name is required');
      return;
    }

    setIsSubmitting(true);

    try {
      // Get token from localStorage (email/password) or Keycloak (Google OAuth)
      const token = localStorage.getItem('accessToken') || keycloak.token;

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

      // Navigate to dashboard - next request will pick up new tenant from database
      onComplete();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create club');
      setIsSubmitting(false);
    }
  };

  return (
    <div className="form-container">
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

        {error && <div className="error-message">{error}</div>}

        <button type="submit" disabled={isSubmitting} className="submit-button">
          {isSubmitting ? 'Creating Club...' : 'Create Club'}
        </button>
      </form>
    </div>
  );
}
