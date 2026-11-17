/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Fuso fixo para os testes de formatação de data (os workers do Vitest herdam o ambiente).
process.env.TZ ??= 'America/Sao_Paulo';

// Mesma origem por proxy, sem CORS na API nem no coletor (specs/004-frontend-base, research R4).
const apiUrl = process.env.BANK_API_URL ?? 'http://localhost:5080';
const otlpUrl = process.env.BANK_OTLP_URL ?? 'http://localhost:4318';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: apiUrl, changeOrigin: true },
      '/otlp': {
        target: otlpUrl,
        changeOrigin: true,
        rewrite: (caminho) => caminho.replace(/^\/otlp/, ''),
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['src/testes/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: { modules: { classNameStrategy: 'non-scoped' } },
  },
});
