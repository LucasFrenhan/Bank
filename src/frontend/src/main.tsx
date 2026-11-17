import '@fontsource-variable/inter';
import '@fontsource/jetbrains-mono/400.css';
import './design/global.css';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { telemetriaHabilitada } from './telemetria/config';

// Telemetria carregada sob demanda: desligada, o SDK nem entra no bundle inicial (research R7).
if (telemetriaHabilitada(import.meta.env)) {
  void import('./telemetria/telemetria').then(({ iniciarTelemetria }) => iniciarTelemetria(import.meta.env));
}

const raiz = document.getElementById('raiz');
if (!raiz) {
  throw new Error('Elemento raiz não encontrado.');
}

createRoot(raiz).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
