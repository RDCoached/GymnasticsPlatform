import { useState, FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { apiClient } from '../lib/api-client';

export function RegisterPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

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

    const passwordError = validatePassword(password);
    if (passwordError) {
      setError(passwordError);
      return;
    }

    if (password !== confirmPassword) {
      setError('Passwords do not match');
      return;
    }

    setLoading(true);

    try {
      await apiClient.register({ email, password, fullName });
      setSuccess(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Registration failed');
    } finally {
      setLoading(false);
    }
  };

  if (success) {
    return (
      <div className="onboarding-container">
        <div className="form-container success-container">
          <h2>Registration Successful!</h2>
          <p className="success-message">
            Please check your email to verify your account before signing in.
          </p>
          <p className="dev-hint">
            (In development, check MailHog at{' '}
            <a href="http://localhost:8025" target="_blank" rel="noopener noreferrer" className="accent-link">
              http://localhost:8025
            </a>
            )
          </p>
          <button
            onClick={() => navigate('/sign-in')}
            className="submit-button"
          >
            Go to Sign In
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="onboarding-container">
      <div className="onboarding-header">
        <h1>Create Account</h1>
        <p>Sign up to get started with Gymnastics Platform.</p>
      </div>

      <div className="form-container">
        {error && <div className="error-message">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="fullName">Full Name</label>
            <input
              id="fullName"
              type="text"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              required
              disabled={loading}
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
              disabled={loading}
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
              disabled={loading}
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
              disabled={loading}
            />
          </div>

          <button type="submit" disabled={loading} className="submit-button">
            {loading ? 'Creating account...' : 'Register'}
          </button>
        </form>

        <div className="auth-link-section">
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
