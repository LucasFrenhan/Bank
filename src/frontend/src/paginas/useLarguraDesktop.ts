import { useSyncExternalStore } from 'react';

/** Mesmo ponto de quebra do `Layout` e dos tokens (≥ 1024 px = desktop). */
export const CONSULTA_DESKTOP = '(min-width: 1024px)';

function assinar(aoMudar: () => void): () => void {
  if (typeof window.matchMedia !== 'function') {
    return () => undefined;
  }

  const consulta = window.matchMedia(CONSULTA_DESKTOP);
  consulta.addEventListener('change', aoMudar);
  return () => consulta.removeEventListener('change', aoMudar);
}

function ler(): boolean {
  return typeof window.matchMedia === 'function' ? window.matchMedia(CONSULTA_DESKTOP).matches : true;
}

/**
 * Layout do extrato (specs/007, research R7): tabela com páginas no desktop, lista agrupada com
 * "Carregar mais" no mobile. Sem `matchMedia` (testes), assume desktop.
 */
export function useLarguraDesktop(): boolean {
  return useSyncExternalStore(assinar, ler, () => true);
}
