import { createContext, use, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { api, integrarSessao } from '../api/cliente';
import type { MensagemErro } from '../api/erros';
import { criarSessao, lerSessao, removerSessao, salvarSessao, type Sessao } from './armazenamento';

/**
 * Estado da sessão (US2; FR-009 a FR-013; research R5): login, saída e expiração por tempo ou
 * por 401 da API. O redirecionamento para o login fica só em `RotaProtegida`, que recebe o
 * motivo do encerramento e a rota de retorno.
 */

/** `expirada`: token vencido ou recusado (401); `conta`: a conta não existe mais (404). */
export type MotivoEncerramento = 'expirada' | 'conta';

export interface ContextoSessaoValor {
  sessao: Sessao | null;
  /** Motivo do último encerramento involuntário (exibido na tela de login). */
  motivoEncerramento: MotivoEncerramento | null;
  entrar: (email: string, senha: string) => Promise<{ ok: true } | { ok: false; erro: MensagemErro }>;
  sair: () => void;
  /** Encerra a sessão; a rota protegida leva ao login com o aviso correspondente. */
  expirar: (motivo?: MotivoEncerramento) => void;
}

const ContextoSessao = createContext<ContextoSessaoValor | null>(null);

export const ROTA_ENTRAR = '/entrar';

export function ProvedorSessao({ children }: { children: ReactNode }) {
  const [sessao, definirSessao] = useState<Sessao | null>(() => lerSessao());
  const [motivoEncerramento, definirMotivo] = useState<MotivoEncerramento | null>(null);
  // Atualizada nos próprios callbacks (não durante a renderização).
  const sessaoRef = useRef(sessao);

  const encerrar = useCallback((motivo: MotivoEncerramento | null) => {
    removerSessao();
    sessaoRef.current = null;
    definirMotivo(motivo);
    definirSessao(null);
  }, []);

  const expirar = useCallback(
    (motivo: MotivoEncerramento = 'expirada') => {
      if (sessaoRef.current) {
        encerrar(motivo);
      }
    },
    [encerrar],
  );

  const sair = useCallback(() => encerrar(null), [encerrar]);

  const entrar = useCallback(async (email: string, senha: string) => {
    const resultado = await api.entrar(email, senha);
    if (!resultado.ok) {
      return resultado;
    }

    const nova = criarSessao(resultado.dados.tokenAcesso, Number(resultado.dados.expiraEmSegundos));
    if (!nova) {
      return { ok: false as const, erro: { codigo: 'inesperado', mensagem: 'Algo deu errado' } };
    }

    salvarSessao(nova);
    sessaoRef.current = nova;
    definirMotivo(null);
    definirSessao(nova);
    return { ok: true as const };
  }, []);

  // Cliente da API: reação a 401 (o token é lido do armazenamento pelo próprio cliente).
  useEffect(() => {
    integrarSessao({ aoNaoAutenticado: () => expirar('expirada') });
  }, [expirar]);

  // Expiração por tempo (US2, cenário 4).
  useEffect(() => {
    if (!sessao) {
      return undefined;
    }

    const temporizador = window.setTimeout(() => expirar('expirada'), Math.max(sessao.expiraEm - Date.now(), 0));
    return () => window.clearTimeout(temporizador);
  }, [sessao, expirar]);

  const valor = useMemo(
    () => ({ sessao, motivoEncerramento, entrar, sair, expirar }),
    [sessao, motivoEncerramento, entrar, sair, expirar],
  );

  return <ContextoSessao value={valor}>{children}</ContextoSessao>;
}

export function useSessao(): ContextoSessaoValor {
  const valor = use(ContextoSessao);
  if (!valor) {
    throw new Error('useSessao precisa estar dentro de ProvedorSessao.');
  }

  return valor;
}

/** Sessão ativa garantida (uso dentro de rotas protegidas). */
export function useSessaoAtiva(): Sessao {
  const { sessao } = useSessao();
  if (!sessao) {
    throw new Error('Rota protegida sem sessão.');
  }

  return sessao;
}
