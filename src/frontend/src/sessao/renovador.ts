/**
 * Uma renovação por vez (specs/016-sessao-refresh-token, FR-015; research R9): na aba, chamadas concorrentes
 * aguardam a mesma promessa; entre abas, a renovação roda dentro de uma trava da Web Locks API, então o navegador
 * nunca apresenta a mesma credencial duas vezes ao mesmo tempo. Sem Web Locks, vale só o voo único da aba (a
 * tolerância do servidor cobre o resto).
 */

export const NOME_TRAVA = 'bank.renovacao';

let emVoo: Promise<boolean> | null = null;

export function renovarUmaVez(executar: () => Promise<boolean>): Promise<boolean> {
  if (emVoo) {
    return emVoo;
  }

  const atual = comTrava(executar).finally(() => {
    if (emVoo === atual) emVoo = null;
  });
  emVoo = atual;
  return atual;
}

async function comTrava(executar: () => Promise<boolean>): Promise<boolean> {
  const travas = typeof navigator === 'undefined' ? undefined : navigator.locks;
  if (!travas?.request) {
    return executar();
  }

  return travas.request(NOME_TRAVA, () => executar());
}

/** Só para testes: esquece uma renovação pendente. */
export function reiniciarRenovador(): void {
  emVoo = null;
}
