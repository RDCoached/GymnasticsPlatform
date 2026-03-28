import { useKeycloak } from '@react-keycloak/web';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { OnboardingGuard } from './components/OnboardingGuard';
import { OnboardingScreen } from './pages/OnboardingScreen';
import { Dashboard } from './pages/Dashboard';
import './App.css';

function App() {
  const { keycloak, initialized } = useKeycloak();

  if (!initialized) {
    return <div>Loading...</div>;
  }

  if (!keycloak.authenticated) {
    return (
      <div className="container">
        <h1>Gymnastics Platform - User Portal</h1>
        <p>Please log in to access your account.</p>
        <button onClick={() => keycloak.login()}>
          Login
        </button>
      </div>
    );
  }

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/onboarding" element={<OnboardingScreen />} />
        <Route
          path="/dashboard"
          element={
            <OnboardingGuard>
              <Dashboard />
            </OnboardingGuard>
          }
        />
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
