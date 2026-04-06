import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useNavigate, useLocation } from 'react-router-dom';
import { AuthProvider } from './providers/AuthProvider';
import { useAuth } from './contexts/AuthContext';
import { OnboardingGuard } from './components/OnboardingGuard';
import { OnboardingScreen } from './pages/OnboardingScreen';
import { Dashboard } from './pages/Dashboard';
import { UpdateProfilePage } from './pages/UpdateProfilePage';
import { ClubInvitesPage } from './pages/ClubInvitesPage';
import { GymnastsPage } from './pages/GymnastsPage';
import { ProgrammeBuilderPage } from './pages/ProgrammeBuilderPage';
import { SignInPage } from './pages/SignInPage';
import { RegisterPage } from './pages/RegisterPage';
import './App.css';

function RoutesComponent() {
  const { isAuthenticated } = useAuth();
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
          <Route path="/sign-in" element={<SignInPage />} />
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
          <Route
            path="/gymnasts"
            element={
              <OnboardingGuard>
                <GymnastsPage />
              </OnboardingGuard>
            }
          />
          <Route
            path="/programme-builder"
            element={
              <OnboardingGuard>
                <ProgrammeBuilderPage />
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
  const { isLoading } = useAuth();

  if (isLoading) {
    return <div>Loading...</div>;
  }

  return (
    <BrowserRouter>
      <RoutesComponent />
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
