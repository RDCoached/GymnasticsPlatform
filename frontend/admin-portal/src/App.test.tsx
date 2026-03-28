import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import App from './App';

// Mock Keycloak
vi.mock('@react-keycloak/web', () => ({
  useKeycloak: () => ({
    keycloak: {
      authenticated: false,
      login: vi.fn(),
    },
    initialized: false,
  }),
}));

describe('App', () => {
  it('should render loading state when Keycloak is not initialized', () => {
    render(<App />);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });
});
