import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeAll, vi } from 'vitest';
import { ReactNode } from 'react';

// Mock localStorage
class LocalStorageMock {
  private store: Record<string, string> = {};

  clear() {
    this.store = {};
  }

  getItem(key: string) {
    return this.store[key] || null;
  }

  setItem(key: string, value: string) {
    this.store[key] = value;
  }

  removeItem(key: string) {
    delete this.store[key];
  }
}

beforeAll(() => {
  global.localStorage = new LocalStorageMock() as Storage;

  // Mock ReactKeycloakProvider to just render children
  vi.mock('@react-keycloak/web', async () => {
    const actual = await vi.importActual('@react-keycloak/web');
    return {
      ...actual,
      ReactKeycloakProvider: ({ children }: { children: ReactNode }) => children,
      useKeycloak: vi.fn(),
    };
  });
});

// Cleanup after each test
afterEach(() => {
  cleanup();
  localStorage.clear();
});
