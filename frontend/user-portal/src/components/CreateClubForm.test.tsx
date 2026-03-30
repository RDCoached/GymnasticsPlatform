import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { CreateClubForm } from './CreateClubForm';
import { useKeycloak } from '@react-keycloak/web';

vi.mock('@react-keycloak/web');

const API_BASE_URL = 'http://localhost:5137';

describe('CreateClubForm', () => {
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

  it('should render form with club name input', () => {
    render(<CreateClubForm onComplete={mockOnComplete} />);

    expect(screen.getByRole('heading', { name: /create your club/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/club name/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create club/i })).toBeInTheDocument();
  });

  it('should disable button when club name is whitespace-only', async () => {
    render(<CreateClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/club name/i);
    const submitButton = screen.getByRole('button', { name: /create club/i });

    // Button should be disabled when field contains only whitespace
    fireEvent.change(input, { target: { value: '   ' } });

    await waitFor(() => {
      expect(submitButton).toBeDisabled();
    });

    expect(global.fetch).not.toHaveBeenCalled();
    expect(mockOnComplete).not.toHaveBeenCalled();
  });

  it('should call API with club name when form is submitted', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ tenantId: 'new-tenant-id', role: 'organization_owner', clubId: 'club-id' }),
    } as Response);

    render(<CreateClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/club name/i);
    const submitButton = screen.getByRole('button', { name: /create club/i });

    fireEvent.change(input, { target: { value: 'Elite Gymnastics' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        `${API_BASE_URL}/api/onboarding/create-club`,
        {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${mockToken}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ name: 'Elite Gymnastics' }),
        }
      );
    });
  });

  it('should call onComplete after successful API call', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ tenantId: 'new-tenant-id', role: 'organization_owner', clubId: 'club-id' }),
    } as Response);

    render(<CreateClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/club name/i);
    const submitButton = screen.getByRole('button', { name: /create club/i });

    fireEvent.change(input, { target: { value: 'Elite Gymnastics' } });
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

    render(<CreateClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/club name/i);
    const submitButton = screen.getByRole('button', { name: /create club/i });

    fireEvent.change(input, { target: { value: 'Elite Gymnastics' } });
    fireEvent.click(submitButton);

    // Should show loading state
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /creating club\.\.\./i })).toBeInTheDocument();
    });

    // Input should be disabled
    expect(input).toBeDisabled();

    // Resolve the request
    resolveRequest!();
  });

  it('should display API error message when request fails', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: false,
      json: async () => ({ title: 'Club name already exists' }),
    } as Response);

    render(<CreateClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/club name/i);
    const submitButton = screen.getByRole('button', { name: /create club/i });

    fireEvent.change(input, { target: { value: 'Existing Club' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Club name already exists')).toBeInTheDocument();
    });

    expect(mockOnComplete).not.toHaveBeenCalled();
  });

  it('should display generic error when API response is malformed', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      ok: false,
      json: async () => { throw new Error('Invalid JSON'); },
    } as Response);

    render(<CreateClubForm onComplete={mockOnComplete} />);

    const input = screen.getByLabelText(/club name/i);
    const submitButton = screen.getByRole('button', { name: /create club/i });

    fireEvent.change(input, { target: { value: 'Test Club' } });
    fireEvent.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Failed to create club')).toBeInTheDocument();
    });
  });

});
