import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { JoinClubForm } from './JoinClubForm';
import { useKeycloak } from '@react-keycloak/web';

vi.mock('@react-keycloak/web');

const API_BASE_URL = 'http://localhost:5001';

describe('JoinClubForm', () => {
  const mockOnComplete = vi.fn();
  const mockToken = 'mock-jwt-token';

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useKeycloak).mockReturnValue({
      keycloak: {
        token: mockToken,
      },
    } as never);
    global.fetch = vi.fn();
  });

  it('should render form with invite code input', () => {
    render(<JoinClubForm onComplete={mockOnComplete} />);

    expect(screen.getByRole('heading', { name: /join a club/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/invite code/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /join club/i })).toBeInTheDocument();
  });

  it('should disable button when invite code is whitespace-only', async () => {
    render(<JoinClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/invite code/i);
    const submitButton = screen.getByRole('button', { name: /join club/i });

    // Button should be disabled when field contains only whitespace
    fireEvent.change(input, { target: { value: '   ' } });

    await waitFor(() => {
      expect(submitButton).toBeDisabled();
    });

    expect(global.fetch).not.toHaveBeenCalled();
    expect(mockOnComplete).not.toHaveBeenCalled();
  });

  it('should call API with invite code when form is submitted', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ tenantId: 'club-tenant-id', role: 'member', clubId: 'club-id' }),
    } as Response);

    render(<JoinClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/invite code/i);
    const submitButton = screen.getByRole('button', { name: /join club/i });

    fireEvent.change(input, { target: { value: 'ABC123' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        `${API_BASE_URL}/api/onboarding/join-club`,
        {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${mockToken}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ inviteCode: 'ABC123' }),
        }
      );
    });
  });

  it('should convert invite code to uppercase', async () => {
    render(<JoinClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/invite code/i) as HTMLInputElement;

    fireEvent.change(input, { target: { value: 'abc123' } });

    expect(input.value).toBe('ABC123');
  });

  it('should call onComplete after successful API call', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ tenantId: 'club-tenant-id', role: 'member', clubId: 'club-id' }),
    } as Response);

    render(<JoinClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/invite code/i);
    const submitButton = screen.getByRole('button', { name: /join club/i });

    fireEvent.change(input, { target: { value: 'VALID123' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(mockOnComplete).toHaveBeenCalled();
    });
  });

  it('should show loading state while submitting', async () => {
    let resolveRequest: () => void;
    const requestPromise = new Promise<Response>((resolve) => {
      resolveRequest = () => resolve({
        ok: true,
        json: async () => ({}),
      } as Response);
    });

    vi.mocked(global.fetch).mockReturnValueOnce(requestPromise as never);

    render(<JoinClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/invite code/i);
    const submitButton = screen.getByRole('button', { name: /join club/i });

    fireEvent.change(input, { target: { value: 'ABC123' } });
    fireEvent.click(submitButton);

    // Should show loading state
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /joining club\.\.\./i })).toBeInTheDocument();
    });

    // Input should be disabled
    expect(input).toBeDisabled();

    // Resolve the request
    resolveRequest!();
  });

  it('should display API error message when invite code is invalid', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: false,
      json: async () => ({ title: 'Invalid invite code' }),
    } as Response);

    render(<JoinClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/invite code/i);
    const submitButton = screen.getByRole('button', { name: /join club/i });

    fireEvent.change(input, { target: { value: 'INVALID' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Invalid invite code')).toBeInTheDocument();
    });

    expect(mockOnComplete).not.toHaveBeenCalled();
  });

  it('should display generic error when API response is malformed', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: false,
      json: async () => { throw new Error('Invalid JSON'); },
    } as Response);

    render(<JoinClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/invite code/i);
    const submitButton = screen.getByRole('button', { name: /join club/i });

    fireEvent.change(input, { target: { value: 'TEST123' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Invalid invite code')).toBeInTheDocument();
    });
  });

  it('should trim whitespace from invite code before sending', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ tenantId: 'club-tenant-id' }),
    } as Response);

    render(<JoinClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/invite code/i);
    const submitButton = screen.getByRole('button', { name: /join club/i });

    fireEvent.change(input, { target: { value: '  ABC123  ' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          body: JSON.stringify({ inviteCode: 'ABC123' }),
        })
      );
    });
  });
});
