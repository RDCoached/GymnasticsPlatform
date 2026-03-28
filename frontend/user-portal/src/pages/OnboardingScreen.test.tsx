import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { OnboardingScreen } from './OnboardingScreen';
import { useKeycloak } from '@react-keycloak/web';
import { useOnboardingComplete } from '../hooks/useOnboardingComplete';

vi.mock('@react-keycloak/web');
vi.mock('../hooks/useOnboardingComplete');
vi.mock('../components/CreateClubForm', () => ({
  CreateClubForm: ({ onComplete }: any) => (
    <div data-testid="create-club-form">
      <button onClick={onComplete}>Complete Create Club</button>
    </div>
  ),
}));
vi.mock('../components/JoinClubForm', () => ({
  JoinClubForm: ({ onComplete }: any) => (
    <div data-testid="join-club-form">
      <button onClick={onComplete}>Complete Join Club</button>
    </div>
  ),
}));

const API_BASE_URL = 'http://localhost:5001';

describe('OnboardingScreen', () => {
  const mockToken = 'mock-jwt-token';
  const mockComplete = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: mockToken,
      },
    } as any);
    vi.mocked(useOnboardingComplete).mockReturnValue({
      complete: mockComplete,
      isLoading: false,
    });
    global.fetch = vi.fn();
  });

  it('should render three onboarding options', () => {
    render(<OnboardingScreen />);

    expect(screen.getByRole('heading', { name: /welcome to gymnastics platform/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /create a club/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /join a club/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /individual mode/i })).toBeInTheDocument();
  });

  it('should show CreateClubForm when Create Club option is clicked', () => {
    render(<OnboardingScreen />);

    const createButton = screen.getByRole('button', { name: /create club/i });
    fireEvent.click(createButton);

    expect(screen.getByTestId('create-club-form')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument();
  });

  it('should show JoinClubForm when Join Club option is clicked', () => {
    render(<OnboardingScreen />);

    const joinButton = screen.getByRole('button', { name: /join club/i });
    fireEvent.click(joinButton);

    expect(screen.getByTestId('join-club-form')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /back/i })).toBeInTheDocument();
  });

  it('should return to option selection when back button is clicked', () => {
    render(<OnboardingScreen />);

    // Navigate to Create Club form
    const createButton = screen.getByRole('button', { name: /create club/i });
    fireEvent.click(createButton);

    expect(screen.getByTestId('create-club-form')).toBeInTheDocument();

    // Click back button
    const backButton = screen.getByRole('button', { name: /back/i });
    fireEvent.click(backButton);

    // Should show options again
    expect(screen.getByRole('heading', { name: /create a club/i })).toBeInTheDocument();
    expect(screen.queryByTestId('create-club-form')).not.toBeInTheDocument();
  });

  it('should call API and complete when Individual Mode is clicked', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ tenantId: 'individual-tenant-id', role: 'individual' }),
    } as Response);

    render(<OnboardingScreen />);

    const individualButton = screen.getByRole('button', { name: /go individual/i });
    fireEvent.click(individualButton);

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        `${API_BASE_URL}/api/onboarding/individual`,
        {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${mockToken}`,
            'Content-Type': 'application/json',
          },
        }
      );
    });

    expect(mockComplete).toHaveBeenCalled();
  });

  it('should show loading state when onboarding is completing', () => {
    vi.mocked(useOnboardingComplete).mockReturnValue({
      complete: mockComplete,
      isLoading: true,
    });

    render(<OnboardingScreen />);

    expect(screen.getByRole('heading', { name: /setting up your account/i })).toBeInTheDocument();
    expect(screen.getByText(/please wait while we complete your setup/i)).toBeInTheDocument();
  });

  it('should not call complete when individual mode API call fails', async () => {
    const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    window.alert = vi.fn();

    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: false,
      status: 500,
    } as Response);

    render(<OnboardingScreen />);

    const individualButton = screen.getByRole('button', { name: /go individual/i });
    fireEvent.click(individualButton);

    await waitFor(() => {
      expect(window.alert).toHaveBeenCalledWith('Failed to complete onboarding. Please try again.');
    });

    expect(mockComplete).not.toHaveBeenCalled();
    expect(consoleErrorSpy).toHaveBeenCalled();

    consoleErrorSpy.mockRestore();
  });

  it('should pass complete function to CreateClubForm', () => {
    render(<OnboardingScreen />);

    const createButton = screen.getByRole('button', { name: /create club/i });
    fireEvent.click(createButton);

    const completeButton = screen.getByRole('button', { name: /complete create club/i });
    fireEvent.click(completeButton);

    expect(mockComplete).toHaveBeenCalled();
  });

  it('should pass complete function to JoinClubForm', () => {
    render(<OnboardingScreen />);

    const joinButton = screen.getByRole('button', { name: /join club/i });
    fireEvent.click(joinButton);

    const completeButton = screen.getByRole('button', { name: /complete join club/i });
    fireEvent.click(completeButton);

    expect(mockComplete).toHaveBeenCalled();
  });
});
