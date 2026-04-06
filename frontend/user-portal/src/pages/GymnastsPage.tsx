import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import { apiClient, type GymnastResponse, type CreateGymnastRequest, type UpdateGymnastRequest } from '../lib/api-client';

type Mode = 'list' | 'create' | 'edit';

export function GymnastsPage() {
  const { getToken, logout } = useAuth();
  const navigate = useNavigate();
  const authToken = getToken();

  const [mode, setMode] = useState<Mode>('list');
  const [gymnasts, setGymnasts] = useState<GymnastResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const [formData, setFormData] = useState({
    email: '',
    fullName: '',
  });
  const [editingId, setEditingId] = useState<string | null>(null);

  const fetchGymnasts = useCallback(async () => {
    if (!authToken) return;

    setLoading(true);
    setError(null);

    try {
      const data = await apiClient.listGymnasts(authToken);
      setGymnasts(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch gymnasts');
    } finally {
      setLoading(false);
    }
  }, [authToken]);

  useEffect(() => {
    if (mode === 'list') {
      fetchGymnasts();
    }
  }, [mode, fetchGymnasts]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!authToken) return;

    setSubmitting(true);
    setError(null);

    try {
      const request: CreateGymnastRequest = {
        email: formData.email,
        fullName: formData.fullName,
      };

      await apiClient.createGymnast(authToken, request);

      // Reset form and return to list
      setFormData({ email: '', fullName: '' });
      setMode('list');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create gymnast');
    } finally {
      setSubmitting(false);
    }
  };

  const handleUpdate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!authToken || !editingId) return;

    setSubmitting(true);
    setError(null);

    try {
      const request: UpdateGymnastRequest = {
        fullName: formData.fullName,
      };

      await apiClient.updateGymnast(authToken, editingId, request);

      // Reset form and return to list
      setFormData({ email: '', fullName: '' });
      setEditingId(null);
      setMode('list');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update gymnast');
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!authToken) return;
    if (!confirm('Are you sure you want to remove this gymnast?')) return;

    setDeletingId(id);
    setError(null);

    try {
      await apiClient.deleteGymnast(authToken, id);
      await fetchGymnasts();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete gymnast');
    } finally {
      setDeletingId(null);
    }
  };

  const startEdit = (gymnast: GymnastResponse) => {
    setFormData({
      email: gymnast.email,
      fullName: gymnast.name,
    });
    setEditingId(gymnast.id);
    setMode('edit');
    setError(null);
  };

  const cancelForm = () => {
    setFormData({ email: '', fullName: '' });
    setEditingId(null);
    setMode('list');
    setError(null);
  };

  const handleLogout = async () => {
    await logout();
    navigate('/sign-in');
  };

  // Render form (create or edit mode)
  if (mode === 'create' || mode === 'edit') {
    return (
      <div className="container-wide">
        <header>
          <h1 style={{ marginRight: '1rem', flex: 1 }}>
            {mode === 'create' ? 'Add New Gymnast' : 'Edit Gymnast'}
          </h1>
          <button onClick={handleLogout} style={{ flexShrink: 0 }}>
            Logout
          </button>
        </header>

        <nav style={{ marginBottom: '20px', padding: '10px', background: 'rgba(170, 59, 255, 0.05)', border: '1px solid var(--accent-border)', borderRadius: '8px' }}>
          <button
            onClick={cancelForm}
            style={{ background: 'none', border: 'none', color: 'var(--accent)', textDecoration: 'none', fontWeight: 500, cursor: 'pointer', padding: '0', fontSize: '1rem' }}
          >
            ← Back to Gymnasts
          </button>
        </nav>

        {error && (
          <div className="error" style={{ marginBottom: '1.5rem' }}>
            <strong>Error:</strong> {error}
          </div>
        )}

        <section className="user-info">
          <form onSubmit={mode === 'create' ? handleCreate : handleUpdate}>
            <div className="form-row-2-col" style={{ marginBottom: '1rem' }}>
              <div>
                <label htmlFor="email" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
                  Email Address *
                </label>
                <input
                  id="email"
                  type="email"
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                  placeholder="gymnast@example.com"
                  required
                  disabled={submitting || mode === 'edit'}
                  style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', boxSizing: 'border-box' }}
                />
                {mode === 'edit' && (
                  <small style={{ color: '#666', fontSize: '0.85rem', marginTop: '0.25rem', display: 'block' }}>
                    Email cannot be changed
                  </small>
                )}
              </div>

              <div>
                <label htmlFor="fullName" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
                  Full Name *
                </label>
                <input
                  id="fullName"
                  type="text"
                  value={formData.fullName}
                  onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
                  placeholder="John Smith"
                  required
                  minLength={2}
                  maxLength={100}
                  disabled={submitting}
                  style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', boxSizing: 'border-box' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', gap: '1rem', marginTop: '1.5rem' }}>
              <button
                type="submit"
                disabled={submitting}
                style={{ flex: 1, padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', boxSizing: 'border-box' }}
              >
                {submitting ? 'Saving...' : mode === 'create' ? 'Add Gymnast' : 'Update Gymnast'}
              </button>
              <button
                type="button"
                onClick={cancelForm}
                disabled={submitting}
                style={{ flex: 1, padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', boxSizing: 'border-box', background: '#666', color: 'white', border: 'none' }}
              >
                Cancel
              </button>
            </div>
          </form>
        </section>
      </div>
    );
  }

  // Render list mode
  return (
    <div className="container-wide">
      <header>
        <h1 style={{ marginRight: '1rem', flex: 1 }}>Gymnasts</h1>
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

      <div style={{ marginBottom: '1.5rem' }}>
        <button
          onClick={() => setMode('create')}
          style={{ padding: '0.75rem 1.5rem', fontSize: '1rem', borderRadius: '4px', fontWeight: 500 }}
        >
          + Add New Gymnast
        </button>
      </div>

      <section className="user-info">
        <h2>Registered Gymnasts</h2>
        {loading ? (
          <p style={{ marginTop: '1rem' }}>Loading gymnasts...</p>
        ) : gymnasts.length === 0 ? (
          <p style={{ marginTop: '1rem', color: 'var(--text)' }}>No gymnasts registered yet.</p>
        ) : (
          <>
            {/* Desktop Table View */}
            <div style={{ overflowX: 'auto', marginTop: '1rem' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid var(--border)', background: 'rgba(170, 59, 255, 0.03)' }}>
                    <th style={{ textAlign: 'left', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Name</th>
                    <th style={{ textAlign: 'left', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Email</th>
                    <th style={{ textAlign: 'right', padding: '10px 12px', color: 'var(--text-h)', fontWeight: 600, fontSize: '0.85rem', textTransform: 'uppercase', letterSpacing: '0.5px' }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {gymnasts.map((gymnast) => (
                    <tr key={gymnast.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px', fontWeight: 500 }}>{gymnast.name}</td>
                      <td style={{ padding: '12px', color: 'var(--text)' }}>{gymnast.email}</td>
                      <td style={{ textAlign: 'right', padding: '12px' }}>
                        <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                          <button
                            onClick={() => startEdit(gymnast)}
                            disabled={deletingId === gymnast.id}
                            style={{
                              padding: '6px 12px',
                              fontSize: '0.8rem',
                              background: 'rgba(170, 59, 255, 0.08)',
                              color: 'var(--accent)',
                              border: '1px solid var(--accent)',
                              borderRadius: '4px',
                              cursor: 'pointer',
                              fontWeight: 600,
                              whiteSpace: 'nowrap',
                            }}
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDelete(gymnast.id)}
                            disabled={deletingId === gymnast.id}
                            style={{
                              padding: '6px 12px',
                              fontSize: '0.8rem',
                              background: '#e74c3c',
                              color: 'white',
                              border: 'none',
                              borderRadius: '4px',
                              cursor: deletingId === gymnast.id ? 'not-allowed' : 'pointer',
                              fontWeight: 500,
                              opacity: deletingId === gymnast.id ? 0.6 : 1,
                              whiteSpace: 'nowrap',
                            }}
                          >
                            {deletingId === gymnast.id ? 'Removing...' : 'Remove'}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Mobile Card View */}
            <div className="gymnasts-cards" style={{ marginTop: '1rem', display: 'none' }}>
              {gymnasts.map((gymnast) => (
                <div
                  key={gymnast.id}
                  style={{
                    border: '1px solid var(--border)',
                    borderRadius: '8px',
                    padding: '1rem',
                    marginBottom: '1rem',
                    background: 'var(--bg)',
                  }}
                >
                  <div style={{ marginBottom: '0.75rem' }}>
                    <div style={{ fontWeight: 600, fontSize: '1rem', marginBottom: '0.25rem' }}>
                      {gymnast.name}
                    </div>
                    <div style={{ color: 'var(--text)', fontSize: '0.9rem' }}>
                      {gymnast.email}
                    </div>
                  </div>

                  <div style={{ display: 'flex', gap: '8px', marginTop: '1rem' }}>
                    <button
                      onClick={() => startEdit(gymnast)}
                      disabled={deletingId === gymnast.id}
                      style={{
                        flex: 1,
                        padding: '8px 16px',
                        fontSize: '0.9rem',
                        background: 'rgba(170, 59, 255, 0.08)',
                        color: 'var(--accent)',
                        border: '1px solid var(--accent)',
                        borderRadius: '4px',
                        cursor: 'pointer',
                        fontWeight: 600,
                      }}
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => handleDelete(gymnast.id)}
                      disabled={deletingId === gymnast.id}
                      style={{
                        flex: 1,
                        padding: '8px 16px',
                        fontSize: '0.9rem',
                        background: '#e74c3c',
                        color: 'white',
                        border: 'none',
                        borderRadius: '4px',
                        cursor: deletingId === gymnast.id ? 'not-allowed' : 'pointer',
                        fontWeight: 500,
                        opacity: deletingId === gymnast.id ? 0.6 : 1,
                      }}
                    >
                      {deletingId === gymnast.id ? 'Removing...' : 'Remove'}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </>
        )}
      </section>

      <style>{`
        @media (max-width: 768px) {
          table {
            display: none !important;
          }
          .gymnasts-cards {
            display: block !important;
          }
        }
      `}</style>
    </div>
  );
}
