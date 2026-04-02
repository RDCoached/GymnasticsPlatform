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
  const [resendingInvite, setResendingInvite] = useState<string | null>(null);

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

  const handleResendEmail = async (inviteId: string, email: string) => {
    if (!clubId || !authToken) return;

    setResendingInvite(inviteId);
    setError(null);

    try {
      // Resend uses the same endpoint - it will send another email with the same code
      await apiClient.sendEmailInvite(authToken, clubId, {
        email,
        inviteType: invites.find(i => i.id === inviteId)?.inviteType ?? 2,
        description: 'Resent invitation',
      });

      alert(`Invitation resent to ${email}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to resend invitation');
    } finally {
      setResendingInvite(null);
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
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', boxSizing: 'border-box' }}
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
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', boxSizing: 'border-box' }}
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
                style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', resize: 'vertical', boxSizing: 'border-box' }}
              />
            </div>

            <button
              type="submit"
              disabled={sendingEmail}
              style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', boxSizing: 'border-box' }}
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
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--border)', background: 'rgba(170, 59, 255, 0.03)' }}>
                  <th style={{ textAlign: 'left', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Email</th>
                  <th style={{ textAlign: 'left', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Code</th>
                  <th style={{ textAlign: 'center', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Role</th>
                  <th style={{ textAlign: 'center', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Status</th>
                  <th style={{ textAlign: 'left', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Expires</th>
                  <th style={{ textAlign: 'center', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Used</th>
                  <th style={{ textAlign: 'right', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {invites.map((invite) => {
                  const expired = isExpired(invite.expiresAt);
                  const full = isFull(invite);
                  const inactive = expired || full;
                  const canResend = invite.email && !expired && !full;

                  return (
                    <tr key={invite.id} style={{ borderBottom: '1px solid var(--border)', opacity: inactive ? 0.5 : 1 }}>
                      <td style={{ padding: '12px', fontWeight: 500 }}>
                        {invite.email || <span style={{ color: '#999', fontStyle: 'italic' }}>No email</span>}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <code style={{
                          background: 'rgba(170, 59, 255, 0.1)',
                          padding: '4px 8px',
                          borderRadius: '4px',
                          fontFamily: 'monospace',
                          fontSize: '0.85rem',
                          fontWeight: 600,
                          color: 'var(--accent)'
                        }}>
                          {invite.code}
                        </code>
                      </td>
                      <td style={{ textAlign: 'center', padding: '12px' }}>
                        <span style={{
                          display: 'inline-block',
                          padding: '4px 10px',
                          borderRadius: '12px',
                          fontSize: '0.75rem',
                          fontWeight: 600,
                          textTransform: 'uppercase',
                          letterSpacing: '0.5px',
                          background: invite.inviteType === 1 ? 'rgba(52, 152, 219, 0.1)' : 'rgba(155, 89, 182, 0.1)',
                          color: invite.inviteType === 1 ? '#3498db' : '#9b59b6',
                        }}>
                          {getInviteTypeName(invite.inviteType)}
                        </span>
                      </td>
                      <td style={{ textAlign: 'center', padding: '12px' }}>
                        {expired ? (
                          <span style={{
                            display: 'inline-block',
                            padding: '4px 10px',
                            borderRadius: '12px',
                            fontSize: '0.75rem',
                            fontWeight: 600,
                            textTransform: 'uppercase',
                            letterSpacing: '0.5px',
                            background: 'rgba(231, 76, 60, 0.1)',
                            color: '#e74c3c',
                          }}>
                            Expired
                          </span>
                        ) : full ? (
                          <span style={{
                            display: 'inline-block',
                            padding: '4px 10px',
                            borderRadius: '12px',
                            fontSize: '0.75rem',
                            fontWeight: 600,
                            textTransform: 'uppercase',
                            letterSpacing: '0.5px',
                            background: 'rgba(230, 126, 34, 0.1)',
                            color: '#e67e22',
                          }}>
                            Full
                          </span>
                        ) : (
                          <span style={{
                            display: 'inline-block',
                            padding: '4px 10px',
                            borderRadius: '12px',
                            fontSize: '0.75rem',
                            fontWeight: 600,
                            textTransform: 'uppercase',
                            letterSpacing: '0.5px',
                            background: 'rgba(39, 174, 96, 0.1)',
                            color: '#27ae60',
                          }}>
                            Active
                          </span>
                        )}
                      </td>
                      <td style={{ padding: '12px', color: expired ? '#e74c3c' : 'var(--text)' }}>
                        {new Date(invite.expiresAt).toLocaleDateString('en-US', {
                          month: 'short',
                          day: 'numeric',
                          year: 'numeric',
                        })}
                      </td>
                      <td style={{ textAlign: 'center', padding: '12px' }}>
                        <span style={{
                          fontWeight: 500,
                          color: invite.timesUsed > 0 ? '#27ae60' : '#999'
                        }}>
                          {invite.timesUsed} / {invite.maxUses}
                        </span>
                      </td>
                      <td style={{ textAlign: 'right', padding: '12px' }}>
                        <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                          <button
                            onClick={() => copyToClipboard(invite.code)}
                            style={{
                              padding: '6px 12px',
                              fontSize: '0.8rem',
                              background: copiedCode === invite.code ? '#27ae60' : 'rgba(170, 59, 255, 0.08)',
                              color: copiedCode === invite.code ? 'white' : 'var(--accent)',
                              border: copiedCode === invite.code ? '1px solid #27ae60' : '1px solid var(--accent)',
                              borderRadius: '4px',
                              cursor: 'pointer',
                              fontWeight: 600,
                              whiteSpace: 'nowrap',
                            }}
                            title="Copy invite code to clipboard"
                          >
                            {copiedCode === invite.code ? '✓ Copied' : 'Copy'}
                          </button>
                          {canResend && (
                            <button
                              onClick={() => handleResendEmail(invite.id, invite.email!)}
                              disabled={resendingInvite === invite.id}
                              style={{
                                padding: '6px 12px',
                                fontSize: '0.8rem',
                                background: 'var(--accent)',
                                color: 'white',
                                border: 'none',
                                borderRadius: '4px',
                                cursor: resendingInvite === invite.id ? 'not-allowed' : 'pointer',
                                fontWeight: 500,
                                opacity: resendingInvite === invite.id ? 0.6 : 1,
                                whiteSpace: 'nowrap',
                              }}
                              title="Resend invitation email"
                            >
                              {resendingInvite === invite.id ? 'Sending...' : 'Resend'}
                            </button>
                          )}
                        </div>
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
