import { useCallback, useState } from 'react';

export interface AvisoCopia {
  texto: string;
  falhou: boolean;
}

const SEM_AVISO: AvisoCopia = { texto: '', falhou: false };

/**
 * Copia um texto e informa o resultado (specs/013, research R4). O aviso é limpo antes de cada
 * cópia, para que a repetição seja anunciada de novo. Sem área de transferência no navegador, a
 * chamada lança e conta como falha.
 */
export function useCopia() {
  const [aviso, definirAviso] = useState<AvisoCopia>(SEM_AVISO);

  const copiar = useCallback(async (texto: string, mensagens: { sucesso: string; falha: string }) => {
    definirAviso(SEM_AVISO);
    try {
      await navigator.clipboard.writeText(texto);
      definirAviso({ texto: mensagens.sucesso, falhou: false });
    } catch {
      definirAviso({ texto: mensagens.falha, falhou: true });
    }
  }, []);

  return { aviso, copiar };
}
