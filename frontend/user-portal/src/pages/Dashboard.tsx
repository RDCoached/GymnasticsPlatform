import { useAuth } from '../contexts/AuthContext';
import { useState, useMemo, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiClient, type CurrentUserResponse } from '../lib/api-client';

/**
 * Dashboard component displays user information and quick actions
 */
export function Dashboard() {
  const { getToken, logout, user } = useAuth();
  const navigate = useNavigate();
  const [apiResponse, setApiResponse] = useState<Record<string, unknown> | null>(null);
  const [currentUser, setCurrentUser] = useState<CurrentUserResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showApiSection, setShowApiSection] = useState(false);

  const authToken = getToken();

  // Fetch current user with roles from API (database is source of truth)
  const fetchCurrentUser = useCallback(async () => {
    try {
      const user = await apiClient.getCurrentUser(authToken);
      setCurrentUser(user);
    } catch (err) {
      console.error('Failed to fetch current user:', err);
    }
  }, [authToken]);

  useEffect(() => {
    fetchCurrentUser();
  }, [fetchCurrentUser]);

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

  const handleLogout = async () => {
    await logout();
    navigate('/sign-in');
  };

  const tenantId = currentUser?.tenantId || 'from-api';
  const username = currentUser?.name || user?.fullName || 'User';
  const email = currentUser?.email || user?.email || 'No email';
  const roles: string[] = [];

  return (
    <div className="container-wide">
      <header>
        <h1>Dashboard</h1>
        <button onClick={handleLogout}>
          Logout
        </button>
      </header>

      <main>
        <section className="user-info">
          <h2>User Information</h2>
          <div className="user-info-grid">
            <div className="info-item">
              <dt>Username:</dt>
              <dd>{username}</dd>
            </div>

            <div className="info-item">
              <dt>Email:</dt>
              <dd>{email}</dd>
            </div>

            <div className="info-item">
              <dt>Tenant ID:</dt>
              <dd>{tenantId || <em>No tenant (platform admin)</em>}</dd>
            </div>

            <div className="info-item">
              <dt>Auth Method:</dt>
              <dd>Email/Password</dd>
            </div>

            {roles.length > 0 && (
              <div className="info-item">
                <dt>Roles (Keycloak):</dt>
                <dd>{roles.filter(r => !r.startsWith('default-') && !r.startsWith('uma_')).join(', ')}</dd>
              </div>
            )}

            {currentUser && currentUser.roles.length > 0 && (
              <div className="info-item">
                <dt>App Roles:</dt>
                <dd>{currentUser.roles.join(', ')}</dd>
              </div>
            )}
          </div>
        </section>

        <section style={{ marginBottom: '2rem' }}>
          <h2>Quick Actions</h2>
          <div className="onboarding-options" style={{ marginTop: '1.5rem' }}>
            <div className="option-card" onClick={() => navigate('/profile')}>
              <h2>Update My Profile</h2>
              <p>Change your personal information and account settings.</p>
              <button className="option-button">Update Profile</button>
            </div>

            {currentUser && currentUser.roles.includes('ClubAdmin') && currentUser.clubId ? (
              <div className="option-card" onClick={() => navigate(`/club/invites?clubId=${currentUser.clubId}`)}>
                <h2>Manage Club</h2>
                <p>View and manage your club invites and members.</p>
                <button className="option-button">Manage Club</button>
              </div>
            ) : (
              <div className="option-card" style={{ opacity: 0.6, cursor: 'not-allowed' }}>
                <h2>Manage Club</h2>
                <p>View and manage your club settings and members.</p>
                <button className="option-button" disabled>Coming Soon</button>
              </div>
            )}

            {currentUser && (currentUser.roles.includes('Coach') || currentUser.roles.includes('ClubAdmin') || currentUser.roles.includes('IndividualAdmin')) ? (
              <div className="option-card" onClick={() => navigate('/gymnasts')}>
                <h2>Manage Gymnasts</h2>
                <p>Add, edit, and manage your gymnasts.</p>
                <button className="option-button">Manage Gymnasts</button>
              </div>
            ) : (
              <div className="option-card" style={{ opacity: 0.6, cursor: 'not-allowed' }}>
                <h2>Manage Gymnasts</h2>
                <p>Add, edit, and manage your gymnasts.</p>
                <button className="option-button" disabled>Coach Access Only</button>
              </div>
            )}

            {currentUser && (currentUser.roles.includes('Coach') || currentUser.roles.includes('ClubAdmin') || currentUser.roles.includes('IndividualAdmin')) ? (
              <div className="option-card" onClick={() => navigate('/programme-builder')}>
                <h2>🤖 Build Programme</h2>
                <p>Create AI-powered training programmes with RAG assistance.</p>
                <button className="option-button">Build Programme</button>
              </div>
            ) : (
              <div className="option-card" style={{ opacity: 0.6, cursor: 'not-allowed' }}>
                <h2>🤖 Build Programme</h2>
                <p>Create AI-powered training programmes.</p>
                <button className="option-button" disabled>Coach Access Only</button>
              </div>
            )}

            <div className="option-card" style={{ opacity: 0.6, cursor: 'not-allowed' }}>
              <h2>View Sessions</h2>
              <p>Track your training sessions and progress.</p>
              <button className="option-button" disabled>Coming Soon</button>
            </div>
          </div>
        </section>

        <section className="api-test">
          <div
            className="collapsible-header"
            onClick={() => setShowApiSection(!showApiSection)}
          >
            <h2>API Integration</h2>
            <span>{showApiSection ? '▼' : '▶'}</span>
          </div>

          <div className={`collapsible-content ${showApiSection ? '' : 'collapsed'}`}>
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
          </div>
        </section>
      </main>
    </div>
  );
}
