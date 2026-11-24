import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import type { ContaBancaria } from '../api/cliente';
import type { TipoConta } from '../sessao/claims';
import { carteira, contaBancaria, extratoDe, itemExtrato, problema, titular, TRACE_ID } from '../testes/handlers';
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
    expect(screen.getByText(/^Clientes com conta One Bank transferem .*Contas Pessoa Jurídica não enviam transferências\.$/)).toBeInTheDocument();
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

describe('Início — dados da conta (specs/013, US1)', () => {
  function usarConta(tipoConta: TipoConta, alteracoes: Partial<ContaBancaria> = {}) {
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () =>
        HttpResponse.json({ ...carteira(tipoConta), contaBancaria: contaBancaria(alteracoes) }),
      ),
      http.get('*/api/v1/usuarios/me', () => HttpResponse.json(titular(tipoConta))),
    );
  }

  /** Valor do campo: o `dd` do mesmo grupo do `dt`. */
  function campo(regiao: HTMLElement, rotulo: string): HTMLElement {
    const grupo = within(regiao).getByText(rotulo, { selector: 'dt' }).parentElement!;
    return grupo.querySelector('dd')!;
  }

  it('PF: banco, agência, conta com dígito, tipo e texto de apoio', async () => {
    usarConta('PessoaFisica');
    renderizarApp('/', { sessao: 'PessoaFisica' });

    const regiao = await screen.findByRole('region', { name: 'Dados da conta' });
    expect(within(regiao).getByRole('heading', { level: 2, name: 'Dados da conta' })).toBeInTheDocument();
    expect(Array.from(regiao.querySelectorAll('dt'), (dt) => dt.textContent)).toEqual(['Banco', 'Agência', 'Conta', 'Tipo']);
    expect(campo(regiao, 'Banco')).toHaveTextContent('001 – One Bank');
    expect(within(campo(regiao, 'Agência')).getByText('0001')).toBeInTheDocument();
    expect(within(campo(regiao, 'Conta')).getByText('00000042-0')).toBeInTheDocument();
    expect(campo(regiao, 'Tipo')).toHaveTextContent('Conta corrente');
    expect(within(regiao).getByText('Para receber de clientes One Bank, informe o seu CPF.')).toBeInTheDocument();
  });

  it('agência e conta são lidas dígito a dígito por leitores de tela', async () => {
    usarConta('PessoaFisica');
    renderizarApp('/', { sessao: 'PessoaFisica' });

    const regiao = await screen.findByRole('region', { name: 'Dados da conta' });
    const agencia = campo(regiao, 'Agência');
    expect(within(agencia).getByText('0001')).toHaveAttribute('aria-hidden', 'true');
    expect(within(agencia).getByText('0 0 0 1')).toHaveClass('apenas-leitor-de-tela');
    const conta = campo(regiao, 'Conta');
    expect(within(conta).getByText('00000042-0')).toHaveAttribute('aria-hidden', 'true');
    expect(within(conta).getByText('0 0 0 0 0 0 4 2, dígito 0')).toHaveClass('apenas-leitor-de-tela');
  });

  it('PJ: mesmos dados depois de "Receber transferências", sem o número do CNPJ', async () => {
    usarConta('PessoaJuridica');
    renderizarApp('/', { sessao: 'PessoaJuridica' });

    const regiao = await screen.findByRole('region', { name: 'Dados da conta' });
    expect(campo(regiao, 'Banco')).toHaveTextContent('001 – One Bank');
    expect(within(regiao).getByText('00000042-0')).toBeInTheDocument();
    expect(within(regiao).getByText('Para receber de clientes One Bank, informe o seu CNPJ.')).toBeInTheDocument();

    expect(await screen.findByText('12.ABC.345/01DE-35')).toBeInTheDocument();
    expect(within(regiao).queryByText(/12\.?ABC/)).not.toBeInTheDocument();
    const receber = screen.getByRole('heading', { name: 'Receber transferências' });
    expect(receber.compareDocumentPosition(regiao) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it.each([
    ['ContaPoupanca', 'Conta poupança'],
    ['ContaSalario', 'Conta'],
  ])('tipo %s é exibido como "%s"', async (tipo, nome) => {
    usarConta('PessoaFisica', { tipo });
    renderizarApp('/', { sessao: 'PessoaFisica' });

    const regiao = await screen.findByRole('region', { name: 'Dados da conta' });
    expect(campo(regiao, 'Tipo').textContent).toBe(nome);
  });

  it('o nome do banco vem da API (FR-002)', async () => {
    usarConta('PessoaFisica', { banco: { codigo: '009', nome: 'Banco de Teste' } });
    renderizarApp('/', { sessao: 'PessoaFisica' });

    const regiao = await screen.findByRole('region', { name: 'Dados da conta' });
    expect(campo(regiao, 'Banco')).toHaveTextContent('009 – Banco de Teste');
  });

  it('carregando ou com erro, não mostra o cartão', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => problema('qualquer', 500)));
    renderizarApp('/', { sessao: 'PessoaFisica' });

    expect(screen.getByRole('status')).toHaveTextContent('Carregando sua carteira…');
    expect(screen.queryByText('Dados da conta')).not.toBeInTheDocument();
    await screen.findByRole('alert');
    expect(screen.queryByText('Dados da conta')).not.toBeInTheDocument();
  });
});
