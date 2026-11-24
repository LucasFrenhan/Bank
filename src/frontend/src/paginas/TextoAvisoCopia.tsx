import estilos from './Autenticado.module.css';
import type { AvisoCopia } from './useCopia';

/** Resultado de uma cópia, anunciado a leitores de tela; a falha usa o estilo de erro (specs/013, FR-007). */
export function TextoAvisoCopia({ aviso }: { aviso: AvisoCopia }) {
  return (
    <p className={[estilos.avisoCopia, aviso.falhou ? estilos.avisoFalha : ''].join(' ')} role="status" aria-live="polite">
      {aviso.texto}
    </p>
  );
}
