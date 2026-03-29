import { useKeycloak } from '@react-keycloak/web';
import { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiClient } from '../lib/api-client';

export function Dashboard() {
  const { keycloak } = useKeycloak();
  const navigate = useNavigate();
  const [apiResponse, setApiResponse] = useState<Record<string, unknown> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Get authentication source (localStorage for email/password, Keycloak for OAuth)
  const authToken = localStorage.getItem('accessToken') || keycloak.token;
  const userFromStorage = useMemo(() => {
    const userJson = localStorage.getItem('user');
    return userJson ? JSON.parse(userJson) : null;
  }, []);

  const testApiCall = async () => {
    setLoading(true);
    setError(null);
    setApiResponse(null);

    try {
      if (!authToken) {
        throw new Error('No authentication token available');
      }

      const data = await apiClient.getCurrentUser(authToken);
      setApiResponse(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    // Clear localStorage for email/password auth
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');

    // Logout from Keycloak for OAuth
    if (keycloak.authenticated) {
      keycloak.logout({ redirectUri: window.location.origin + '/sign-in' });
    } else {
      // Force full page reload to reset app state and redirect to sign-in
      window.location.href = '/sign-in';
    }
  };

  // Get user info from localStorage (email/password) or Keycloak token (OAuth)
  const token = keycloak.tokenParsed;
  const tenantId = token?.tenant_id || 'from-api';
  const username = token?.preferred_username || userFromStorage?.fullName || 'User';
  const email = token?.email || userFromStorage?.email || 'No email';
  const roles = token?.realm_access?.roles || [];

  return (
    <div className="container">
      <header>
        <h1>Gymnastics Platform - User Portal</h1>
        <button onClick={handleLogout}>
          Logout
        </button>
      </header>

      <main>
        <section className="user-info">
          <h2>User Information</h2>
          <dl>
            <dt>Username:</dt>
            <dd>{username}</dd>

            <dt>Email:</dt>
            <dd>{email}</dd>

            <dt>Tenant ID:</dt>
            <dd>{tenantId || <em>No tenant (platform admin)</em>}</dd>

            <dt>Auth Method:</dt>
            <dd>{keycloak.authenticated ? 'Google OAuth' : 'Email/Password'}</dd>

            {roles.length > 0 && (
              <>
                <dt>Roles:</dt>
                <dd>{roles.filter(r => !r.startsWith('default-') && !r.startsWith('uma_')).join(', ')}</dd>
              </>
            )}
          </dl>
        </section>

        <section style={{ marginBottom: '2rem' }}>
          <h2>Quick Actions</h2>
          <div className="onboarding-options" style={{ marginTop: '1.5rem' }}>
            <div className="option-card" onClick={() => navigate('/profile')}>
              <h2>Update My Profile</h2>
              <p>Change your personal information and account settings.</p>
              <button className="option-button">Update Profile</button>
            </div>

            <div className="option-card" style={{ opacity: 0.6, cursor: 'not-allowed' }}>
              <h2>Manage Club</h2>
              <p>View and manage your club settings and members.</p>
              <button className="option-button" disabled>Coming Soon</button>
            </div>

            <div className="option-card" style={{ opacity: 0.6, cursor: 'not-allowed' }}>
              <h2>View Sessions</h2>
              <p>Track your training sessions and progress.</p>
              <button className="option-button" disabled>Coming Soon</button>
            </div>
          </div>
        </section>

        <section className="api-test">
          <h2>API Integration</h2>
          <p>Token available for API calls:</p>
          <code className="token-preview">
            Bearer {authToken?.substring(0, 50)}...
          </code>

          <button onClick={testApiCall} disabled={loading} className="test-button">
            {loading ? 'Calling API...' : 'Test API Call'}
          </button>

          {error && (
            <div className="error">
              <strong>Error:</strong> {error}
            </div>
          )}

          {apiResponse && (
            <div className="api-response">
              <h3>API Response:</h3>
              <pre>{JSON.stringify(apiResponse, null, 2)}</pre>
            </div>
          )}

          <p className="info">
            Click the button above to test calling the authenticated API endpoint.
          </p>
        </section>
      </main>
    </div>
  );
}
