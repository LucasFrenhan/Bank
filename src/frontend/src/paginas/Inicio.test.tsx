import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { carteira, extratoDe, itemExtrato, problema, titular, TRACE_ID } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';

describe('Início (US3; FR-014 a FR-016)', () => {
  it('PF: saudação, saldo, atualização e ações ativas (specs/007, FR-022)', async () => {
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(await screen.findByRole('heading', { name: 'Olá, Lucas' })).toBeInTheDocument();
    expect(screen.getAllByText('Pessoa Física').length).toBeGreaterThan(0);
    const saldo = screen.getByRole('region', { name: 'Saldo disponível' });
    expect(within(saldo).getByText(/R\$\s10,00/)).toBeInTheDocument();
    expect(screen.getByText('Atualizado em 17 nov 2026, 14:32.')).toBeInTheDocument();

    expect(within(saldo).getByRole('link', { name: /Transferir/ })).toHaveAttribute('href', '/transferir');
    expect(within(saldo).getByRole('link', { name: /Ver extrato/ })).toHaveAttribute('href', '/extrato');
    expect(within(saldo).queryByText('Em breve')).not.toBeInTheDocument();
    expect(screen.queryByText(/Entradas no mês|Saídas no mês/)).not.toBeInTheDocument();
  });

  it('PJ: razão social, saldo zero e nenhuma ação de transferir (RN-06)', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira('PessoaJuridica'))));
    renderizarApp('/', { sessao: 'PessoaJuridica' });

    expect(await screen.findByRole('heading', { name: 'Empresa Exemplo Ltda' })).toBeInTheDocument();
    expect(screen.getByText(/R\$\s0,00/)).toBeInTheDocument();
    expect(screen.getByText(/Contas Pessoa Jurídica não enviam transferências/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Transferir/ })).not.toBeInTheDocument();
    expect(screen.queryByText('Transferir')).not.toBeInTheDocument();
  });

  it('PJ: mostra o próprio CNPJ e "Copiar" grava o valor sem máscara (FR-017a)', async () => {
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira('PessoaJuridica'))),
      http.get('*/api/v1/usuarios/me', () => HttpResponse.json(titular('PessoaJuridica'))),
    );
    const { usuario } = renderizarApp('/', { sessao: 'PessoaJuridica' });

    expect(await screen.findByText('12.ABC.345/01DE-35')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Copiar CNPJ' }));

    expect(await navigator.clipboard.readText()).toBe('12ABC34501DE35');
    expect(screen.getByRole('status')).toHaveTextContent('CNPJ copiado');
  });

  it('PF: não consulta nem exibe CNPJ', async () => {
    let consultas = 0;
    servidor.use(
      http.get('*/api/v1/usuarios/me', () => {
        consultas++;
        return HttpResponse.json(titular());
      }),
    );
    renderizarApp('/', { sessao: 'PessoaFisica' });

    await screen.findByRole('heading', { name: 'Olá, Lucas' });
    expect(screen.queryByRole('button', { name: 'Copiar CNPJ' })).not.toBeInTheDocument();
    expect(consultas).toBe(0);
  });

  it('mostra carregamento sem saldo falso', async () => {
    servidor.use(
      http.get('*/api/v1/carteiras/minha', async () => {
        await new Promise((r) => setTimeout(r, 100));
        return HttpResponse.json(carteira());
      }),
    );
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(screen.getByRole('status')).toHaveTextContent('Carregando sua carteira…');
    expect(screen.queryByText(/R\$/)).not.toBeInTheDocument();
    await screen.findByRole('heading', { name: 'Olá, Lucas' });
  });

  it('erro inesperado mostra "Algo deu errado" com traceId e permite tentar de novo', async () => {
    let chamadas = 0;
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => {
        chamadas++;
        return chamadas === 1 ? problema('qualquer', 500) : HttpResponse.json(carteira());
      }),
    );
    const { usuario } = renderizarApp('/', { sessao: 'PessoaFisica' });

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('Algo deu errado');
    expect(alerta).toHaveTextContent(TRACE_ID);

    await usuario.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByRole('heading', { name: 'Olá, Lucas' })).toBeInTheDocument();
    expect(chamadas).toBe(2);
  });

  it('conta inexistente (404) encerra a sessão e leva ao login', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => problema('carteira-nao-encontrada', 404)));
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Não encontramos sua conta. Entre novamente.')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar?conta=1'));
  });

  it('cada exibição busca o saldo atual na API', async () => {
    let saldo = 10;
    servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json({ ...carteira(), saldo })));
    const primeira = renderizarApp('/', { sessao: 'PessoaFisica' });
    const regiao = await screen.findByRole('region', { name: 'Saldo disponível' });
    expect(within(regiao).getByText(/R\$\s10,00/)).toBeInTheDocument();
    primeira.unmount();

    saldo = 123.45;
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(await screen.findByText(/R\$\s123,45/)).toBeInTheDocument();
  });
});

describe('Início — movimentações reais (specs/007: US5; FR-022 a FR-024)', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-17T15:00:00Z'));
  });

  it('PF: "Últimas movimentações" com os 5 mais recentes e "Ver tudo"', async () => {
    const consultas: string[] = [];
    servidor.events.on('request:start', ({ request }) => {
      if (request.url.includes('/extrato')) consultas.push(new URL(request.url).search);
    });
    servidor.use(extratoDe([itemExtrato(1), itemExtrato(2), itemExtrato(30), itemExtrato(50), itemExtrato(51), itemExtrato(52)]));
    renderizarApp('/', { sessao: 'PessoaFisica' });

    const secao = await screen.findByRole('region', { name: 'Últimas movimentações' });
    await waitFor(() => expect(within(secao).getAllByRole('listitem')).toHaveLength(5));
    expect(within(secao).getByText('Transferência enviada · Hoje, 08:00')).toBeInTheDocument();
    expect(within(secao).getByText('Transferência recebida · Hoje, 07:00')).toBeInTheDocument();
    expect(within(secao).getByText('Transferência recebida · 15 nov, 07:00')).toBeInTheDocument();
    expect(within(secao).getByRole('link', { name: 'Ver tudo' })).toHaveAttribute('href', '/extrato');
    expect(consultas[0]).toContain('tamanhoPagina=5');
    servidor.events.removeAllListeners();
  });

  it('PF sem movimentações → "Nenhuma movimentação ainda"', async () => {
    servidor.use(extratoDe([]));
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Nenhuma movimentação ainda')).toBeInTheDocument();
  });

  it('PJ: "Últimos recebimentos" só com entradas e "Ver tudo" no extrato filtrado', async () => {
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira('PessoaJuridica'))),
      extratoDe([itemExtrato(1), itemExtrato(2, { descricao: null }), itemExtrato(4)]),
    );
    renderizarApp('/', { sessao: 'PessoaJuridica' });

    const secao = await screen.findByRole('region', { name: 'Últimos recebimentos' });
    await waitFor(() => expect(within(secao).getAllByRole('listitem')).toHaveLength(2));
    expect(within(secao).getByText('Sem descrição · Hoje, 07:00')).toBeInTheDocument();
    expect(within(secao).getByText('Descrição 4 · Hoje, 05:00')).toBeInTheDocument();
    expect(within(secao).queryByText(/Contraparte 1$/)).not.toBeInTheDocument();
    expect(within(secao).getByRole('link', { name: 'Ver tudo' })).toHaveAttribute('href', '/extrato?direcao=entradas');
  });

  it('navegação PF: Transferir e Extrato ativos; PJ: Recebimentos leva ao extrato filtrado e Cobranças continua "Em breve"', async () => {
    const pf = renderizarApp('/', { sessao: 'PessoaFisica' });
    const menuPf = await screen.findByRole('navigation', { name: 'Menu principal' });
    expect(within(menuPf).getByRole('link', { name: 'Transferir' })).toHaveAttribute('href', '/transferir');
    expect(within(menuPf).getByRole('link', { name: 'Extrato' })).toHaveAttribute('href', '/extrato');
    const barraPf = screen.getByRole('navigation', { name: 'Menu principal (celular)' });
    expect(within(barraPf).queryByText('Transferir')).not.toBeInTheDocument();
    expect(within(barraPf).getByRole('link', { name: 'Extrato' })).toBeInTheDocument();
    pf.unmount();
    sessionStorage.clear();

    servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira('PessoaJuridica'))));
    renderizarApp('/', { sessao: 'PessoaJuridica' });
    const menuPj = await screen.findByRole('navigation', { name: 'Menu principal' });
    expect(within(menuPj).getByRole('link', { name: 'Recebimentos' })).toHaveAttribute('href', '/extrato?direcao=entradas');
    expect(within(menuPj).getByText('Cobranças').closest('[aria-disabled]')).toHaveAttribute('aria-disabled', 'true');
  });
});
