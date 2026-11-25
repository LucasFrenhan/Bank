import { useEffect, useRef } from 'react';

export const INTERVALO_ATUALIZACAO_MS = 5_000;

/**
 * Chama `atualizar` a cada 5 s enquanto `ativa` e com a aba visível, sem sobrepor chamadas (specs/015, FR-012; research
 * R7). Ao voltar para a aba, atualiza na hora.
 */
export function useAtualizacaoAutomatica(ativa: boolean, atualizar: () => Promise<unknown>): void {
  const atualizarRef = useRef(atualizar);
  useEffect(() => {
    atualizarRef.current = atualizar;
  });

  useEffect(() => {
    if (!ativa) return undefined;
    let emAndamento = false;

    const executar = async () => {
      if (emAndamento || document.visibilityState !== 'visible') return;
      emAndamento = true;
      await atualizarRef.current();
      emAndamento = false;
    };
    const temporizador = window.setInterval(() => void executar(), INTERVALO_ATUALIZACAO_MS);
    const aoMudar = () => void executar();
    document.addEventListener('visibilitychange', aoMudar);
    return () => {
      window.clearInterval(temporizador);
      document.removeEventListener('visibilitychange', aoMudar);
    };
  }, [ativa]);
}
