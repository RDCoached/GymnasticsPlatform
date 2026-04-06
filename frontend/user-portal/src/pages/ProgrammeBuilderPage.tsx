import { useState, useCallback, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import { apiClient, type StartProgrammeBuilderRequest, type BuilderSessionResult, type GymnastResponse } from '../lib/api-client';

type BuilderMode = 'input' | 'conversation' | 'loading' | 'success';

interface ConversationMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

interface BuilderState {
  mode: BuilderMode;
  sessionId: string | null;
  gymnastId: string;
  goals: string;
  ragScope: 'gymnast' | 'tenant';
  messages: ConversationMessage[];
  currentMessage: string;
  error: string | null;
  programmeId: string | null;
}

export function ProgrammeBuilderPage() {
  const { getToken, logout } = useAuth();
  const navigate = useNavigate();

  const [state, setState] = useState<BuilderState>({
    mode: 'input',
    sessionId: null,
    gymnastId: '',
    goals: '',
    ragScope: 'gymnast',
    messages: [],
    currentMessage: '',
    error: null,
    programmeId: null,
  });

  const [gymnasts, setGymnasts] = useState<GymnastResponse[]>([]);
  const [loadingGymnasts, setLoadingGymnasts] = useState(true);

  // Fetch gymnasts on mount
  useEffect(() => {
    const fetchGymnasts = async () => {
      try {
        const token = getToken();
        if (!token) return;

        const data = await apiClient.listGymnasts(token);
        setGymnasts(data);
      } catch (err) {
        console.error('Failed to fetch gymnasts:', err);
      } finally {
        setLoadingGymnasts(false);
      }
    };

    fetchGymnasts();
  }, [getToken]);

  const handleStartSession = useCallback(async () => {
    setState(prev => ({ ...prev, mode: 'loading', error: null }));
    try {
      const token = getToken();
      if (!token) throw new Error('No authentication token');

      const request: StartProgrammeBuilderRequest = {
        gymnastId: state.gymnastId,
        goals: state.goals,
        ragScope: state.ragScope,
      };

      const result: BuilderSessionResult = await apiClient.startProgrammeBuilder(token, request);

      setState(prev => ({
        ...prev,
        mode: 'conversation',
        sessionId: result.sessionId,
        messages: [
          { role: 'user', content: state.goals, timestamp: new Date() },
          { role: 'assistant', content: result.suggestion, timestamp: new Date() },
        ],
      }));
    } catch (err) {
      setState(prev => ({
        ...prev,
        mode: 'input',
        error: err instanceof Error ? err.message : 'Failed to start session',
      }));
    }
  }, [state.gymnastId, state.goals, state.ragScope, getToken]);

  const handleSendMessage = useCallback(async () => {
    if (!state.currentMessage.trim()) return;

    const userMessage = state.currentMessage;
    setState(prev => ({
      ...prev,
      mode: 'loading',
      currentMessage: '',
      messages: [
        ...prev.messages,
        { role: 'user', content: userMessage, timestamp: new Date() },
      ],
    }));

    try {
      const token = getToken();
      if (!token) throw new Error('No authentication token');

      const result = await apiClient.continueProgrammeBuilder(
        token,
        state.sessionId!,
        { message: userMessage }
      );

      setState(prev => ({
        ...prev,
        mode: 'conversation',
        messages: [
          ...prev.messages,
          { role: 'assistant', content: result.suggestion, timestamp: new Date() },
        ],
      }));
    } catch (err) {
      setState(prev => ({
        ...prev,
        mode: 'conversation',
        error: err instanceof Error ? err.message : 'Failed to send message',
      }));
    }
  }, [state.currentMessage, state.sessionId, getToken]);

  const handleAcceptProgramme = useCallback(async () => {
    setState(prev => ({ ...prev, mode: 'loading' }));
    try {
      const token = getToken();
      if (!token) throw new Error('No authentication token');

      const programmeId = await apiClient.acceptProgrammeBuilder(token, state.sessionId!);

      setState(prev => ({ ...prev, mode: 'success', programmeId }));

      // Redirect to dashboard after 2s
      setTimeout(() => navigate('/dashboard'), 2000);
    } catch (err) {
      setState(prev => ({
        ...prev,
        mode: 'conversation',
        error: err instanceof Error ? err.message : 'Failed to accept programme',
      }));
    }
  }, [state.sessionId, getToken, navigate]);

  const handleLogout = async () => {
    await logout();
    navigate('/sign-in');
  };

  // Render based on mode
  if (state.mode === 'loading') {
    return (
      <div className="container-wide">
        <header>
          <h1>Programme Builder</h1>
          <button onClick={handleLogout}>Logout</button>
        </header>
        <div style={{ padding: '2rem', textAlign: 'center' }}>
          <div style={{ fontSize: '1.2rem', marginBottom: '1rem' }}>
            {state.messages.length === 0 ? 'Generating programme suggestion...' : 'Processing your request...'}
          </div>
          <div style={{ color: 'var(--text)', opacity: 0.7 }}>
            This may take a few moments
          </div>
        </div>
      </div>
    );
  }

  if (state.mode === 'success') {
    return (
      <div className="container-wide">
        <header>
          <h1>Programme Builder</h1>
          <button onClick={handleLogout}>Logout</button>
        </header>
        <div className="success-message" style={{ margin: '2rem', padding: '1.5rem', textAlign: 'center' }}>
          <strong>✓ Programme created successfully!</strong>
          <p style={{ marginTop: '1rem' }}>Redirecting to dashboard...</p>
        </div>
      </div>
    );
  }

  if (state.mode === 'conversation') {
    return (
      <div className="container-wide">
        <header>
          <h1>Programme Builder</h1>
          <button onClick={handleLogout}>Logout</button>
        </header>

        <nav style={{ marginBottom: '20px', padding: '10px', background: 'rgba(170, 59, 255, 0.05)', border: '1px solid var(--accent-border)', borderRadius: '8px' }}>
          <button
            onClick={() => navigate('/dashboard')}
            style={{ background: 'none', border: 'none', color: 'var(--accent)', textDecoration: 'none', fontWeight: 500, cursor: 'pointer', padding: '0', fontSize: '1rem' }}
          >
            ← Back to Dashboard
          </button>
        </nav>

        <div style={{ padding: '1rem', background: 'rgba(170, 59, 255, 0.05)', border: '1px solid var(--accent-border)', borderRadius: '8px', marginBottom: '1.5rem' }}>
          <strong>Goals:</strong> {state.goals}
        </div>

        {state.error && (
          <div className="error" style={{ marginBottom: '1.5rem' }} role="alert">
            <strong>Error:</strong> {state.error}
          </div>
        )}

        <div style={{ maxHeight: '60vh', overflowY: 'auto', padding: '1rem', background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: '8px', marginBottom: '1.5rem' }}>
          {state.messages.map((msg, idx) => (
            <div
              key={idx}
              style={{
                marginBottom: '1.5rem',
                padding: '1rem',
                borderRadius: '8px',
                borderLeft: msg.role === 'assistant' ? '4px solid var(--accent)' : '4px solid var(--text-h)',
                background: msg.role === 'assistant' ? 'rgba(170, 59, 255, 0.05)' : 'var(--code-bg)',
              }}
            >
              <div style={{ fontWeight: 600, marginBottom: '0.5rem', color: 'var(--text-h)' }}>
                {msg.role === 'assistant' ? '🤖 AI Coach' : '💬 You'}
              </div>
              <div style={{ whiteSpace: 'pre-wrap', lineHeight: 1.6 }}>
                {msg.content}
              </div>
              <div style={{ fontSize: '0.875rem', color: 'var(--text)', opacity: 0.7, marginTop: '0.5rem' }}>
                {msg.timestamp.toLocaleTimeString()}
              </div>
            </div>
          ))}
        </div>

        <div style={{ position: 'sticky', bottom: 0, background: 'var(--bg)', padding: '1rem', border: '1px solid var(--border)', borderRadius: '8px' }}>
          <label htmlFor="refine-message" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
            Refine programme
          </label>
          <textarea
            id="refine-message"
            placeholder="Request changes (e.g., 'Can we add more plyometrics?')"
            rows={3}
            value={state.currentMessage}
            onChange={(e) => setState(prev => ({ ...prev, currentMessage: e.target.value }))}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                if (state.currentMessage.trim()) handleSendMessage();
              }
            }}
            style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', marginBottom: '1rem', boxSizing: 'border-box' }}
          />
          <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
            <button
              onClick={handleSendMessage}
              disabled={!state.currentMessage.trim()}
              style={{ padding: '0.75rem 1.5rem', fontSize: '1rem', borderRadius: '4px', background: 'rgba(170, 59, 255, 0.08)', color: 'var(--accent)', border: '1px solid var(--accent)' }}
            >
              Send Message
            </button>
            <button
              onClick={handleAcceptProgramme}
              style={{ padding: '0.75rem 1.5rem', fontSize: '1rem', borderRadius: '4px' }}
            >
              ✓ Accept Programme
            </button>
          </div>
        </div>
      </div>
    );
  }

  // Input mode (default)
  return (
    <div className="container-wide">
      <header>
        <h1>Build New Programme</h1>
        <button onClick={handleLogout}>Logout</button>
      </header>

      <nav style={{ marginBottom: '20px', padding: '10px', background: 'rgba(170, 59, 255, 0.05)', border: '1px solid var(--accent-border)', borderRadius: '8px' }}>
        <button
          onClick={() => navigate('/dashboard')}
          style={{ background: 'none', border: 'none', color: 'var(--accent)', textDecoration: 'none', fontWeight: 500, cursor: 'pointer', padding: '0', fontSize: '1rem' }}
        >
          ← Back to Dashboard
        </button>
      </nav>

      {state.error && (
        <div className="error" style={{ marginBottom: '1.5rem' }} role="alert">
          <strong>Error:</strong> {state.error}
        </div>
      )}

      <form
        className="form-container-wide"
        onSubmit={(e) => {
          e.preventDefault();
          if (state.gymnastId && state.goals.trim()) {
            handleStartSession();
          }
        }}
      >
        <div className="form-group">
          <label htmlFor="gymnast">Select Gymnast *</label>
          <select
            id="gymnast"
            value={state.gymnastId}
            onChange={(e) => setState(prev => ({ ...prev, gymnastId: e.target.value }))}
            required
            disabled={loadingGymnasts}
            style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', boxSizing: 'border-box' }}
          >
            <option value="">{loadingGymnasts ? 'Loading gymnasts...' : '-- Choose a gymnast --'}</option>
            {gymnasts.map(g => (
              <option key={g.id} value={g.id}>{g.name}</option>
            ))}
          </select>
          {!loadingGymnasts && gymnasts.length === 0 && (
            <small style={{ color: '#e74c3c', fontSize: '0.85rem', marginTop: '0.25rem', display: 'block' }}>
              No gymnasts found. <button
                type="button"
                onClick={() => navigate('/gymnasts')}
                style={{ background: 'none', border: 'none', color: 'var(--accent)', textDecoration: 'underline', cursor: 'pointer', padding: 0, fontSize: 'inherit' }}
              >
                Add a gymnast first
              </button>
            </small>
          )}
        </div>

        <div className="form-group">
          <label htmlFor="goals">Training Goals *</label>
          <textarea
            id="goals"
            rows={6}
            placeholder="Describe the training goals for this programme (e.g., 'Improve vault power and landing stability')"
            value={state.goals}
            onChange={(e) => setState(prev => ({ ...prev, goals: e.target.value }))}
            required
            style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', border: '1px solid var(--border)', resize: 'vertical', boxSizing: 'border-box' }}
          />
        </div>

        <fieldset className="form-group" style={{ border: '1px solid var(--border)', borderRadius: '4px', padding: '1rem' }}>
          <legend style={{ padding: '0 0.5rem', fontWeight: 500 }}>Search Scope</legend>
          <label style={{ display: 'block', marginBottom: '0.5rem' }}>
            <input
              type="radio"
              name="ragScope"
              value="gymnast"
              checked={state.ragScope === 'gymnast'}
              onChange={() => setState(prev => ({ ...prev, ragScope: 'gymnast' }))}
              style={{ marginRight: '0.5rem' }}
            />
            Only this gymnast's past programmes
          </label>
          <label style={{ display: 'block' }}>
            <input
              type="radio"
              name="ragScope"
              value="tenant"
              checked={state.ragScope === 'tenant'}
              onChange={() => setState(prev => ({ ...prev, ragScope: 'tenant' }))}
              style={{ marginRight: '0.5rem' }}
            />
            All club programmes (wider search)
          </label>
        </fieldset>

        <button
          type="submit"
          disabled={!state.gymnastId || !state.goals.trim()}
          style={{ width: '100%', padding: '0.75rem', fontSize: '1rem', borderRadius: '4px', boxSizing: 'border-box' }}
        >
          Generate Programme Suggestion →
        </button>
      </form>
    </div>
  );
}
