import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';
import App from './App';

describe('App', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('should redirect to sign-in page when not authenticated', async () => {
    render(<App />);

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /sign in/i })).toBeInTheDocument();
    });
  });

  it('should show app when authenticated', async () => {
    localStorage.setItem('accessToken', 'fake-token');
    localStorage.setItem('refreshToken', 'fake-refresh-token');
    localStorage.setItem('user', JSON.stringify({
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true
    }));

    render(<App />);

    await waitFor(() => {
      expect(screen.queryByRole('heading', { name: /sign in/i })).not.toBeInTheDocument();
    });
  });
});
