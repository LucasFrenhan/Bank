import { createContext, use, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { api, integrarSessao, type SessaoEmitida } from '../api/cliente';
import type { MensagemErro } from '../api/erros';
import {
  calcularPrazos,
  criarSessao,
  lerSessao,
  limparSessaoLegada,
  removerSessao,
  salvarSessao,
  type Sessao,
} from './armazenamento';
import { abrirCanal, type CanalSessao, type MensagemSessao } from './canalAbas';
import { renovarUmaVez } from './renovador';

/**
 * Estado da sessão (spec 004, US2; specs/016-sessao-refresh-token, FR-014 a FR-019): login, renovação sob demanda
 * (401, recarga, "Continuar conectado"), saída no servidor e sessão compartilhada entre as abas. O
 * redirecionamento para o login fica só em `RotaProtegida`, que recebe o motivo do encerramento e a rota de
 * retorno.
 */

/** `expirada`: sessão vencida ou recusada; `conta`: a conta não existe mais (404). */
export type MotivoEncerramento = 'expirada' | 'conta';

export interface ContextoSessaoValor {
  sessao: Sessao | null;
  /** `true` enquanto a sessão é recuperada pela renovação ao abrir ou recarregar a página (FR-017). */
  verificando: boolean;
  /** Motivo do último encerramento involuntário (exibido na tela de login). */
  motivoEncerramento: MotivoEncerramento | null;
  entrar: (email: string, senha: string) => Promise<{ ok: true } | { ok: false; erro: MensagemErro }>;
  /** Renova agora (uma por vez); `true` se a sessão continua. */
  renovar: () => Promise<boolean>;
  sair: () => Promise<void>;
  /** Encerra a sessão; a rota protegida leva ao login com o aviso correspondente. */
  expirar: (motivo?: MotivoEncerramento) => void;
}

const ContextoSessao = createContext<ContextoSessaoValor | null>(null);

export const ROTA_ENTRAR = '/entrar';

export function ProvedorSessao({ children }: { children: ReactNode }) {
  const [sessao, definirSessao] = useState<Sessao | null>(() => lerSessao());
  const [verificando, definirVerificando] = useState(() => lerSessao() === null);
  const [motivoEncerramento, definirMotivo] = useState<MotivoEncerramento | null>(null);
  // Atualizada nos próprios callbacks (não durante a renderização).
  const sessaoRef = useRef(sessao);
  // Muda a cada encerramento: uma renovação que termina depois de "Sair" não restabelece a sessão.
  const geracaoRef = useRef(0);
  const canalRef = useRef<CanalSessao | null>(null);

  const aplicar = useCallback((nova: Sessao | null) => {
    if (nova) salvarSessao(nova);
    sessaoRef.current = nova;
    definirSessao(nova);
  }, []);

  const encerrar = useCallback(
    (motivo: MotivoEncerramento | null) => {
      geracaoRef.current += 1;
      removerSessao();
      aplicar(null);
      definirMotivo(motivo);
    },
    [aplicar],
  );

  const assumir = useCallback(
    (dados: SessaoEmitida): Sessao | null => {
      const nova = criarSessao(dados.tokenAcesso, dados);
      if (!nova) return null;
      if (sessaoRef.current && sessaoRef.current.usuarioId !== nova.usuarioId) {
        removerSessao(); // outra pessoa entrou neste navegador: nada da sessão anterior fica
      }

      aplicar(nova);
      return nova;
    },
    [aplicar],
  );

  const renovar = useCallback(
    () =>
      renovarUmaVez(async () => {
        const geracao = geracaoRef.current;
        const resultado = await api.renovar();
        if (geracao !== geracaoRef.current) {
          return false; // "Sair" durante a renovação: o logout prevalece
        }

        if (!resultado.ok || !assumir(resultado.dados)) {
          return false;
        }

        canalRef.current?.publicar({
          tipo: 'renovou',
          sessaoExpiraPorInatividadeEmSegundos: Number(resultado.dados.sessaoExpiraPorInatividadeEmSegundos),
          sessaoExpiraEmSegundos: Number(resultado.dados.sessaoExpiraEmSegundos),
        });
        return true;
      }),
    [assumir],
  );

  const expirar = useCallback(
    (motivo: MotivoEncerramento = 'expirada') => {
      if (sessaoRef.current) {
        encerrar(motivo);
        canalRef.current?.publicar({ tipo: 'expirou' });
      }
    },
    [encerrar],
  );

  const sair = useCallback(async () => {
    encerrar(null);
    canalRef.current?.publicar({ tipo: 'saiu' });
    await api.sair(); // encerra no servidor; a sessão local já foi descartada, mesmo com falha de rede
  }, [encerrar]);

  const entrar = useCallback(
    async (email: string, senha: string) => {
      const resultado = await api.entrar(email, senha);
      if (!resultado.ok) {
        return resultado;
      }

      const nova = assumir(resultado.dados);
      if (!nova) {
        return { ok: false as const, erro: { codigo: 'inesperado', mensagem: 'Algo deu errado' } };
      }

      definirMotivo(null);
      canalRef.current?.publicar({ tipo: 'entrou', sub: nova.usuarioId });
      return { ok: true as const };
    },
    [assumir],
  );

  // Cliente da API: renovação e repetição no 401; sem renovação possível, a sessão expira.
  useEffect(() => {
    integrarSessao({ renovar, aoNaoAutenticado: () => expirar('expirada') });
  }, [renovar, expirar]);

  // Mensagens das outras abas (FR-016).
  useEffect(() => {
    const aoReceber = (mensagem: MensagemSessao) => {
      switch (mensagem.tipo) {
        case 'entrou':
          if (sessaoRef.current && sessaoRef.current.usuarioId !== mensagem.sub) {
            encerrar(null);
          }

          if (!sessaoRef.current) void renovar();
          break;
        case 'renovou':
          if (sessaoRef.current) aplicar({ ...sessaoRef.current, ...calcularPrazos(mensagem) });
          break;
        case 'saiu':
          if (sessaoRef.current) encerrar(null);
          break;
        case 'expirou':
          if (sessaoRef.current) encerrar('expirada');
          break;
      }
    };

    const canal = abrirCanal(aoReceber);
    canalRef.current = canal;
    return () => {
      canal.fechar();
      canalRef.current = null;
    };
  }, [aplicar, encerrar, renovar]);

  // Abrir ou recarregar a página: a sessão volta pela renovação (FR-017).
  useEffect(() => {
    if (!verificando) return;
    limparSessaoLegada();
    let ativo = true;
    void renovar().finally(() => {
      if (ativo) definirVerificando(false);
    });
    return () => {
      ativo = false;
    };
  }, [verificando, renovar]);

  const valor = useMemo(
    () => ({ sessao, verificando, motivoEncerramento, entrar, renovar, sair, expirar }),
    [sessao, verificando, motivoEncerramento, entrar, renovar, sair, expirar],
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
