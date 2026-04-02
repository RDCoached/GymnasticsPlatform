import { useState, FormEvent, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { apiClient, type ProfileResponse } from '../lib/api-client';

export function UpdateProfilePage() {
  const navigate = useNavigate();
  const { getToken, logout } = useAuth();

  const [profile, setProfile] = useState<ProfileResponse | null>(null);
  const [fullName, setFullName] = useState('');
  const [loading, setLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const fetchProfile = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const token = getToken();

      if (!token) {
        throw new Error('Not authenticated');
      }

      const data = await apiClient.getProfile(token);
      setProfile(data);
      setFullName(data.fullName);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load profile');
    } finally {
      setLoading(false);
    }
  }, [getToken]);

  useEffect(() => {
    fetchProfile();
  }, [fetchProfile]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(false);

    if (!fullName.trim()) {
      setError('Full name is required');
      return;
    }

    setIsSubmitting(true);

    try {
      const token = getToken();

      if (!token) {
        throw new Error('Not authenticated');
      }

      const updatedProfile = await apiClient.updateProfile(token, { fullName: fullName.trim() });

      // Update localStorage with the new data
      const userFromStorage = localStorage.getItem('user');
      if (userFromStorage) {
        const user = JSON.parse(userFromStorage);
        const updatedUser = { ...user, fullName: updatedProfile.fullName };
        localStorage.setItem('user', JSON.stringify(updatedUser));
      }

      setSuccess(true);

      // Redirect back to dashboard after 2 seconds
      setTimeout(() => {
        navigate('/dashboard');
      }, 2000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update profile');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleLogout = async () => {
    await logout();
    navigate('/sign-in');
  };

  if (loading) {
    return (
      <div className="container">
        <header>
          <h1 style={{ marginRight: '1rem', flex: 1 }}>Update Profile</h1>
          <button onClick={handleLogout} style={{ flexShrink: 0 }}>
            Logout
          </button>
        </header>
        <main>
          <div style={{ textAlign: 'center', padding: '2rem' }}>Loading profile...</div>
        </main>
      </div>
    );
  }

  return (
    <div className="container">
      <header>
        <h1 style={{ marginRight: '1rem', flex: 1 }}>Update Profile</h1>
        <button onClick={handleLogout} style={{ flexShrink: 0 }}>
          Logout
        </button>
      </header>

      <main>
        <button onClick={() => navigate('/dashboard')} className="back-button">
          ← Back to Dashboard
        </button>

        <div className="form-container">
          <h2>Your Profile</h2>
          <p>Update your personal information</p>

          {success && (
            <div
              role="status"
              style={{
                background: 'rgba(76, 175, 80, 0.1)',
                border: '1px solid rgba(76, 175, 80, 0.3)',
                color: '#4caf50',
                padding: '0.75rem',
                borderRadius: '8px',
                marginBottom: '1rem',
                textAlign: 'center'
              }}
            >
              Profile updated successfully! Redirecting...
            </div>
          )}

          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label htmlFor="email">Email</label>
              <input
                id="email"
                type="email"
                value={profile?.email || ''}
                disabled
                style={{ opacity: 0.6, cursor: 'not-allowed' }}
              />
              <small style={{ color: 'var(--text)', fontSize: '0.85rem', display: 'block', marginTop: '0.25rem' }}>
                Email cannot be changed
              </small>
            </div>

            <div className="form-group">
              <label htmlFor="fullName">Full Name</label>
              <input
                id="fullName"
                type="text"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                placeholder="Enter your full name"
                disabled={isSubmitting}
                required
                maxLength={100}
              />
            </div>

            {error && <div className="error-message" role="alert">{error}</div>}

            <button type="submit" disabled={isSubmitting || success || !fullName.trim()} className="submit-button">
              {isSubmitting ? 'Updating...' : success ? 'Updated!' : 'Update Profile'}
            </button>
          </form>
        </div>
      </main>
    </div>
  );
}
