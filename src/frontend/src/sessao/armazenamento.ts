import { decodificarClaims, type ClaimsSessao } from './claims';

/**
 * Sessão restrita à aba, em `sessionStorage` (FR-013; research R5). O token nunca é exibido,
 * nunca vai para a URL nem para logs.
 */

export const CHAVE_SESSAO = 'bank.sessao';

export interface Sessao extends ClaimsSessao {
  token: string;
  /** Instante de expiração (epoch ms). */
  expiraEm: number;
}

export function criarSessao(token: string, expiraEmSegundos: number, agora = Date.now()): Sessao | null {
  const claims = decodificarClaims(token);
  return claims ? { ...claims, token, expiraEm: agora + expiraEmSegundos * 1000 } : null;
}

export function salvarSessao(sessao: Sessao): void {
  try {
    sessionStorage.setItem(CHAVE_SESSAO, JSON.stringify({ token: sessao.token, expiraEm: sessao.expiraEm }));
  } catch {
    // Armazenamento indisponível (modo privado restrito): a sessão vale só enquanto a página estiver aberta.
  }
}

/** Lê a sessão guardada; vencida, ilegível ou sem claims é descartada. */
export function lerSessao(agora = Date.now()): Sessao | null {
  let bruto: string | null = null;
  try {
    bruto = sessionStorage.getItem(CHAVE_SESSAO);
  } catch {
    return null;
  }

  if (!bruto) {
    return null;
  }

  try {
    const { token, expiraEm } = JSON.parse(bruto) as { token?: unknown; expiraEm?: unknown };
    const claims = typeof token === 'string' ? decodificarClaims(token) : null;

    if (!claims || typeof token !== 'string' || typeof expiraEm !== 'number' || expiraEm <= agora) {
      removerSessao();
      return null;
    }

    return { ...claims, token, expiraEm };
  } catch {
    removerSessao();
    return null;
  }
}

export function removerSessao(): void {
  try {
    sessionStorage.removeItem(CHAVE_SESSAO);
  } catch {
    // ignorado
  }
}
