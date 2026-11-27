import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { setupServer } from 'msw/node';
import { removerSessao } from '../sessao/armazenamento';
import { reiniciarRenovador } from '../sessao/renovador';
import { handlersPadrao } from './handlers';

/** Servidor MSW compartilhado: requisição não prevista falha o teste (research R3). */
export const servidor = setupServer(...handlersPadrao);

beforeAll(() => servidor.listen({ onUnhandledFrame: 'error' }));

afterEach(() => {
  cleanup();
  servidor.resetHandlers();
  sessionStorage.clear();
  removerSessao(); // a sessão vive em memória (specs/016, FR-005)
  reiniciarRenovador();
  vi.useRealTimers();
});

afterAll(() => servidor.close());
