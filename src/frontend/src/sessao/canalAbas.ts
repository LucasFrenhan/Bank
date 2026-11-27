/**
 * Sessão compartilhada entre as abas (specs/016-sessao-refresh-token, FR-016, FR-016b; research R10). As mensagens
 * nunca levam o token: só o `sub` (não é segredo) e os prazos em segundos.
 */

export const NOME_CANAL = 'bank.sessao';

export type MensagemSessao =
  | { tipo: 'entrou'; sub: string }
  | { tipo: 'renovou'; sessaoExpiraPorInatividadeEmSegundos: number; sessaoExpiraEmSegundos: number }
  | { tipo: 'saiu' }
  | { tipo: 'expirou' };

export interface CanalSessao {
  publicar: (mensagem: MensagemSessao) => void;
  fechar: () => void;
}

/** Canal entre abas; sem `BroadcastChannel`, vira um canal que não faz nada. */
export function abrirCanal(aoReceber: (mensagem: MensagemSessao) => void): CanalSessao {
  if (typeof BroadcastChannel === 'undefined') {
    return { publicar: () => undefined, fechar: () => undefined };
  }

  const canal = new BroadcastChannel(NOME_CANAL);
  canal.onmessage = (evento: MessageEvent<MensagemSessao>) => aoReceber(evento.data);

  return {
    publicar: (mensagem) => {
      try {
        canal.postMessage(mensagem);
      } catch {
        // Canal fechado (aba saindo): nada a fazer.
      }
    },
    fechar: () => canal.close(),
  };
}
