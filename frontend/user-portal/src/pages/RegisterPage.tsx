import { useState, FormEvent, useEffect } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

export function RegisterPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { register, loginWithOAuth, isLoading } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  // Extract and store invite code from URL
  useEffect(() => {
    const inviteCodeFromUrl = searchParams.get('inviteCode');
    if (inviteCodeFromUrl) {
      // Store in localStorage to survive Keycloak redirect
      localStorage.setItem('pendingInviteCode', inviteCodeFromUrl);
    }
  }, [searchParams]);

  const validatePassword = (pwd: string): string | null => {
    if (pwd.length < 8) {
      return 'Password must be at least 8 characters';
    }
    if (!/[A-Z]/.test(pwd)) {
      return 'Password must contain at least one uppercase letter';
    }
    if (!/[0-9]/.test(pwd)) {
      return 'Password must contain at least one digit';
    }
    if (!/[!@#$%^&*(),.?":{}|<>]/.test(pwd)) {
      return 'Password must contain at least one special character';
    }
    return null;
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess(false);

    const passwordError = validatePassword(password);
    if (passwordError) {
      setError(passwordError);
      return;
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    try {
      await register(email, password, fullName);
      setSuccess(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed');
    }
  };

  const handleGoogleSignup = async () => {
    setError('');
    try {
      await loginWithOAuth('google');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Google sign-up failed');
    }
  };

  return (
    <div className="onboarding-container">
      <div className="onboarding-header">
        <h1>Create Account</h1>
        <p>Sign up to get started with Gymnastics Platform.</p>
      </div>

      <div className="form-container">
        {error && <div className="error-message" role="alert">{error}</div>}
        {success && (
          <div className="success-message" role="alert">
            <strong>Registration successful!</strong>
            <p style={{ marginTop: '0.5rem' }}>
              Please check your email to verify your account before signing in.
            </p>
            <p style={{ marginTop: '0.5rem', fontSize: '0.9rem', opacity: 0.9 }}>
              In development: Open{' '}
              <a
                href="http://localhost:8025"
                target="_blank"
                rel="noopener noreferrer"
                style={{ color: 'inherit', textDecoration: 'underline' }}
              >
                MailHog
              </a>
              {' '}to view the verification email.
            </p>
            <Link
              to="/sign-in"
              className="accent-link"
              style={{ display: 'inline-block', marginTop: '1rem' }}
            >
              Go to Sign In →
            </Link>
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate style={{ display: success ? 'none' : 'block' }}>
          <div className="form-group">
            <label htmlFor="fullName">Full Name</label>
            <input
              id="fullName"
              type="text"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              required
              disabled={isLoading}
            />
          </div>

          <div className="form-group">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              disabled={isLoading}
            />
          </div>

          <div className="form-group">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              disabled={isLoading}
            />
            <small className="password-hint">
              Min 8 chars, 1 uppercase, 1 digit, 1 special character
            </small>
          </div>

          <div className="form-group">
            <label htmlFor="confirmPassword">Confirm Password</label>
            <input
              id="confirmPassword"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              disabled={isLoading}
            />
          </div>

          <button type="submit" disabled={isLoading} className="submit-button">
            {isLoading ? 'Creating account...' : 'Register'}
          </button>
        </form>

        <div className="auth-link-section">
          <div className="oauth-divider">
            <div className="divider-line"></div>
            <span className="divider-text">OR</span>
            <div className="divider-line"></div>
          </div>

          <button
            type="button"
            onClick={handleGoogleSignup}
            className="oauth-button"
            disabled={isLoading}
          >
            <svg width="18" height="18" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48">
              <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/>
              <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/>
              <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"/>
              <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/>
              <path fill="none" d="M0 0h48v48H0z"/>
            </svg>
            Continue with Google
          </button>

          <p>
            Already have an account?{' '}
            <Link to="/sign-in" className="accent-link">
              Sign in here
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
