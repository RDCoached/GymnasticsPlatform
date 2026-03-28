import { render, screen } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useKeycloak } from '@react-keycloak/web';
import App from './App';

vi.mock('@react-keycloak/web');

describe('App', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();

    // Default mock - Keycloak initialized but not authenticated
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        authenticated: false,
        login: vi.fn(),
        logout: vi.fn(),
      },
      initialized: true,
    } as never);
  });

  it('should render without crashing when not authenticated', () => {
    render(<App />);
    // Just verify it renders - the router will redirect to sign-in
    expect(document.body).toBeInTheDocument();
  });

  it('should render without crashing when authenticated', () => {
    localStorage.setItem('accessToken', 'fake-token');
    localStorage.setItem('refreshToken', 'fake-refresh-token');
    localStorage.setItem('user', JSON.stringify({
      email: 'test@example.com',
      fullName: 'Test User',
      onboardingCompleted: true
    }));

    render(<App />);
    // Just verify it renders
    expect(document.body).toBeInTheDocument();
  });
});
