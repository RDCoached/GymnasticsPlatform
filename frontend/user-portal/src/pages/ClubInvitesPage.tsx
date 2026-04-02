import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiClient, type InviteResponse, type SendEmailInviteRequest } from '../lib/api-client';

export function ClubInvitesPage() {
  const { getToken, logout } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const clubId = searchParams.get('clubId');

  const [invites, setInvites] = useState<InviteResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [copiedCode, setCopiedCode] = useState<string | null>(null);

  const [emailFormData, setEmailFormData] = useState({
    email: '',
    inviteType: 2, // Default to Gymnast
    description: '',
  });
  const [sendingEmail, setSendingEmail] = useState(false);

  const authToken = getToken();

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


  const handleSendEmailInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!clubId || !authToken) return;

    setSendingEmail(true);
    setError(null);

    try {
      const request: SendEmailInviteRequest = {
        email: emailFormData.email,
        inviteType: emailFormData.inviteType,
        description: emailFormData.description || undefined,
      };

      await apiClient.sendEmailInvite(authToken, clubId, request);

      // Reset form
      setEmailFormData({ email: '', inviteType: 2, description: '' });

      // Refresh invites list
      await fetchInvites();

      // Show success - you could add a success message state if desired
      alert(`Invitation sent to ${emailFormData.email}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to send invitation');
    } finally {
      setSendingEmail(false);
    }
  };

  const copyToClipboard = async (code: string) => {
    try {
      await navigator.clipboard.writeText(code);
      setCopiedCode(code);
      setTimeout(() => setCopiedCode(null), 2000);
    } catch {
      setError('Failed to copy invite code');
    }
  };

  const handleLogout = async () => {
    await logout();
    navigate('/sign-in');
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

      <section className="user-info" style={{ marginBottom: '2rem' }}>
        <h2>Send Email Invitation</h2>
        <p style={{ color: '#666', marginBottom: '1rem', marginTop: '0.5rem' }}>
          Send a personal invitation to someone's email address.
          They'll receive a link to register and join your club.
        </p>

        <form onSubmit={handleSendEmailInvite} style={{ marginTop: '1.5rem' }}>
          <div style={{ display: 'grid', gap: '1rem' }}>
            <div>
              <label htmlFor="inviteEmail" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
                Email Address *
              </label>
              <input
                id="inviteEmail"
                type="email"
                value={emailFormData.email}
                onChange={(e) => setEmailFormData({ ...emailFormData, email: e.target.value })}
                placeholder="person@example.com"
                required
                disabled={sendingEmail}
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)' }}
              />
            </div>

            <div>
              <label htmlFor="emailInviteType" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
                Role *
              </label>
              <select
                id="emailInviteType"
                value={emailFormData.inviteType}
                onChange={(e) => setEmailFormData({ ...emailFormData, inviteType: Number(e.target.value) })}
                required
                disabled={sendingEmail}
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)' }}
              >
                <option value={2}>Gymnast</option>
                <option value={1}>Coach</option>
              </select>
            </div>

            <div>
              <label htmlFor="emailDescription" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
                Personal Message (optional)
              </label>
              <textarea
                id="emailDescription"
                value={emailFormData.description}
                onChange={(e) => setEmailFormData({ ...emailFormData, description: e.target.value })}
                placeholder="Add a personal note to the invitation..."
                rows={3}
                disabled={sendingEmail}
                maxLength={500}
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', resize: 'vertical' }}
              />
            </div>

            <button
              type="submit"
              disabled={sendingEmail}
              style={{ padding: '0.75rem 1.5rem', fontSize: '1rem' }}
            >
              {sendingEmail ? 'Sending...' : 'Send Email Invitation'}
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
            <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: '900px' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--border)' }}>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Code</th>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Type</th>
                  <th style={{ textAlign: 'left', padding: '8px 10px', color: 'var(--text-h)', fontWeight: 600 }}>Email</th>
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
                      <td style={{ padding: '8px 10px', fontSize: '0.9rem' }}>
                        {invite.email || '-'}
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
