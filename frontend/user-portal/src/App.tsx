import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ReactKeycloakProvider, useKeycloak } from '@react-keycloak/web';
import keycloak from './keycloak';
import { OnboardingGuard } from './components/OnboardingGuard';
import { OnboardingScreen } from './pages/OnboardingScreen';
import { Dashboard } from './pages/Dashboard';
import { UpdateProfilePage } from './pages/UpdateProfilePage';
import { SignInPage } from './pages/SignInPage';
import { RegisterPage } from './pages/RegisterPage';
import './App.css';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: {
    email: string;
    fullName: string;
    onboardingCompleted: boolean;
  } | null;
}

function AppContent() {
  const { keycloak: keycloakInstance, initialized: keycloakInitialized } = useKeycloak();
  const [authState, setAuthState] = useState<AuthState>({
    accessToken: localStorage.getItem('accessToken'),
    refreshToken: localStorage.getItem('refreshToken'),
    user: localStorage.getItem('user') ? JSON.parse(localStorage.getItem('user')!) : null,
  });

  const handleLoginSuccess = (tokens: { accessToken: string; refreshToken: string; user: { email: string; fullName: string; onboardingCompleted: boolean } }) => {
    localStorage.setItem('accessToken', tokens.accessToken);
    localStorage.setItem('refreshToken', tokens.refreshToken);
    localStorage.setItem('user', JSON.stringify(tokens.user));
    setAuthState({
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      user: tokens.user,
    });
  };

  const handleLogout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
    keycloakInstance.logout();
  };

  if (!keycloakInitialized) {
    return <div>Loading...</div>;
  }

  // Check if authenticated via Keycloak (Google OAuth) or localStorage (email/password)
  const isAuthenticated = keycloakInstance.authenticated || (authState.accessToken && authState.user);

  if (!isAuthenticated) {
    return (
      <BrowserRouter>
        <Routes>
          <Route path="/sign-in" element={<SignInPage onLoginSuccess={handleLoginSuccess} />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="*" element={<Navigate to="/sign-in" replace />} />
        </Routes>
      </BrowserRouter>
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
        <Route
          path="/profile"
          element={
            <OnboardingGuard>
              <UpdateProfilePage />
            </OnboardingGuard>
          }
        />
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

function App() {
  return (
    <ReactKeycloakProvider
      authClient={keycloak}
      initOptions={{
        onLoad: 'check-sso',
        checkLoginIframe: false,
      }}
    >
      <AppContent />
    </ReactKeycloakProvider>
  );
}

export default App;
