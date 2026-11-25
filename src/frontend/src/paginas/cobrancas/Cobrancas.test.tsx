import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { CODIGO, cobranca, paginado, problema, TRACE_ID } from '../../testes/handlers';
import { renderizarApp } from '../../testes/renderizar';
import { servidor } from '../../testes/setup';

/** Lista de cobranças da PJ (specs/015, US2; FR-011). */

const FILTRO = { parametro: 'status', campo: 'status' };
const lista = (n: number, extra: object = {}) =>
  Array.from({ length: n }, (_, i) => cobranca({ cobrancaId: `id-${i}`, descricao: `Cobrança ${i + 1}`, ...extra }));

function larguraCelular() {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    value: (media: string) => ({ matches: false, media, addEventListener: () => undefined, removeEventListener: () => undefined }),
  });
}

afterEach(() => {
  Reflect.deleteProperty(window, 'matchMedia');
});

describe('Lista de cobranças', () => {
  it('vazia: estado com a ação "Nova cobrança" e as abas', async () => {
    servidor.use(paginado('/api/v1/cobrancas/minhas', []));
    renderizarApp('/cobrancas', { sessao: 'PessoaJuridica' });

    expect(await screen.findByText('Nenhuma cobrança ainda')).toBeInTheDocument();
    for (const link of screen.getAllByRole('link', { name: 'Nova cobrança' })) expect(link).toHaveAttribute('href', '/cobrancas/nova');
    const secoes = screen.getByRole('navigation', { name: 'Seções de cobranças' });
    expect(within(secoes).getByRole('link', { name: 'Cobranças' })).toHaveAttribute('aria-current', 'page');
    expect(within(secoes).getByRole('link', { name: 'Recebíveis' })).toHaveAttribute('href', '/cobrancas/recebiveis');
    await waitFor(() => expect(document.title).toBe('Cobranças · One Bank'));
  });

  it('itens com descrição, situação, valor e link curto; filtro no endereço', async () => {
    servidor.use(
      paginado('/api/v1/cobrancas/minhas', [cobranca(), cobranca({ cobrancaId: 'x', descricao: 'Paga antes', status: 'Paga' })], FILTRO),
    );
    const { usuario } = renderizarApp('/cobrancas', { sessao: 'PessoaJuridica' });

    const regiao = await screen.findByRole('region', { name: 'Lista de cobranças' });
    expect(within(regiao).getAllByRole('link')).toHaveLength(2);
    const primeira = within(regiao).getByRole('link', { name: /Kit 3 Pacotes/ });
    expect(primeira).toHaveAttribute('href', `/c/${CODIGO}`);
    expect(primeira).toHaveTextContent(/Pendente.*R\$\s299,90/);

    await usuario.click(screen.getByRole('button', { name: 'Pagas' }));
    expect(await screen.findByRole('link', { name: /Paga antes/ })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('link', { name: /Kit 3 Pacotes/ })).not.toBeInTheDocument());
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/cobrancas?situacao=pagas');

    await usuario.click(screen.getByRole('button', { name: 'Canceladas' }));
    expect(await screen.findByText('Nenhuma cobrança nesta situação')).toBeInTheDocument();
  });

  it('páginas no desktop, mantidas no endereço', async () => {
    servidor.use(paginado('/api/v1/cobrancas/minhas', lista(25), FILTRO));
    const { usuario } = renderizarApp('/cobrancas?situacao=pendentes', { sessao: 'PessoaJuridica' });
    await screen.findByText('Página 1 de 2 · 20 por página');
    await usuario.click(screen.getByRole('button', { name: 'Próxima' }));

    expect(await screen.findByText('Cobrança 25')).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/cobrancas?situacao=pendentes&pagina=2');
  });

  it('"Carregar mais" no celular', async () => {
    larguraCelular();
    servidor.use(paginado('/api/v1/cobrancas/minhas', lista(21)));
    const { usuario } = renderizarApp('/cobrancas', { sessao: 'PessoaJuridica' });
    await usuario.click(await screen.findByRole('button', { name: 'Carregar mais' }));

    expect(await screen.findByText('Cobrança 21')).toBeInTheDocument();
  });

  it('erro com código de suporte', async () => {
    servidor.use(http.get('*/api/v1/cobrancas/minhas', () => problema('x', 500)));
    renderizarApp('/cobrancas', { sessao: 'PessoaJuridica' });
    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
  });

  it.each(['/cobrancas', '/cobrancas/nova'])('PF em %s: área indisponível, sem chamar a API', async (rota) => {
    let chamadas = 0;
    servidor.use(http.get('*/api/v1/cobrancas/minhas', () => { chamadas++; return HttpResponse.json({}); }));
    renderizarApp(rota, { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Área indisponível para o seu tipo de conta')).toBeInTheDocument();
    expect(screen.getByText('Cobranças e recebíveis são para contas Pessoa Jurídica.')).toBeInTheDocument();
    expect(chamadas).toBe(0);
  });
});
