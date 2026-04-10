import { useState, useCallback, useEffect, useRef } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import { apiClient, type StartProgrammeBuilderRequest, type BuilderSessionResult, type GymnastResponse } from '../lib/api-client';
// import ReactMarkdown from 'react-markdown'; // TODO: npm install react-markdown

type BuilderMode = 'input' | 'section-select' | 'conversation' | 'loading' | 'preview' | 'success';
type Section = 'Bars' | 'Vault' | 'Floor' | 'Beam' | 'Strength & Conditioning';

interface ConversationMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
  section?: Section;
}

interface SectionData {
  section: Section;
  content: string;
  completed: boolean;
}

interface BuilderState {
  mode: BuilderMode;
  sessionId: string | null;
  gymnastId: string;
  gymnastName: string;
  goals: string;
  ragScope: 'gymnast' | 'tenant';
  currentSection: Section | null;
  sections: SectionData[];
  messages: ConversationMessage[];
  currentMessage: string;
  error: string | null;
  programmeId: string | null;
  isTyping: boolean;
}

const AVAILABLE_SECTIONS: Section[] = [
  'Bars',
  'Vault',
  'Floor',
  'Beam',
  'Strength & Conditioning',
];

export function ProgrammeBuilderPage() {
  const { logout } = useAuth();
  const navigate = useNavigate();
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const [state, setState] = useState<BuilderState>({
    mode: 'input',
    sessionId: null,
    gymnastId: '',
    gymnastName: '',
    goals: '',
    ragScope: 'gymnast',
    currentSection: null,
    sections: [],
    messages: [],
    currentMessage: '',
    error: null,
    programmeId: null,
    isTyping: false,
  });

  const [gymnasts, setGymnasts] = useState<GymnastResponse[]>([]);
  const [loadingGymnasts, setLoadingGymnasts] = useState(true);

  // Auto-scroll to bottom of conversation
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [state.messages, state.isTyping]);

  // Fetch gymnasts on mount
  useEffect(() => {
    const fetchGymnasts = async () => {
      try {
        const data = await apiClient.listGymnasts();
        setGymnasts(data);
      } catch (err) {
        console.error('Failed to fetch gymnasts:', err);
      } finally {
        setLoadingGymnasts(false);
      }
    };

    fetchGymnasts();
  }, []);

  const handleStartSession = useCallback(async () => {
    setState(prev => ({ ...prev, mode: 'loading', error: null, isTyping: true }));

    try {
      const selectedGymnast = gymnasts.find(g => g.id === state.gymnastId);

      const request: StartProgrammeBuilderRequest = {
        gymnastId: state.gymnastId,
        goals: `Create a training programme for ${selectedGymnast?.name || 'the gymnast'} with the following goals: ${state.goals}\n\nIMPORTANT: This will be a section-based programme. Ask the coach which section they want to work on first from: Bars, Vault, Floor, Beam, or Strength & Conditioning.`,
        ragScope: state.ragScope,
      };

      const result: BuilderSessionResult = await apiClient.startProgrammeBuilder(request);

      setState(prev => ({
        ...prev,
        mode: 'section-select',
        sessionId: result.sessionId,
        gymnastName: selectedGymnast?.name || 'Gymnast',
        messages: [
          { role: 'assistant', content: result.suggestion, timestamp: new Date() },
        ],
        isTyping: false,
      }));
    } catch (err) {
      setState(prev => ({
        ...prev,
        mode: 'input',
        error: err instanceof Error ? err.message : 'Failed to start session',
        isTyping: false,
      }));
    }
  }, [state.gymnastId, state.goals, state.ragScope, gymnasts]);

  const handleSelectSection = useCallback(async (section: Section) => {
    setState(prev => ({
      ...prev,
      mode: 'loading',
      currentSection: section,
      error: null,
      isTyping: true,
      messages: [
        ...prev.messages,
        { role: 'user', content: `Let's work on ${section}`, timestamp: new Date(), section },
      ],
    }));

    try {
      const result = await apiClient.continueProgrammeBuilder(
        state.sessionId!,
        { message: `Create a detailed training plan for ${section}. Include specific exercises, sets, reps, and progression notes.` }
      );

      setState(prev => ({
        ...prev,
        mode: 'conversation',
        messages: [
          ...prev.messages,
          { role: 'assistant', content: result.suggestion, timestamp: new Date(), section },
        ],
        isTyping: false,
      }));
    } catch (err) {
      setState(prev => ({
        ...prev,
        mode: 'section-select',
        error: err instanceof Error ? err.message : 'Failed to generate section',
        isTyping: false,
      }));
    }
  }, [state.sessionId]);

  const handleCompleteSection = useCallback(() => {
    if (!state.currentSection) return;

    const latestAssistantMessage = [...state.messages]
      .reverse()
      .find(m => m.role === 'assistant' && m.section === state.currentSection);

    const newSection: SectionData = {
      section: state.currentSection,
      content: latestAssistantMessage?.content || '',
      completed: true,
    };

    setState(prev => ({
      ...prev,
      mode: 'section-select',
      sections: [...prev.sections, newSection],
      currentSection: null,
      messages: [
        ...prev.messages,
        {
          role: 'assistant',
          content: `Great! ${newSection.section} is complete. Which section would you like to work on next?${prev.sections.length + 1 >= AVAILABLE_SECTIONS.length ? ' Or would you like to preview and accept the programme?' : ''}`,
          timestamp: new Date(),
        },
      ],
    }));
  }, [state.currentSection, state.messages]);

  const handleSendMessage = useCallback(async () => {
    if (!state.currentMessage.trim()) return;

    const userMessage = state.currentMessage;
    setState(prev => ({
      ...prev,
      mode: 'loading',
      currentMessage: '',
      isTyping: true,
      messages: [
        ...prev.messages,
        { role: 'user', content: userMessage, timestamp: new Date(), section: prev.currentSection || undefined },
      ],
    }));

    try {
      const result = await apiClient.continueProgrammeBuilder(
        state.sessionId!,
        { message: userMessage }
      );

      setState(prev => ({
        ...prev,
        mode: 'conversation',
        messages: [
          ...prev.messages,
          { role: 'assistant', content: result.suggestion, timestamp: new Date(), section: prev.currentSection || undefined },
        ],
        isTyping: false,
      }));
    } catch (err) {
      setState(prev => ({
        ...prev,
        mode: 'conversation',
        error: err instanceof Error ? err.message : 'Failed to send message',
        isTyping: false,
      }));
    }
  }, [state.currentMessage, state.sessionId]);

  const handlePreview = useCallback(() => {
    setState(prev => ({ ...prev, mode: 'preview' }));
  }, []);

  const handleBackToEditing = useCallback(() => {
    setState(prev => ({ ...prev, mode: 'section-select' }));
  }, []);

  const handleAcceptProgramme = useCallback(async () => {
    setState(prev => ({ ...prev, mode: 'loading', isTyping: true }));
    try {
      const programmeId = await apiClient.acceptProgrammeBuilder(state.sessionId!);

      setState(prev => ({ ...prev, mode: 'success', programmeId, isTyping: false }));

      setTimeout(() => navigate('/dashboard'), 2000);
    } catch (err) {
      setState(prev => ({
        ...prev,
        mode: 'preview',
        error: err instanceof Error ? err.message : 'Failed to accept programme',
        isTyping: false,
      }));
    }
  }, [state.sessionId, navigate]);

  const handleLogout = async () => {
    await logout();
    navigate('/sign-in');
  };

  const getRemainingSection = (): Section[] => {
    const completedSections = state.sections.map(s => s.section);
    return AVAILABLE_SECTIONS.filter(s => !completedSections.includes(s) && s !== state.currentSection);
  };

  // Typing indicator component
  const TypingIndicator = () => (
    <div
      style={{
        marginBottom: '1.5rem',
        padding: '1rem',
        borderRadius: '8px',
        borderLeft: '4px solid var(--accent)',
        background: 'rgba(170, 59, 255, 0.05)',
      }}
    >
      <div style={{ fontWeight: 600, marginBottom: '0.5rem', color: 'var(--text-h)' }}>
        🤖 AI Coach
      </div>
      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
        <span>Thinking</span>
        <span className="typing-dots">
          <span>.</span><span>.</span><span>.</span>
        </span>
      </div>
      <style>{`
        @keyframes blink {
          0%, 20% { opacity: 0; }
          40% { opacity: 1; }
          100% { opacity: 0; }
        }
        .typing-dots span {
          animation: blink 1.4s infinite;
        }
        .typing-dots span:nth-child(2) {
          animation-delay: 0.2s;
        }
        .typing-dots span:nth-child(3) {
          animation-delay: 0.4s;
        }
      `}</style>
    </div>
  );

  // Render markdown (fallback to plain text if react-markdown not installed)
  const renderContent = (content: string) => {
    // TODO: Uncomment when react-markdown is installed
    // return <ReactMarkdown>{content}</ReactMarkdown>;

    // Fallback: Basic markdown-like rendering
    return (
      <div style={{ whiteSpace: 'pre-wrap', lineHeight: 1.6 }}>
        {content.split('\n').map((line, i) => {
          // Bold
          if (line.startsWith('**') && line.endsWith('**')) {
            return <strong key={i}>{line.slice(2, -2)}</strong>;
          }
          // Headers
          if (line.startsWith('## ')) {
            return <h3 key={i} style={{ marginTop: '1rem', marginBottom: '0.5rem' }}>{line.slice(3)}</h3>;
          }
          if (line.startsWith('# ')) {
            return <h2 key={i} style={{ marginTop: '1rem', marginBottom: '0.5rem' }}>{line.slice(2)}</h2>;
          }
          // List items
          if (line.startsWith('- ')) {
            return <li key={i} style={{ marginLeft: '1.5rem' }}>{line.slice(2)}</li>;
          }
          return <div key={i}>{line || <br />}</div>;
        })}
      </div>
    );
  };

  // Preview Mode
  if (state.mode === 'preview') {
    return (
      <div className="container-wide">
        <header>
          <h1>Programme Preview</h1>
          <button onClick={handleLogout}>Logout</button>
        </header>

        <nav style={{ marginBottom: '20px', padding: '10px', background: 'rgba(170, 59, 255, 0.05)', border: '1px solid var(--accent-border)', borderRadius: '8px', display: 'flex', justifyContent: 'space-between' }}>
          <button
            onClick={handleBackToEditing}
            style={{ background: 'none', border: 'none', color: 'var(--accent)', textDecoration: 'none', fontWeight: 500, cursor: 'pointer', padding: '0', fontSize: '1rem' }}
          >
            ← Back to Editing
          </button>
        </nav>

        <div style={{ marginBottom: '1.5rem', padding: '1rem', background: 'rgba(170, 59, 255, 0.05)', border: '1px solid var(--accent-border)', borderRadius: '8px' }}>
          <div><strong>Gymnast:</strong> {state.gymnastName}</div>
          <div><strong>Goals:</strong> {state.goals}</div>
          <div><strong>Completed Sections:</strong> {state.sections.length} of {AVAILABLE_SECTIONS.length}</div>
        </div>

        {state.error && (
          <div className="error" style={{ marginBottom: '1.5rem' }} role="alert">
            <strong>Error:</strong> {state.error}
          </div>
        )}

        <div style={{ marginBottom: '2rem' }}>
          {state.sections.map((section, idx) => (
            <div key={idx} style={{ marginBottom: '2rem', padding: '1.5rem', border: '1px solid var(--border)', borderRadius: '8px', background: 'var(--bg)' }}>
              <h2 style={{ marginTop: 0, marginBottom: '1rem', color: 'var(--accent)' }}>{section.section}</h2>
              <div>{renderContent(section.content)}</div>
            </div>
          ))}
        </div>

        <div style={{ position: 'sticky', bottom: 0, background: 'var(--bg)', padding: '1rem', border: '1px solid var(--border)', borderRadius: '8px', display: 'flex', gap: '1rem', justifyContent: 'center' }}>
          <button
            onClick={handleBackToEditing}
            style={{ padding: '0.75rem 1.5rem', fontSize: '1rem', borderRadius: '4px', background: '#666', color: 'white', border: 'none' }}
          >
            Continue Editing
          </button>
          <button
            onClick={handleAcceptProgramme}
            style={{ padding: '0.75rem 1.5rem', fontSize: '1rem', borderRadius: '4px' }}
          >
            ✓ Accept Programme
          </button>
        </div>
      </div>
    );
  }

  // Loading Mode
  if (state.mode === 'loading' && state.messages.length === 0) {
    return (
      <div className="container-wide">
        <header>
          <h1>Programme Builder</h1>
          <button onClick={handleLogout}>Logout</button>
        </header>
        <div style={{ padding: '2rem', textAlign: 'center' }}>
          <div style={{ fontSize: '1.2rem', marginBottom: '1rem' }}>
            Starting your programme builder session...
          </div>
          <div style={{ color: 'var(--text)', opacity: 0.7 }}>
            This may take a few moments
          </div>
        </div>
      </div>
    );
  }

  // Success Mode
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

  // Section Select or Conversation Mode
  if (state.mode === 'section-select' || state.mode === 'conversation' || (state.mode === 'loading' && state.messages.length > 0)) {
    const remainingSections = getRemainingSection();
    const canPreview = state.sections.length > 0;
    const allSectionsComplete = state.sections.length >= AVAILABLE_SECTIONS.length;

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
          <div><strong>Gymnast:</strong> {state.gymnastName}</div>
          <div><strong>Goals:</strong> {state.goals}</div>
          <div style={{ marginTop: '0.5rem' }}>
            <strong>Progress:</strong> {state.sections.length} of {AVAILABLE_SECTIONS.length} sections complete
            {state.sections.length > 0 && (
              <span style={{ marginLeft: '1rem', fontSize: '0.9rem' }}>
                ({state.sections.map(s => s.section).join(', ')})
              </span>
            )}
          </div>
        </div>

        {state.error && (
          <div className="error" style={{ marginBottom: '1.5rem' }} role="alert">
            <strong>Error:</strong> {state.error}
          </div>
        )}

        <div style={{ maxHeight: '50vh', overflowY: 'auto', padding: '1rem', background: 'var(--bg)', border: '1px solid var(--border)', borderRadius: '8px', marginBottom: '1.5rem' }}>
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
              <div style={{ fontWeight: 600, marginBottom: '0.5rem', color: 'var(--text-h)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span>{msg.role === 'assistant' ? '🤖 AI Coach' : '💬 You'}</span>
                {msg.section && <span style={{ fontSize: '0.8rem', opacity: 0.7 }}>{msg.section}</span>}
              </div>
              <div>{renderContent(msg.content)}</div>
              <div style={{ fontSize: '0.875rem', color: 'var(--text)', opacity: 0.7, marginTop: '0.5rem' }}>
                {msg.timestamp.toLocaleTimeString()}
              </div>
            </div>
          ))}
          {state.isTyping && <TypingIndicator />}
          <div ref={messagesEndRef} />
        </div>

        {state.mode === 'section-select' && remainingSections.length > 0 && (
          <div style={{ marginBottom: '1.5rem', padding: '1rem', border: '1px solid var(--border)', borderRadius: '8px' }}>
            <h3 style={{ marginTop: 0, marginBottom: '1rem' }}>Select Next Section:</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem' }}>
              {remainingSections.map(section => (
                <button
                  key={section}
                  onClick={() => handleSelectSection(section)}
                  style={{
                    padding: '1rem',
                    fontSize: '1rem',
                    borderRadius: '8px',
                    background: 'rgba(170, 59, 255, 0.08)',
                    color: 'var(--accent)',
                    border: '2px solid var(--accent)',
                    cursor: 'pointer',
                    fontWeight: 600,
                  }}
                >
                  {section}
                </button>
              ))}
            </div>
          </div>
        )}

        {state.mode === 'conversation' && state.currentSection && (
          <div style={{ marginBottom: '1.5rem', padding: '1rem', border: '1px solid var(--border)', borderRadius: '8px' }}>
            <div style={{ marginBottom: '1rem' }}>
              <strong>Working on:</strong> {state.currentSection}
            </div>
            <label htmlFor="refine-message" style={{ display: 'block', marginBottom: '0.5rem', fontWeight: 500 }}>
              Refine this section
            </label>
            <textarea
              id="refine-message"
              placeholder="Request changes (e.g., 'Add more plyometric exercises')"
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
                onClick={handleCompleteSection}
                style={{ padding: '0.75rem 1.5rem', fontSize: '1rem', borderRadius: '4px' }}
              >
                ✓ Complete {state.currentSection}
              </button>
            </div>
          </div>
        )}

        {canPreview && (
          <div style={{ display: 'flex', gap: '1rem', justifyContent: 'center' }}>
            <button
              onClick={handlePreview}
              style={{ padding: '0.75rem 1.5rem', fontSize: '1rem', borderRadius: '4px', background: 'var(--accent)', color: 'white', border: 'none' }}
            >
              {allSectionsComplete ? '→ Preview & Accept Programme' : '👁️ Preview Programme'}
            </button>
          </div>
        )}
      </div>
    );
  }

  // Input Mode (default)
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

      <div style={{ marginBottom: '1.5rem', padding: '1rem', background: 'rgba(52, 152, 219, 0.1)', border: '1px solid rgba(52, 152, 219, 0.3)', borderRadius: '8px' }}>
        <strong>ℹ️ Section-Based Building</strong>
        <p style={{ marginTop: '0.5rem', marginBottom: 0 }}>
          You'll build your programme section by section: Bars, Vault, Floor, Beam, and Strength & Conditioning.
          The AI will help you create each section individually.
        </p>
      </div>

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
          <label htmlFor="goals">Overall Training Goals *</label>
          <textarea
            id="goals"
            rows={6}
            placeholder="Describe the overall goals for this programme (e.g., 'Improve vault power and landing stability, develop bar strength and consistency')"
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
          Start Building Programme →
        </button>
      </form>
    </div>
  );
}
