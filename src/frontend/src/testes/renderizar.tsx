import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { Rotas } from '../App';
import { criarSessao, salvarSessao } from '../sessao/armazenamento';
import type { TipoConta } from '../sessao/claims';
import { ProvedorSessao } from '../sessao/ContextoSessao';
import { PRAZOS_SESSAO, tokenFalso } from './handlers';

export interface OpcoesRenderizacao {
  /** Sessão já em memória (como numa navegação dentro do app, sem recarregar a página). */
  sessao?: TipoConta;
  /** Prazos da sessão em segundos (padrão: 15 min de inatividade e 8 h de limite absoluto). */
  inatividadeEmSegundos?: number;
  absolutoEmSegundos?: number;
}

/** Renderiza a aplicação inteira numa rota, opcionalmente com sessão (testes de componentes). */
export function renderizarApp(rota: string, opcoes: OpcoesRenderizacao = {}) {
  if (opcoes.sessao) {
    const sessao = criarSessao(tokenFalso(opcoes.sessao), {
      sessaoExpiraPorInatividadeEmSegundos: opcoes.inatividadeEmSegundos ?? PRAZOS_SESSAO.sessaoExpiraPorInatividadeEmSegundos,
      sessaoExpiraEmSegundos: opcoes.absolutoEmSegundos ?? PRAZOS_SESSAO.sessaoExpiraEmSegundos,
    });
    if (sessao) salvarSessao(sessao);
  }

  const usuario = userEvent.setup();
  const resultado = render(
    <MemoryRouter initialEntries={[rota]}>
      <ProvedorSessao>
        <Rotas />
        <LocalAtual />
      </ProvedorSessao>
    </MemoryRouter>,
  );

  return { usuario, ...resultado };
}

/** Expõe a rota atual no DOM para asserções. */
function LocalAtual() {
  const local = useLocation();
  return <output data-testid="rota-atual" hidden>{local.pathname + local.search}</output>;
}

/** Como {@link renderizarApp}, mas espera a verificação da sessão terminar (telas públicas; specs/016, FR-017). */
export async function renderizarAppPronto(rota: string, opcoes: OpcoesRenderizacao = {}) {
  const resultado = renderizarApp(rota, opcoes);
  await waitFor(() => expect(screen.queryByText('Carregando sua sessão…')).not.toBeInTheDocument());
  return resultado;
}
