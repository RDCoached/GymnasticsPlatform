import { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useNavigate, useLocation } from 'react-router-dom';
import { ReactKeycloakProvider, useKeycloak } from '@react-keycloak/web';
import keycloak from './keycloak';
import { OnboardingGuard } from './components/OnboardingGuard';
import { OnboardingScreen } from './pages/OnboardingScreen';
import { Dashboard } from './pages/Dashboard';
import { UpdateProfilePage } from './pages/UpdateProfilePage';
import { ClubInvitesPage } from './pages/ClubInvitesPage';
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

function RoutesComponent({
  isAuthenticated,
  handleLoginSuccess
}: {
  isAuthenticated: boolean;
  handleLoginSuccess: (tokens: { accessToken: string; refreshToken: string; user: { email: string; fullName: string; onboardingCompleted: boolean } }) => void;
}) {
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    if (isAuthenticated && (location.pathname === '/sign-in' || location.pathname === '/register')) {
      navigate('/dashboard', { replace: true });
    }
  }, [isAuthenticated, location.pathname, navigate]);

  return (
    <Routes>
        {!isAuthenticated ? (
          <>
            <Route path="/sign-in" element={<SignInPage onLoginSuccess={handleLoginSuccess} />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="*" element={<Navigate to="/sign-in" replace />} />
          </>
        ) : (
          <>
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
            <Route
              path="/club/invites"
              element={
                <OnboardingGuard>
                  <ClubInvitesPage />
                </OnboardingGuard>
              }
            />
            <Route path="/sign-in" element={<Navigate to="/dashboard" replace />} />
            <Route path="/register" element={<Navigate to="/dashboard" replace />} />
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="*" element={<Navigate to="/dashboard" replace />} />
          </>
        )}
      </Routes>
  );
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

  if (!keycloakInitialized) {
    return <div>Loading...</div>;
  }

  const isAuthenticated = keycloakInstance.authenticated || (authState.accessToken && authState.user);

  return (
    <BrowserRouter>
      <RoutesComponent isAuthenticated={isAuthenticated} handleLoginSuccess={handleLoginSuccess} />
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
