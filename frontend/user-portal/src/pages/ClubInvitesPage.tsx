import { useState, useEffect, useCallback } from 'react';
import { useKeycloak } from '@react-keycloak/web';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiClient, type InviteResponse, type CreateInviteRequest } from '../lib/api-client';

export function ClubInvitesPage() {
  const { keycloak } = useKeycloak();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const clubId = searchParams.get('clubId');

  const [invites, setInvites] = useState<InviteResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [copiedCode, setCopiedCode] = useState<string | null>(null);

  const [formData, setFormData] = useState({
    inviteType: 1, // 1 = Coach
    maxUses: 10,
    expiryDays: 7,
    description: '',
  });

  const authToken = localStorage.getItem('accessToken') || keycloak.token;

  const fetchInvites = useCallback(async () => {
    if (!clubId || !authToken) return;

    setLoading(true);
    setError(null);

    try {
      const data = await apiClient.listInvites(authToken, clubId);
      setInvites(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch invites');
    } finally {
      setLoading(false);
    }
  }, [clubId, authToken]);

  useEffect(() => {
    if (!clubId) {
      setError('Club ID is required');
      return;
    }
    fetchInvites();
  }, [clubId, fetchInvites]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!clubId || !authToken) return;

    setCreating(true);
    setError(null);

    try {
      const request: CreateInviteRequest = {
        inviteType: formData.inviteType,
        maxUses: formData.maxUses,
        expiryDays: formData.expiryDays,
        description: formData.description || undefined,
      };

      await apiClient.createInvite(authToken, clubId, request);

      // Reset form
      setFormData({
        inviteType: 1,
        maxUses: 10,
        expiryDays: 7,
        description: '',
      });

      // Refresh invites list
      await fetchInvites();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create invite');
    } finally {
      setCreating(false);
    }
  };

  const copyToClipboard = async (code: string) => {
    try {
      await navigator.clipboard.writeText(code);
      setCopiedCode(code);
      setTimeout(() => setCopiedCode(null), 2000);
    } catch (err) {
      setError('Failed to copy invite code');
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');

    if (keycloak.authenticated) {
      keycloak.logout({ redirectUri: window.location.origin + '/sign-in' });
    } else {
      window.location.href = '/sign-in';
    }
  };

  const getInviteTypeName = (type: number) => {
    return type === 1 ? 'Coach' : 'Gymnast';
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const isExpired = (expiresAt: string) => {
    return new Date(expiresAt) < new Date();
  };

  const isFull = (invite: InviteResponse) => {
    return invite.timesUsed >= invite.maxUses;
  };

  if (!clubId) {
    return (
      <div className="container">
        <div className="error">
          <strong>Error:</strong> Club ID is required. Please navigate from the dashboard.
        </div>
      </div>
    );
  }

  return (
    <div className="container">
      <header>
        <h1 style={{ marginRight: '1rem', flex: 1 }}>Club Invites</h1>
        <button onClick={handleLogout} style={{ flexShrink: 0 }}>
          Logout
        </button>
      </header>

      <nav style={{ marginBottom: '20px', padding: '10px', background: 'rgba(170, 59, 255, 0.05)', border: '1px solid var(--accent-border)', borderRadius: '8px' }}>
        <button
          onClick={() => navigate('/dashboard')}
          style={{ background: 'none', border: 'none', color: 'var(--accent)', textDecoration: 'none', fontWeight: 500, cursor: 'pointer', padding: '0', fontSize: '1rem' }}
        >
          ← Back to Dashboard
        </button>
      </nav>

      {error && (
        <div className="error" style={{ marginBottom: '1.5rem' }}>
          <strong>Error:</strong> {error}
        </div>
      )}

      <section className="user-info">
        <h2>Create New Invite</h2>
        <form onSubmit={handleSubmit} style={{ marginTop: '1.5rem' }}>
          <div style={{ display: 'grid', gap: '1rem' }}>
            <div>
              <label htmlFor="inviteType" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
                Invite Type
              </label>
              <select
                id="inviteType"
                value={formData.inviteType}
                onChange={(e) => setFormData({ ...formData, inviteType: Number(e.target.value) })}
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)' }}
              >
                <option value={1}>Coach</option>
                <option value={2}>Gymnast</option>
              </select>
            </div>

            <div>
              <label htmlFor="maxUses" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
                Max Uses (1-1000)
              </label>
              <input
                type="number"
                id="maxUses"
                min="1"
                max="1000"
                value={formData.maxUses}
                onChange={(e) => setFormData({ ...formData, maxUses: Number(e.target.value) })}
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)' }}
              />
            </div>

            <div>
              <label htmlFor="expiryDays" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
                Expiry Days (1-365)
              </label>
              <input
                type="number"
                id="expiryDays"
                min="1"
                max="365"
                value={formData.expiryDays}
                onChange={(e) => setFormData({ ...formData, expiryDays: Number(e.target.value) })}
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)' }}
              />
            </div>

            <div>
              <label htmlFor="description" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
                Description (optional)
              </label>
              <textarea
                id="description"
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                maxLength={500}
                rows={3}
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', resize: 'vertical' }}
                placeholder="Optional description for this invite..."
              />
            </div>

            <button
              type="submit"
              disabled={creating}
              style={{ padding: '0.75rem 1.5rem', fontSize: '1rem' }}
            >
              {creating ? 'Creating...' : 'Create Invite'}
            </button>
          </div>
        </form>
      </section>

      <section className="user-info" style={{ marginTop: '2rem' }}>
        <h2>Active Invites</h2>
        {loading ? (
          <p style={{ marginTop: '1rem' }}>Loading invites...</p>
        ) : invites.length === 0 ? (
          <p style={{ marginTop: '1rem', color: 'var(--text)' }}>No invites created yet.</p>
        ) : (
          <div style={{ overflowX: 'auto', marginTop: '1rem' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: '800px' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--border)' }}>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Code</th>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Type</th>
                  <th style={{ textAlign: 'center', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Uses</th>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Expires</th>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Status</th>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Description</th>
                  <th style={{ textAlign: 'center', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {invites.map((invite) => {
                  const expired = isExpired(invite.expiresAt);
                  const full = isFull(invite);
                  const inactive = expired || full;

                  return (
                    <tr key={invite.id} style={{ borderBottom: '1px solid var(--border)', opacity: inactive ? 0.6 : 1 }}>
                      <td style={{ padding: '8px 10px', fontFamily: 'monospace', fontSize: '0.9rem' }}>
                        {invite.code}
                      </td>
                      <td style={{ padding: '8px 10px' }}>
                        {getInviteTypeName(invite.inviteType)}
                      </td>
                      <td style={{ textAlign: 'center', padding: '8px 10px' }}>
                        {invite.timesUsed} / {invite.maxUses}
                      </td>
                      <td style={{ padding: '8px 10px', fontSize: '0.9rem' }}>
                        {formatDate(invite.expiresAt)}
                      </td>
                      <td style={{ padding: '8px 10px' }}>
                        {expired ? (
                          <span style={{ color: '#e74c3c', fontWeight: 500 }}>Expired</span>
                        ) : full ? (
                          <span style={{ color: '#e67e22', fontWeight: 500 }}>Full</span>
                        ) : (
                          <span style={{ color: '#27ae60', fontWeight: 500 }}>Active</span>
                        )}
                      </td>
                      <td style={{ padding: '8px 10px', fontSize: '0.9rem', maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                        {invite.description || '-'}
                      </td>
                      <td style={{ textAlign: 'center', padding: '8px 10px' }}>
                        <button
                          onClick={() => copyToClipboard(invite.code)}
                          style={{
                            padding: '0.5rem 1rem',
                            fontSize: '0.9rem',
                            background: copiedCode === invite.code ? '#27ae60' : undefined,
                          }}
                        >
                          {copiedCode === invite.code ? '✓ Copied' : 'Copy Code'}
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
