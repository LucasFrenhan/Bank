import { useCallback, useEffect, useRef, useState } from 'react';
import { api, CODIGO_CANCELADA, type DadosCartao } from '../api/cliente';
import type { MensagemErro } from '../api/erros';

/** Tempo máximo com os dados à mostra (specs/015, FR-007). */
export const TEMPO_EXIBICAO_MS = 60_000;

export type EstadoDadosCartao =
  | { estado: 'oculto' }
  | { estado: 'buscando' }
  | { estado: 'exibido'; dados: DadosCartao }
  | { estado: 'erro'; erro: MensagemErro };

/**
 * Número e CVV só em memória e só enquanto exibidos (specs/015-frontend-cartao-credito, FR-007 e FR-008; research R5):
 * buscados no pedido e apagados ao ocultar, sair da tela, esconder a aba ou depois de 1 minuto. Uma resposta que chega
 * depois de ocultar é descartada.
 */
export function useDadosCartao() {
  const [estado, definirEstado] = useState<EstadoDadosCartao>({ estado: 'oculto' });
  const pedido = useRef<AbortController | null>(null);

  const ocultar = useCallback(() => {
    pedido.current?.abort();
    pedido.current = null;
    definirEstado({ estado: 'oculto' });
  }, []);

  const exibir = useCallback(async () => {
    pedido.current?.abort();
    const atual = new AbortController();
    pedido.current = atual;
    definirEstado({ estado: 'buscando' });

    const resultado = await api.obterDadosCartao(atual.signal);
    if (atual.signal.aborted) return;
    pedido.current = null;
    if (resultado.ok) {
      definirEstado({ estado: 'exibido', dados: resultado.dados });
    } else if (resultado.erro.codigo !== CODIGO_CANCELADA && !resultado.erro.encerrarSessao) {
      definirEstado({ estado: 'erro', erro: resultado.erro });
    }
  }, []);

  useEffect(() => {
    const aoEsconder = () => {
      if (document.visibilityState === 'hidden') ocultar();
    };
    document.addEventListener('visibilitychange', aoEsconder);
    // `pagehide`: a página não pode voltar do cache do navegador com os dados à mostra.
    window.addEventListener('pagehide', ocultar);
    return () => {
      document.removeEventListener('visibilitychange', aoEsconder);
      window.removeEventListener('pagehide', ocultar);
      pedido.current?.abort();
      pedido.current = null;
    };
  }, [ocultar]);

  const exibido = estado.estado === 'exibido';
  useEffect(() => {
    if (!exibido) return undefined;
    const temporizador = window.setTimeout(ocultar, TEMPO_EXIBICAO_MS);
    return () => window.clearTimeout(temporizador);
  }, [exibido, ocultar]);

  return { estado, exibir, ocultar };
}
