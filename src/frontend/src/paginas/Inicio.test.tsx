import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import type { ContaBancaria } from '../api/cliente';
import type { TipoConta } from '../sessao/claims';
import { carteira, contaBancaria, extratoDe, fatura, itemExtrato, paginado, problema, titular, TRACE_ID } from '../testes/handlers';
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
    expect(await screen.findByText('CNPJ copiado')).toHaveAttribute('role', 'status');
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

  it('navegação PF: Transferir e Extrato ativos; PJ: Recebimentos leva ao extrato filtrado e Cobranças está ativa', async () => {
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
    expect(within(menuPj).getByRole('link', { name: 'Cobranças' })).toHaveAttribute('href', '/cobrancas');
    expect(within(menuPj).queryByText(/Em breve/)).not.toBeInTheDocument();
  });
});

/** Aviso de fatura (specs/015, FR-019a). */
describe('Início — aviso de fatura', () => {
  const VENCIDA = { situacao: 'Vencida', total: 1000, diasAtraso: 10, juros: 50, valorAPagar: 1050 };
  const DISPONIVEL = { situacao: 'Disponivel', total: 299.9, valorAPagar: 299.9 };

  async function aviso(faturas: object[]) {
    servidor.use(paginado('/api/v1/faturas', faturas));
    renderizarApp('/', { sessao: 'PessoaFisica' });
    return screen.findByRole('region', { name: 'Aviso de fatura' });
  }

  it('disponível: valor, vencimento e link', async () => {
    const regiao = await aviso([fatura({ competencia: '2027-01' }), fatura(DISPONIVEL)]);
    expect(regiao).toHaveTextContent(/Fatura de dezembro — R\$\s299,90, vence em 10 dez\./);
    expect(within(regiao).getByRole('link', { name: 'Ver fatura' })).toHaveAttribute('href', '/cartao/faturas/2026-12');
  });

  it('vencida: valor de hoje e cartão bloqueado', async () => {
    const regiao = await aviso([fatura(VENCIDA)]);
    expect(regiao).toHaveTextContent(/Fatura vencida — R\$\s1\.050,00 hoje\./);
    expect(regiao).toHaveTextContent('O cartão está bloqueado.');
  });

  it('mais de uma: a mais antiga', async () => {
    const regiao = await aviso([fatura({ ...DISPONIVEL, competencia: '2027-01' }), fatura({ ...VENCIDA, competencia: '2026-12' }), fatura({ ...VENCIDA, competencia: '2026-11' })]);
    expect(within(regiao).getByRole('link', { name: 'Ver fatura' })).toHaveAttribute('href', '/cartao/faturas/2026-11');
  });

  it.each([
    ['nada a pagar', () => paginado('/api/v1/faturas', [fatura(), fatura({ competencia: '2026-11', situacao: 'Paga' }), fatura({ competencia: '2026-10', situacao: 'Fechada' })])],
    ['sem cartão', () => http.get('*/api/v1/faturas', () => problema('cartao-nao-encontrado', 404))],
    ['erro', () => http.get('*/api/v1/faturas', () => problema('x', 500))],
    ['rede', () => http.get('*/api/v1/faturas', () => HttpResponse.error())],
  ])('%s: início como antes', async (_, handler) => {
    let respondida = false;
    servidor.use(handler());
    servidor.events.on('request:start', ({ request }) => {
      if (request.url.includes('/api/v1/faturas')) respondida = true;
    });
    renderizarApp('/', { sessao: 'PessoaFisica' });
    await screen.findByRole('heading', { level: 1, name: 'Olá, Lucas' });
    await waitFor(() => expect(respondida).toBe(true));
    await screen.findByText('Saldo inicial');

    expect(screen.queryByRole('region', { name: 'Aviso de fatura' })).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    servidor.events.removeAllListeners();
  });

  it('PJ não consulta faturas', async () => {
    let consultas = 0;
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira('PessoaJuridica'))),
      http.get('*/api/v1/faturas', () => { consultas++; return problema('acesso-negado', 403); }),
    );
    renderizarApp('/', { sessao: 'PessoaJuridica' });
    await screen.findByRole('heading', { level: 1, name: 'Empresa Exemplo Ltda' });
    expect(consultas).toBe(0);
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

describe('Início — copiar dados da conta (specs/013, US3)', () => {
  const TEXTO = 'Banco 001 – One Bank\nAgência 0001\nConta corrente 00000042-0';
  const FALHA = 'Não foi possível copiar. Selecione os dados e copie manualmente.';

  async function abrir(tipoConta: TipoConta = 'PessoaFisica') {
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira(tipoConta))),
      http.get('*/api/v1/usuarios/me', () => HttpResponse.json(titular(tipoConta))),
    );
    const { usuario } = renderizarApp('/', { sessao: tipoConta });
    const regiao = await screen.findByRole('region', { name: 'Dados da conta' });
    const aviso = within(regiao).getByRole('status');
    const copiar = () => usuario.click(within(regiao).getByRole('button', { name: 'Copiar dados da conta' }));
    return { usuario, regiao, aviso, copiar };
  }

  it('copia exatamente as três linhas e confirma', async () => {
    const { aviso, copiar } = await abrir();
    expect(aviso).toBeEmptyDOMElement();
    await copiar();

    expect(await navigator.clipboard.readText()).toBe(TEXTO);
    expect(aviso).toHaveTextContent('Dados da conta copiados');
    expect(aviso).not.toHaveClass('avisoFalha');
  });

  it('cópia negada: avisa com o estilo de erro e mantém os dados visíveis', async () => {
    const { regiao, aviso, copiar } = await abrir();
    const negada = vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValue(new DOMException('negado', 'NotAllowedError'));
    try {
      await copiar();

      await waitFor(() => expect(aviso).toHaveTextContent(FALHA));
      expect(aviso).toHaveClass('avisoFalha');
      expect(within(regiao).getByText('00000042-0')).toBeInTheDocument();
    } finally {
      negada.mockRestore();
    }
  });

  it('sem área de transferência no navegador: mesma mensagem', async () => {
    const { aviso, copiar } = await abrir();
    const original = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
    Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true });
    try {
      await copiar();
      await waitFor(() => expect(aviso).toHaveTextContent(FALHA));
    } finally {
      if (original) Object.defineProperty(navigator, 'clipboard', original);
    }
  });

  it('copiar de novo limpa o aviso antes do novo resultado, para ser anunciado outra vez', async () => {
    const { aviso, copiar } = await abrir();
    await copiar();
    expect(aviso).toHaveTextContent('Dados da conta copiados');

    let concluir: () => void = () => undefined;
    const pendente = vi.spyOn(navigator.clipboard, 'writeText').mockImplementation(() => new Promise((r) => (concluir = r)));
    try {
      await copiar();
      await waitFor(() => expect(aviso).toBeEmptyDOMElement());
      concluir();
      await waitFor(() => expect(aviso).toHaveTextContent('Dados da conta copiados'));
    } finally {
      pendente.mockRestore();
    }
  });

  it('PJ: o aviso de um cartão não aparece no outro, e a falha do CNPJ usa o estilo de erro', async () => {
    const { usuario, aviso, copiar } = await abrir('PessoaJuridica');
    const copiarCnpj = await screen.findByRole('button', { name: 'Copiar CNPJ' });
    await usuario.click(copiarCnpj);
    expect(await screen.findByText('CNPJ copiado')).toBeInTheDocument();
    expect(aviso).toBeEmptyDOMElement();

    await copiar();
    await waitFor(() => expect(aviso).toHaveTextContent('Dados da conta copiados'));
    expect(screen.getByText('CNPJ copiado')).toBeInTheDocument();

    const negada = vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValue(new DOMException('negado', 'NotAllowedError'));
    try {
      await usuario.click(copiarCnpj);
      expect(await screen.findByText('Não foi possível copiar. Selecione o CNPJ e copie manualmente.')).toHaveClass('avisoFalha');
      expect(aviso).toHaveTextContent('Dados da conta copiados');
    } finally {
      negada.mockRestore();
    }
  });
});
