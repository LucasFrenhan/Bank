import { screen, waitFor, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { carteira, fatura, faturaDetalhada, paginado, problema, TRACE_ID } from '../../testes/handlers';
import { renderizarApp } from '../../testes/renderizar';
import { servidor } from '../../testes/setup';

/** Faturas e pagamento (specs/015, US4 e US5; FR-019 e FR-020). */

const VENCIDA = { situacao: 'Vencida', total: 1000, diasAtraso: 10, juros: 50, valorAPagar: 1050 };
const DISPONIVEL = { situacao: 'Disponivel', total: 1000, valorAPagar: 1000 };
const PAGA = { situacao: 'Paga', total: 1000, diasAtraso: 10, juros: 50, valorAPagar: null, valorPago: 1050, pagaEm: '2026-12-20T13:12:00Z' };
const LANCAMENTOS = [
  { estabelecimento: 'Cafeteria LTDA', descricao: 'Kit 3 Pacotes', valor: 299.9, dataCompra: '2026-11-24' },
  { estabelecimento: 'Livraria ME', descricao: 'Livros', valor: 700.1, dataCompra: '2026-11-21' },
];

describe('Lista de faturas', () => {
  const abrir = () => renderizarApp('/cartao/faturas', { sessao: 'PessoaFisica' });

  it('por mês, com situação, vencimento e valor', async () => {
    servidor.use(
      paginado('/api/v1/faturas', [
        fatura({ competencia: '2027-01', vencimento: '2027-01-11' }),
        fatura(VENCIDA),
        fatura({ competencia: '2026-11', ...PAGA, valorPago: 742.3, pagaEm: '2026-11-09T12:00:00Z' }),
      ]),
    );
    abrir();

    const lista = await screen.findByRole('region', { name: 'Lista de faturas' });
    const links = within(lista).getAllByRole('link');
    expect(links[0]).toHaveTextContent(/Janeiro de 2027Vence em 11 janAbertaR\$\s0,00/);
    expect(links[1]).toHaveTextContent(/Dezembro de 2026Venceu em 10 dez · 10 dias de atrasoVencidaR\$\s1\.050,00/);
    expect(links[2]).toHaveTextContent(/Novembro de 2026Paga em 9 nov 2026PagaR\$\s742,30/);
    expect(links[1]).toHaveAttribute('href', '/cartao/faturas/2026-12');
    expect(document.title).toBe('Faturas · One Bank');
  });

  it('sem cartão leva à solicitação; erro e PJ', async () => {
    const semCartao = abrir();
    expect(await screen.findByText('Você ainda não tem um cartão')).toBeInTheDocument();
    expect(screen.getByText('As faturas aparecem depois que você pedir o seu.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Solicitar cartão' })).toHaveAttribute('href', '/cartao');
    semCartao.unmount();

    servidor.use(http.get('*/api/v1/faturas', () => problema('x', 500)));
    const erro = abrir();
    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
    erro.unmount();

    renderizarApp('/cartao/faturas', { sessao: 'PessoaJuridica' });
    expect(await screen.findByText('Área indisponível para o seu tipo de conta')).toBeInTheDocument();
  });
});

function detalhe(dados: object) {
  servidor.use(http.get('*/api/v1/faturas/:competencia', () => HttpResponse.json(faturaDetalhada(dados))));
  return renderizarApp('/cartao/faturas/2026-12', { sessao: 'PessoaFisica' });
}

describe('Detalhe da fatura', () => {
  it('vencida: aviso, juros separados, valor de hoje e lançamentos', async () => {
    detalhe({ ...VENCIDA, lancamentos: LANCAMENTOS });

    expect(await screen.findByRole('heading', { level: 1, name: 'Fatura de dezembro de 2026' })).toBeInTheDocument();
    const aviso = screen.getByRole('alert');
    expect(aviso).toHaveTextContent('Fatura vencida há 10 dias');
    expect(aviso).toHaveTextContent('O cartão está bloqueado para novas compras até o pagamento.');
    const resumo = screen.getByRole('region', { name: 'Resumo' });
    expect(resumo).toHaveTextContent(/Valor a pagar hojeR\$\s1\.050,00/);
    expect(resumo).toHaveTextContent(/Total das comprasR\$\s1\.000,00/);
    expect(resumo).toHaveTextContent(/Juros \(10 dias de atraso\)R\$\s50,00/);
    expect(resumo).toHaveTextContent('Período8 nov a 7 dez');
    expect(resumo).toHaveTextContent('Fechamento7 dez 2026');
    expect(resumo).toHaveTextContent('Vencimento10 dez 2026');
    const lancamentos = within(screen.getByRole('region', { name: 'Lançamentos' })).getAllByRole('listitem');
    expect(lancamentos[0]).toHaveTextContent(/Cafeteria LTDAKit 3 Pacotes · 24 novR\$\s299,90/);
    expect(lancamentos).toHaveLength(2);
  });

  it('um dia de atraso no singular', async () => {
    detalhe({ ...VENCIDA, diasAtraso: 1 });
    expect(await screen.findByText('Fatura vencida há 1 dia')).toBeInTheDocument();
  });

  it('aberta sem lançamentos: zero, explicação e sem pagar', async () => {
    detalhe({});
    expect(await screen.findByText('Nenhum lançamento ainda. As compras aparecem aqui no próximo dia útil.')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Resumo' })).toHaveTextContent(/Total até agoraR\$\s0,00/);
    expect(screen.queryByRole('button', { name: 'Pagar fatura' })).not.toBeInTheDocument();
  });

  it('fechada: liberada após o processamento, sem pagar', async () => {
    detalhe({ situacao: 'Fechada', total: 10 });
    expect(await screen.findByText('Liberado após o processamento')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Pagar fatura' })).not.toBeInTheDocument();
  });

  it('paga: valor e data do pagamento, sem aviso de bloqueio', async () => {
    detalhe(PAGA);
    expect(await screen.findByRole('region', { name: 'Resumo' })).toHaveTextContent(/Valor pagoR\$\s1\.050,00/);
    expect(screen.getByText(/20 dez 2026/)).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it.each([
    ['fatura-nao-encontrada', 404],
    ['validacao', 400],
  ])('%s: fatura não encontrada', async (codigo, status) => {
    servidor.use(http.get('*/api/v1/faturas/:competencia', () => problema(codigo, status, { errors: { competencia: ['x'] } })));
    renderizarApp('/cartao/faturas/2020-01', { sessao: 'PessoaFisica' });
    expect(await screen.findByText('Fatura não encontrada')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver faturas' })).toHaveAttribute('href', '/cartao/faturas');
  });

  it('erro com nova tentativa', async () => {
    servidor.use(http.get('*/api/v1/faturas/:competencia', () => problema('x', 500)));
    renderizarApp('/cartao/faturas/2026-12', { sessao: 'PessoaFisica' });
    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
    servidor.use(http.get('*/api/v1/faturas/:competencia', () => HttpResponse.json(faturaDetalhada())));
    screen.getByRole('button', { name: 'Tentar de novo' }).click();
    expect(await screen.findByRole('heading', { level: 1, name: 'Fatura de dezembro de 2026' })).toBeInTheDocument();
  });
});

describe('Pagar a fatura', () => {
  function pagarCom(saldos: number[], respostas: (() => Response | Promise<Response>)[] = []) {
    const estado = { chaves: [] as (string | null)[], consultasSaldo: 0 };
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => HttpResponse.json({ ...carteira(), saldo: saldos[Math.min(estado.consultasSaldo++, saldos.length - 1)] })),
      http.post('*/api/v1/faturas/:competencia/pagar', async ({ request }) => {
        estado.chaves.push(request.headers.get('Idempotency-Key'));
        const resposta = respostas[estado.chaves.length - 1];
        return (await (resposta ? resposta() : HttpResponse.json(faturaDetalhada(PAGA))));
      }),
    );
    return estado;
  }

  async function confirmar(usuario: ReturnType<typeof renderizarApp>['usuario']) {
    await usuario.click(await screen.findByRole('button', { name: 'Pagar fatura' }));
    return screen.getByRole('region', { name: 'Confira antes de pagar' });
  }

  it('confirma valor com juros, saldo e saldo depois; paga uma vez e mostra o valor pago de fato', async () => {
    const estado = pagarCom([2500], [
      async () => {
        await delay(20);
        return HttpResponse.json(faturaDetalhada({ ...PAGA, juros: 55, valorPago: 1055 }));
      },
    ]);
    const { usuario } = detalhe(VENCIDA);
    const confirmacao = await confirmar(usuario);
    expect(await within(confirmacao).findByText(/R\$\s1\.450,00/)).toBeInTheDocument();
    expect(confirmacao).toHaveTextContent(/Valor a pagarR\$\s1\.050,00/);
    expect(confirmacao).toHaveTextContent(/JurosR\$\s50,00/);
    expect(confirmacao).toHaveTextContent(/Seu saldoR\$\s2\.500,00/);
    await usuario.dblClick(within(confirmacao).getByRole('button', { name: /^Pagar R\$\s1\.050,00/ }));

    expect(await screen.findByText('Paga')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Resumo' })).toHaveTextContent(/Valor pagoR\$\s1\.055,00/);
    expect(screen.getByText('Fatura paga')).toHaveAttribute('role', 'status');
    expect(screen.getByRole('heading', { level: 1 })).toHaveFocus();
    expect(estado.chaves).toHaveLength(1);
  });

  it('saldo menor: avisa e não deixa pagar; "Voltar à fatura" fecha', async () => {
    const estado = pagarCom([320]);
    const { usuario } = detalhe(VENCIDA);
    const confirmacao = await confirmar(usuario);

    expect(await within(confirmacao).findByText(/Você tem R\$\s320,00 e a fatura é de R\$\s1\.050,00\./)).toBeInTheDocument();
    expect(within(confirmacao).getByRole('button', { name: /^Pagar R\$/ })).toBeDisabled();
    await usuario.click(within(confirmacao).getByRole('button', { name: 'Voltar à fatura' }));
    expect(screen.getByRole('button', { name: 'Pagar fatura' })).toHaveFocus();
    expect(estado.chaves).toEqual([]);
  });

  it('saldo igual ao valor paga', async () => {
    pagarCom([1000], [() => HttpResponse.json(faturaDetalhada({ ...PAGA, juros: 0, valorPago: 1000 }))]);
    const { usuario } = detalhe(DISPONIVEL);
    const confirmacao = await confirmar(usuario);
    expect(await within(confirmacao).findByText(/R\$\s0,00/)).toBeInTheDocument();
    await usuario.click(within(confirmacao).getByRole('button', { name: /^Pagar R\$/ }));
    expect(await screen.findByText('Paga')).toBeInTheDocument();
  });

  it('saldo que mudou depois: a recusa da API é mostrada igual, com o saldo atual', async () => {
    pagarCom([1500, 200], [() => problema('saldo-insuficiente', 422)]);
    const { usuario } = detalhe(DISPONIVEL);
    const confirmacao = await confirmar(usuario);
    await within(confirmacao).findByText(/R\$\s500,00/);
    await usuario.click(within(confirmacao).getByRole('button', { name: /^Pagar R\$/ }));

    expect(await within(confirmacao).findByText(/Você tem R\$\s200,00 e a fatura é de R\$\s1\.000,00\./)).toBeInTheDocument();
    expect(within(confirmacao).getByRole('button', { name: /^Pagar R\$/ })).toBeDisabled();
  });

  it('falha de rede e nova tentativa: mesma chave', async () => {
    const estado = pagarCom([2500], [() => HttpResponse.error()]);
    const { usuario } = detalhe(DISPONIVEL);
    const confirmacao = await confirmar(usuario);
    await within(confirmacao).findByText(/R\$\s1\.500,00/);
    await usuario.click(within(confirmacao).getByRole('button', { name: /^Pagar R\$/ }));
    await usuario.click(await within(confirmacao).findByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByText('Paga')).toBeInTheDocument();
    expect(estado.chaves[1]).toBe(estado.chaves[0]);
  });

  it('paga em outra aba: avisa e mostra a situação atualizada', async () => {
    pagarCom([2500], [
      () => {
        servidor.use(http.get('*/api/v1/faturas/:competencia', () => HttpResponse.json(faturaDetalhada(PAGA))));
        return problema('fatura-nao-disponivel', 409);
      },
    ]);
    const { usuario } = detalhe(DISPONIVEL);
    const confirmacao = await confirmar(usuario);
    await within(confirmacao).findByText(/R\$\s1\.500,00/);
    await usuario.click(within(confirmacao).getByRole('button', { name: /^Pagar R\$/ }));

    expect(await screen.findByText('Esta fatura não está disponível para pagamento')).toBeInTheDocument();
    expect(await screen.findByText('Paga')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Pagar fatura' })).not.toBeInTheDocument());
  });

  it('erro ao consultar o saldo não deixa pagar às cegas', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => problema('x', 500)));
    const { usuario } = detalhe(DISPONIVEL);
    const confirmacao = await confirmar(usuario);
    expect(await within(confirmacao).findByRole('alert')).toHaveTextContent(TRACE_ID);
    expect(within(confirmacao).getByRole('button', { name: /^Pagar R\$/ })).toBeDisabled();
  });
});
