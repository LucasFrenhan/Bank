import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { guardarComprovante } from '../transferencia/armazenamento';
import { carteira, comprovante, extratoDe, ITEM_ABERTURA, itemExtrato, problema, TRACE_ID } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';

/** Extrato (specs/007: US4; FR-015 a FR-020; research R7). */

function simularLargura(desktop: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    value: (consulta: string) => ({
      matches: desktop,
      media: consulta,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    }),
  });
}

function registrarConsultas() {
  const consultas: string[] = [];
  servidor.events.on('request:start', ({ request }) => {
    if (request.url.includes('/transferencias/extrato')) consultas.push(new URL(request.url).search);
  });
  return consultas;
}

afterEach(() => {
  Reflect.deleteProperty(window, 'matchMedia');
  servidor.events.removeAllListeners();
});

describe('Extrato — desktop (US4)', () => {
  beforeEach(() => simularLargura(true));

  it('título, saldo atual e itens na ordem da API, com abertura e "Sem descrição"', async () => {
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => HttpResponse.json({ ...carteira(), saldo: 842.5 })),
      extratoDe([itemExtrato(1, { descricao: null }), itemExtrato(2), ITEM_ABERTURA]),
    );
    renderizarApp('/extrato', { sessao: 'PessoaFisica' });

    expect(await screen.findByRole('heading', { level: 1, name: 'Extrato' })).toBeInTheDocument();
    expect(await screen.findByText(/Saldo atual R\$\s842,50/)).toBeInTheDocument();

    const tabela = await screen.findByRole('table', { name: 'Lançamentos' });
    const linhas = within(tabela).getAllByRole('row').slice(1);
    expect(linhas).toHaveLength(3);
    expect(linhas[0]).toHaveTextContent('Contraparte 1');
    expect(linhas[0]).toHaveTextContent('Saída');
    expect(linhas[0]).toHaveTextContent('Sem descrição');
    expect(linhas[0]).toHaveTextContent(/−\sR\$\s2,00/);
    expect(linhas[1]).toHaveTextContent('Entrada');
    expect(linhas[1]).toHaveTextContent(/\+\sR\$\s3,00/);
    expect(linhas[2]).toHaveTextContent('Saldo inicial');
    expect(linhas[2]).toHaveTextContent('Saldo inicial de demonstração');
    expect(linhas[2]).toHaveTextContent(/\+\sR\$\s10,00/);
    for (const linha of linhas) expect(linha).toHaveTextContent('Concluída');
    expect(within(linhas[2]!).queryByRole('link')).not.toBeInTheDocument();
  });

  it('lançamento com comprovante na aba leva ao comprovante; sem comprovante, não há link (até a #36)', async () => {
    const item = itemExtrato(1);
    guardarComprovante(comprovante({ transferenciaId: item.transferenciaId }));
    servidor.use(extratoDe([item, itemExtrato(3)]));
    renderizarApp('/extrato', { sessao: 'PessoaFisica' });

    expect(await screen.findByRole('link', { name: /Contraparte 1/ })).toHaveAttribute('href', `/transferencias/${item.transferenciaId}`);
    expect(screen.queryByRole('link', { name: /Contraparte 3/ })).not.toBeInTheDocument();
  });

  it('filtros mudam a URL, voltam à página 1 e consultam a API com Entrada/Saida', async () => {
    const consultas = registrarConsultas();
    servidor.use(extratoDe([itemExtrato(1), itemExtrato(2), itemExtrato(3)]));
    const { usuario } = renderizarApp('/extrato?pagina=1', { sessao: 'PessoaFisica' });
    const filtros = await screen.findByRole('group', { name: 'Filtrar lançamentos' });
    expect(within(filtros).getByRole('button', { name: 'Todas' })).toHaveAttribute('aria-pressed', 'true');

    await usuario.click(within(filtros).getByRole('button', { name: 'Entradas' }));
    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/extrato?direcao=entradas'));
    expect(within(filtros).getByRole('button', { name: 'Entradas' })).toHaveAttribute('aria-pressed', 'true');
    await waitFor(() => expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(2));

    await usuario.click(within(filtros).getByRole('button', { name: 'Saídas' }));
    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/extrato?direcao=saidas'));
    await waitFor(() => expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(3));

    expect(consultas.some((c) => c.includes('direcao=Entrada'))).toBe(true);
    expect(consultas.some((c) => c.includes('direcao=Saida'))).toBe(true);
  });

  it('paginação de 20 com "Anterior"/"Próxima" desabilitados nas pontas', async () => {
    servidor.use(extratoDe(Array.from({ length: 25 }, (_, i) => itemExtrato(i + 1))));
    const { usuario } = renderizarApp('/extrato', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Página 1 de 2 · 20 por página')).toBeInTheDocument();
    await waitFor(() => expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(21));
    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled();

    await usuario.click(screen.getByRole('button', { name: 'Próxima' }));
    expect(await screen.findByText('Página 2 de 2 · 20 por página')).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/extrato?pagina=2');
    await waitFor(() => expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(6));
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled();
  });

  it('página além da última → última página válida', async () => {
    servidor.use(extratoDe(Array.from({ length: 25 }, (_, i) => itemExtrato(i + 1))));
    renderizarApp('/extrato?pagina=9', { sessao: 'PessoaFisica' });

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/extrato?pagina=2'));
    expect(await screen.findByText('Página 2 de 2 · 20 por página')).toBeInTheDocument();
  });

  it.each([
    ['/extrato?direcao=xyz&pagina=-3', 'Todas'],
    ['/extrato?pagina=abc', 'Todas'],
  ])('parâmetros inválidos (%s) → padrão', async (rota, filtro) => {
    renderizarApp(rota, { sessao: 'PessoaFisica' });

    expect(await screen.findByRole('button', { name: filtro })).toHaveAttribute('aria-pressed', 'true');
    expect(await screen.findByText('Página 1 de 1 · 20 por página')).toBeInTheDocument();
  });

  it('vazio no filtro → "Nenhuma movimentação ainda"', async () => {
    renderizarApp('/extrato?direcao=saidas', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Nenhuma movimentação ainda')).toBeInTheDocument();
    expect(screen.getByText('Quando você enviar ou receber uma transferência, ela aparece aqui.')).toBeInTheDocument();
  });

  it('carregando e erro com "Tentar de novo"', async () => {
    let falhar = true;
    servidor.use(
      http.get('*/api/v1/transferencias/extrato', () => {
        if (falhar) {
          falhar = false;
          return problema('erro-interno', 500);
        }
        return HttpResponse.json({ itens: [ITEM_ABERTURA], paginaAtual: 1, tamanhoPagina: 20, totalItens: 1 });
      }),
    );
    const { usuario } = renderizarApp('/extrato', { sessao: 'PessoaFisica' });

    expect(screen.getByText('Carregando lançamentos…')).toBeInTheDocument();
    expect(await screen.findByText('Algo deu errado')).toBeInTheDocument();
    expect(screen.getByText(TRACE_ID)).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByText('Saldo inicial de demonstração')).toBeInTheDocument();
  });

  it('PJ também consulta o extrato', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira('PessoaJuridica'))));
    renderizarApp('/extrato', { sessao: 'PessoaJuridica' });

    expect(await screen.findByRole('heading', { name: 'Extrato' })).toBeInTheDocument();
    expect(await screen.findByText('Saldo inicial de demonstração')).toBeInTheDocument();
  });
});

describe('Extrato — mobile (US4)', () => {
  beforeEach(() => {
    simularLargura(false);
    vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-17T15:00:00Z'));
  });

  it('lista agrupada por dia com "Carregar mais" sem duplicar e que some no fim', async () => {
    const consultas = registrarConsultas();
    servidor.use(extratoDe(Array.from({ length: 25 }, (_, i) => itemExtrato(i + 1))));
    const { usuario } = renderizarApp('/extrato', { sessao: 'PessoaFisica' });

    expect(await screen.findByRole('heading', { level: 2, name: 'Hoje' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Ontem' })).toBeInTheDocument();
    expect(screen.getByText('Saída · 08:00')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem').filter((li) => li.textContent?.includes('Contraparte'))).toHaveLength(20);

    await usuario.click(screen.getByRole('button', { name: 'Carregar mais' }));
    await waitFor(() => expect(screen.getAllByText(/^Contraparte \d+$/)).toHaveLength(25));
    expect(screen.queryByRole('button', { name: 'Carregar mais' })).not.toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/extrato?pagina=2');
    expect(consultas.at(-1)).toContain('pagina=2');
  });

  it('pagina=N inicial pede 20·N itens de uma vez', async () => {
    const consultas = registrarConsultas();
    servidor.use(extratoDe(Array.from({ length: 50 }, (_, i) => itemExtrato(i + 1))));
    renderizarApp('/extrato?pagina=2', { sessao: 'PessoaFisica' });

    await waitFor(() => expect(screen.getAllByText(/^Contraparte \d+$/)).toHaveLength(40));
    expect(consultas[0]).toContain('tamanhoPagina=40');
    expect(screen.getByRole('button', { name: 'Carregar mais' })).toBeInTheDocument();
  });
});
