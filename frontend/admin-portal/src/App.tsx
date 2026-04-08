import { BrowserRouter, Routes, Route, Link, Navigate } from 'react-router-dom';
import { AuthProvider } from './providers/AuthProvider';
import { useAuth } from './contexts/AuthContext';
import { SyncUsersPage } from './pages/SyncUsersPage';
import { AuthCallbackPage } from './pages/AuthCallbackPage';
import './App.css';

function HomePage() {
  const { user, logout } = useAuth();

  return (
    <div className="container">
      <header>
        <h1 style={{ marginRight: '1rem', flex: 1 }}>Gymnastics Platform - Admin Portal</h1>
        <button onClick={logout} style={{ flexShrink: 0 }}>
          Logout
        </button>
      </header>

      <nav style={{
        marginBottom: '20px',
        padding: '15px',
        background: 'rgba(170, 59, 255, 0.05)',
        border: '1px solid var(--accent-border)',
        borderRadius: '8px'
      }}>
        <Link to="/" style={{
          marginRight: '20px',
          color: 'var(--accent)',
          textDecoration: 'none',
          fontWeight: 500
        }}>Home</Link>
        <Link to="/sync-users" style={{
          color: 'var(--accent)',
          textDecoration: 'none',
          fontWeight: 500
        }}>Sync Users</Link>
      </nav>

      <main>
        <section className="user-info">
          <h2>Administrator Information</h2>
          <dl>
            <dt>Email:</dt>
            <dd>{user?.email}</dd>

            <dt>Name:</dt>
            <dd>{user?.fullName}</dd>

            <dt>User ID:</dt>
            <dd>{user?.id}</dd>
          </dl>
        </section>

        <section style={{ marginTop: '30px' }}>
          <h2>Admin Tools</h2>
          <ul>
            <li><Link to="/sync-users">Sync User Tenants</Link> - Sync user tenant_id from database to Entra ID</li>
          </ul>
        </section>
      </main>
    </div>
  );
}

function LoginPage() {
  const { login } = useAuth();

  return (
    <div className="container">
      <h1>Gymnastics Platform - Admin Portal</h1>
      <p>Please log in with your administrator credentials.</p>
      <button onClick={() => login('', '')}>
        Login
      </button>
    </div>
  );
}

function AppContent() {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return <div>Loading...</div>;
  }

  return (
    <BrowserRouter>
      <Routes>
        {/* Auth callback route - accessible to all */}
        <Route path="/auth/callback" element={<AuthCallbackPage />} />

        {!isAuthenticated ? (
          <>
            <Route path="/login" element={<LoginPage />} />
            <Route path="*" element={<Navigate to="/login" replace />} />
          </>
        ) : (
          <>
            <Route path="/" element={<HomePage />} />
            <Route path="/sync-users" element={<SyncUsersPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </>
        )}
      </Routes>
    </BrowserRouter>
  );
}

function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );
}

export default App;
