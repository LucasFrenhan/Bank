import { useCallback, useRef, useState } from 'react';
import type { Resultado } from '../api/cliente';
import type { MensagemErro } from '../api/erros';
import { chaveDaOperacao, descartarChave } from './chaves';

/** Respostas depois das quais a mesma chave não serve mais (research R6). */
const ENCERRAM = new Set([
  'idempotencia-divergente',
  'cobranca-nao-pendente',
  'cobranca-nao-encontrada',
  'fatura-nao-disponivel',
  'fatura-nao-encontrada',
  'recebivel-nao-antecipavel',
  'recebivel-nao-encontrado',
]);

/**
 * Envio de uma operação financeira com a chave de idempotência da aba: um envio por vez, mesma chave em reenvios até o
 * sucesso (specs/015-frontend-cartao-credito, FR-016, FR-020, FR-022; SC-004).
 */
export function useOperacao(operacao: string) {
  const [enviando, definirEnviando] = useState(false);
  const [erro, definirErro] = useState<MensagemErro | null>(null);
  const enviandoRef = useRef(false);

  const executar = useCallback(
    async <T>(chamada: (chave: string) => Promise<Resultado<T>>): Promise<Resultado<T> | null> => {
      if (enviandoRef.current) {
        return null;
      }

      enviandoRef.current = true;
      definirEnviando(true);
      definirErro(null);
      const resultado = await chamada(chaveDaOperacao(operacao));
      enviandoRef.current = false;
      definirEnviando(false);

      if (resultado.ok || ENCERRAM.has(resultado.erro.codigo)) {
        descartarChave(operacao);
      }

      if (!resultado.ok && !resultado.erro.encerrarSessao) {
        definirErro(resultado.erro);
      }

      return resultado;
    },
    [operacao],
  );

  return { enviando, erro, definirErro, executar };
}
