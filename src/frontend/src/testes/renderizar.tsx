import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router';
import { Rotas } from '../App';
import { criarSessao, salvarSessao } from '../sessao/armazenamento';
import type { TipoConta } from '../sessao/claims';
import { ProvedorSessao } from '../sessao/ContextoSessao';
import { tokenFalso } from './handlers';

/** Renderiza a aplicação inteira numa rota, opcionalmente com sessão (testes de componentes). */
export function renderizarApp(rota: string, opcoes: { sessao?: TipoConta; expiraEmSegundos?: number } = {}) {
  if (opcoes.sessao) {
    const sessao = criarSessao(tokenFalso(opcoes.sessao), opcoes.expiraEmSegundos ?? 3600);
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
