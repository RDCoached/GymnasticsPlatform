import { render } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useAuth } from './contexts/AuthContext';
import App from './App';

vi.mock('./contexts/AuthContext');

describe('App', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    // Default mock - not authenticated
    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: false,
      isLoading: false,
      user: null,
      login: vi.fn(),
      loginWithOAuth: vi.fn(),
      logout: vi.fn(),
      register: vi.fn(),
      getToken: vi.fn(() => null),
    });
  });

  it('should render without crashing when not authenticated', () => {
    render(<App />);
    // Just verify it renders - the router will redirect to sign-in
    expect(document.body).toBeInTheDocument();
  });

  it('should render without crashing when authenticated', () => {
    vi.mocked(useAuth).mockReturnValue({
      isAuthenticated: true,
      isLoading: false,
      user: {
        id: 'test-id',
        email: 'test@example.com',
        fullName: 'Test User',
        onboardingCompleted: true,
      },
      login: vi.fn(),
      loginWithOAuth: vi.fn(),
      logout: vi.fn(),
      register: vi.fn(),
      getToken: vi.fn(() => 'fake-token'),
    });

    render(<App />);
    // Just verify it renders
    expect(document.body).toBeInTheDocument();
  });
});
