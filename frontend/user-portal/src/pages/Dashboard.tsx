import { useKeycloak } from '@react-keycloak/web';
import { useState } from 'react';

export function Dashboard() {
  const { keycloak } = useKeycloak();
  const [apiResponse, setApiResponse] = useState<Record<string, unknown> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const testApiCall = async () => {
    setLoading(true);
    setError(null);
    setApiResponse(null);

    try {
      const response = await fetch('http://localhost:5001/api/auth/me', {
        headers: {
          'Authorization': `Bearer ${keycloak.token}`,
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error(`API returned ${response.status}: ${response.statusText}`);
      }

      const data = await response.json();
      setApiResponse(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  };

  const token = keycloak.tokenParsed;
  const tenantId = token?.tenant_id;
  const username = token?.preferred_username;
  const email = token?.email;
  const roles = token?.realm_access?.roles || [];

  return (
    <div className="container">
      <header>
        <h1>Gymnastics Platform - User Portal</h1>
        <button onClick={() => keycloak.logout()}>
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

            <dt>Roles:</dt>
            <dd>{roles.filter(r => !r.startsWith('default-') && !r.startsWith('uma_')).join(', ')}</dd>
          </dl>
        </section>

        <section className="api-test">
          <h2>API Integration</h2>
          <p>Token available for API calls:</p>
          <code className="token-preview">
            Bearer {keycloak.token?.substring(0, 50)}...
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
