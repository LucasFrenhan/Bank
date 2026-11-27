import { limparOperacoes } from '../operacoes/chaves';
import { limparDadosDeTransferencia } from '../transferencia/armazenamento';
import { decodificarClaims, type ClaimsSessao } from './claims';

/**
 * Sessão só em memória (specs/016-sessao-refresh-token, FR-005; research R9): o token de acesso nunca vai para
 * `sessionStorage`, `localStorage`, URL ou logs. Recarregar a página perde a memória; a sessão volta pela
 * renovação, com a credencial no cookie `HttpOnly`.
 */

/** Chave das versões anteriores (spec 004), removida na primeira carga. */
export const CHAVE_SESSAO_LEGADA = 'bank.sessao';

/** Antecedência máxima do aviso de expiração (FR-016b). */
export const ANTECEDENCIA_AVISO_MS = 60_000;

export interface PrazosSessao {
  sessaoExpiraPorInatividadeEmSegundos: number | string;
  sessaoExpiraEmSegundos: number | string;
}

export interface Sessao extends ClaimsSessao {
  token: string;
  /** Fim da sessão sem nova renovação (epoch ms). */
  inatividadeAte: number;
  /** Limite absoluto da sessão (epoch ms). */
  absolutoAte: number;
  /** Quanto antes do fim o aviso aparece: 60 s, ou metade do prazo de inatividade se ele for menor que 2 min. */
  antecedenciaAvisoMs: number;
}

let memoria: Sessao | null = null;

export function criarSessao(token: string, prazos: PrazosSessao, agora = Date.now()): Sessao | null {
  const claims = decodificarClaims(token);
  if (!claims) {
    return null;
  }

  return { ...claims, token, ...calcularPrazos(prazos, agora) };
}

/** Prazos locais a partir dos segundos da resposta (não depende do relógio do navegador estar certo). */
export function calcularPrazos(prazos: PrazosSessao, agora = Date.now()) {
  const inatividadeMs = Number(prazos.sessaoExpiraPorInatividadeEmSegundos) * 1000;
  const absolutoMs = Number(prazos.sessaoExpiraEmSegundos) * 1000;
  return {
    inatividadeAte: agora + inatividadeMs,
    absolutoAte: agora + absolutoMs,
    antecedenciaAvisoMs: Math.min(ANTECEDENCIA_AVISO_MS, inatividadeMs / 2),
  };
}

export function salvarSessao(sessao: Sessao): void {
  memoria = sessao;
}

export function lerSessao(): Sessao | null {
  return memoria;
}

export function obterToken(): string | null {
  return memoria?.token ?? null;
}

/** Fim da sessão: memória, transferência em andamento e chaves de idempotência (só aqui, nunca na renovação). */
export function removerSessao(): void {
  memoria = null;

  // A transferência em andamento e os comprovantes vivem no escopo da sessão (specs/007, FR-010).
  limparDadosDeTransferencia();
  // Chaves de idempotência de pagamentos e antecipações (specs/015, research R6).
  limparOperacoes();
}

/** Remove o token gravado por versões anteriores (spec 004) sem tocar nos dados da operação. */
export function limparSessaoLegada(): void {
  try {
    sessionStorage.removeItem(CHAVE_SESSAO_LEGADA);
  } catch {
    // Armazenamento indisponível: nada a limpar.
  }
}
