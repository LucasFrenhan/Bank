import { gerarChave } from '../transferencia/chave';

/**
 * Chave de idempotência por operação (pagar cobrança, pagar fatura, antecipar), guardada na aba e sem dado do cartão
 * (specs/015-frontend-cartao-credito, FR-016; research R6). Sem armazenamento, vale só em memória.
 */

export const CHAVE_OPERACOES = 'bank.operacoes';

let memoria: Record<string, string> = {};

function ler(): Record<string, string> {
  try {
    const bruto = sessionStorage.getItem(CHAVE_OPERACOES);
    const lido: unknown = bruto ? JSON.parse(bruto) : null;
    return { ...memoria, ...(lido !== null && typeof lido === 'object' ? (lido as Record<string, string>) : {}) };
  } catch {
    return { ...memoria };
  }
}

function gravar(chaves: Record<string, string>): void {
  memoria = chaves;
  try {
    sessionStorage.setItem(CHAVE_OPERACOES, JSON.stringify(chaves));
    memoria = {};
  } catch {
    // Armazenamento indisponível: segue em memória.
  }
}

/** A chave da operação; a primeira chamada cria, as demais devolvem a mesma. */
export function chaveDaOperacao(operacao: string): string {
  const chaves = ler();
  const existente = chaves[operacao];
  if (existente) {
    return existente;
  }

  const nova = gerarChave();
  gravar({ ...chaves, [operacao]: nova });
  return nova;
}

/** A próxima tentativa usa uma chave nova. */
export function descartarChave(operacao: string): void {
  const chaves = ler();
  if (operacao in chaves) {
    delete chaves[operacao];
    gravar(chaves);
  }
}

/** Apaga todas (chamado por `removerSessao`). */
export function limparOperacoes(): void {
  memoria = {};
  try {
    sessionStorage.removeItem(CHAVE_OPERACOES);
  } catch {
    // ignorado
  }
}
