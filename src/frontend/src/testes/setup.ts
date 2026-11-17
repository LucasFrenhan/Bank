import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { setupServer } from 'msw/node';
import { handlersPadrao } from './handlers';

/** Servidor MSW compartilhado: requisição não prevista falha o teste (research R3). */
export const servidor = setupServer(...handlersPadrao);

beforeAll(() => servidor.listen({ onUnhandledFrame: 'error' }));

afterEach(() => {
  cleanup();
  servidor.resetHandlers();
  sessionStorage.clear();
  vi.useRealTimers();
});

afterAll(() => servidor.close());
