import { useKeycloak } from '@react-keycloak/web';
import { BrowserRouter, Routes, Route, Link, Navigate } from 'react-router-dom';
import { SyncUsersPage } from './pages/SyncUsersPage';
import './App.css';

function HomePage() {
  const { keycloak } = useKeycloak();
  const token = keycloak.tokenParsed;
  const tenantId = token?.tenant_id;
  const username = token?.preferred_username;
  const email = token?.email;
  const roles = token?.realm_access?.roles || [];

  return (
    <div className="container">
      <header>
        <h1 style={{ marginRight: '1rem', flex: 1 }}>Gymnastics Platform - Admin Portal</h1>
        <button onClick={() => keycloak.logout()} style={{ flexShrink: 0 }}>
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

        <section style={{ marginTop: '30px' }}>
          <h2>Admin Tools</h2>
          <ul>
            <li><Link to="/sync-users">Sync User Tenants</Link> - Sync user tenant_id from database to Keycloak</li>
          </ul>
        </section>
      </main>
    </div>
  );
}

function App() {
  const { keycloak, initialized } = useKeycloak();

  if (!initialized) {
    return <div>Loading...</div>;
  }

  if (!keycloak.authenticated) {
    return (
      <div className="container">
        <h1>Gymnastics Platform - Admin Portal</h1>
        <p>Please log in with your administrator credentials.</p>
        <button onClick={() => keycloak.login()}>
          Login
        </button>
      </div>
    );
  }

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/sync-users" element={<SyncUsersPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
