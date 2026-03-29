import { useState, useEffect, useCallback } from 'react';
import { useKeycloak } from '@react-keycloak/web';
import { Link } from 'react-router-dom';
import { adminApiClient, type UserProfileResponse } from '../lib/api-client';

export function SyncUsersPage() {
  const { keycloak } = useKeycloak();
  const [users, setUsers] = useState<UserProfileResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [syncing, setSyncing] = useState<Set<string>>(new Set());
  const [results, setResults] = useState<Map<string, { success: boolean; message: string }>>(new Map());
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  const fetchUsers = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      if (!keycloak.token) {
        throw new Error('Not authenticated');
      }

      const data = await adminApiClient.listUsers(keycloak.token);
      setUsers(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch users');
    } finally {
      setLoading(false);
    }
  }, [keycloak.token]);

  const syncUser = async (userId: string) => {
    setSyncing(prev => new Set(prev).add(userId));

    try {
      if (!keycloak.token) {
        throw new Error('Not authenticated');
      }

      const result = await adminApiClient.syncUserTenant(keycloak.token, userId);
      setResults(prev => new Map(prev).set(userId, {
        success: true,
        message: result.message
      }));
    } catch (err) {
      setResults(prev => new Map(prev).set(userId, {
        success: false,
        message: err instanceof Error ? err.message : 'Sync failed'
      }));
    } finally {
      setSyncing(prev => {
        const next = new Set(prev);
        next.delete(userId);
        return next;
      });
    }
  };

  const syncAllUsers = async () => {
    for (const user of users) {
      await syncUser(user.keycloakUserId);
    }
  };

  if (loading) {
    return (
      <div className="container">
        <div className="loading-message">
          <h2>Loading users...</h2>
        </div>
      </div>
    );
  }

  return (
    <div className="container">
      <header>
        <h1 style={{ marginRight: '1rem', flex: 1 }}>Sync User Tenants</h1>
        <button onClick={() => keycloak.logout()} style={{ flexShrink: 0 }}>
          Logout
        </button>
      </header>

      <nav style={{ marginBottom: '20px', padding: '10px', background: 'rgba(170, 59, 255, 0.05)', border: '1px solid var(--accent-border)', borderRadius: '8px' }}>
        <Link to="/" style={{ marginRight: '20px', color: 'var(--accent)', textDecoration: 'none', fontWeight: 500 }}>← Back to Home</Link>
      </nav>

      <section className="user-info">
        <h2>Sync Keycloak Tenant Attributes</h2>
        <p className="info">
          This tool syncs user tenant_id attributes from the database to Keycloak.
          After syncing, users will have the correct tenant in their JWT tokens on next login.
        </p>

        {error && (
          <div className="error">
            <strong>Error:</strong> {error}
          </div>
        )}

        <div style={{ marginTop: '1.5rem', display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
          <button
            onClick={syncAllUsers}
            disabled={syncing.size > 0}
          >
            {syncing.size > 0 ? 'Syncing...' : 'Sync All Users'}
          </button>
          <button onClick={fetchUsers}>
            Refresh List
          </button>
        </div>
      </section>

      {users.length === 0 ? (
        <section className="user-info">
          <p>No users found</p>
        </section>
      ) : (
        <section className="user-info">
          <h2>Users ({users.length})</h2>
          <div style={{ overflowX: 'auto', marginTop: '1rem' }}>
            <table style={{
              width: '100%',
              borderCollapse: 'collapse',
              minWidth: '700px'
            }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--border)' }}>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.95rem' }}>Email</th>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.95rem' }}>Full Name</th>
                  <th style={{ textAlign: 'center', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.95rem', width: '80px' }}>Onboarded</th>
                  <th style={{ textAlign: 'center', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.95rem', width: '80px' }}>Choice</th>
                  <th style={{ textAlign: 'center', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.95rem', width: '120px' }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => {
                  const isSyncing = syncing.has(user.keycloakUserId);
                  const result = results.get(user.keycloakUserId);

                  return (
                    <tr key={user.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '8px 10px', color: 'var(--text-h)', fontSize: '0.95rem' }}>
                        {user.email}
                      </td>
                      <td style={{ padding: '8px 10px', color: 'var(--text)', fontSize: '0.95rem' }}>
                        {user.fullName}
                      </td>
                      <td style={{ textAlign: 'center', padding: '8px 10px', color: 'var(--text-h)', fontSize: '1.1rem' }}>
                        {user.onboardingCompleted ? '✓' : '✗'}
                      </td>
                      <td style={{ textAlign: 'center', padding: '8px 10px', color: 'var(--text)', fontSize: '0.9rem' }}>
                        {user.onboardingChoice || '-'}
                      </td>
                      <td style={{ textAlign: 'center', padding: '8px 10px' }}>
                        <button
                          onClick={() => syncUser(user.keycloakUserId, user.email)}
                          disabled={isSyncing}
                          style={{
                            padding: '0.5rem 1rem',
                            fontSize: '0.9rem',
                            minWidth: '80px'
                          }}
                        >
                          {isSyncing ? 'Syncing...' : 'Sync'}
                        </button>
                        {result && (
                          <div style={{
                            fontSize: '0.8rem',
                            marginTop: '4px',
                            color: result.success ? '#4caf50' : '#e74c3c',
                            fontWeight: 500
                          }}>
                            {result.success ? '✓ Synced' : '✗ Failed'}
                          </div>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}
