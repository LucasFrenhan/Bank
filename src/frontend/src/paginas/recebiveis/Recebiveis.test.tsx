import { screen, waitFor, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { paginado, problema, recebivel, TRACE_ID } from '../../testes/handlers';
import { renderizarApp } from '../../testes/renderizar';
import { servidor } from '../../testes/setup';
import { hojeBrasilia } from '../../formatacao/dia';

/** Recebíveis e antecipação (specs/015, US6; FR-021 e FR-022). */

const FILTRO = { parametro: 'situacao', campo: 'situacao' };
const ANTECIPADO = { situacao: 'Antecipado', taxa: 6, valorLiquido: 293.9, recebidoEm: '2026-11-24T18:00:00Z' };
const abrir = (rota = '/cobrancas/recebiveis') => renderizarApp(rota, { sessao: 'PessoaJuridica' });

function api(itens: object[], respostas: (() => Response | Promise<Response>)[] = []) {
  const chaves: (string | null)[] = [];
  servidor.use(
    paginado('/api/v1/recebiveis', itens, FILTRO),
    http.post('*/api/v1/recebiveis/:id/antecipar', async ({ request }) => {
      chaves.push(request.headers.get('Idempotency-Key'));
      const resposta = respostas[chaves.length - 1];
      return (await (resposta ? resposta() : HttpResponse.json(recebivel(ANTECIPADO))));
    }),
  );
  return chaves;
}

async function abrirConfirmacao(usuario: ReturnType<typeof renderizarApp>['usuario']) {
  await usuario.click(await screen.findByRole('button', { name: 'Antecipar Kit 3 Pacotes' }));
  return screen.getByRole('region', { name: 'Antecipar este recebível' });
}

describe('Lista de recebíveis', () => {
  it('descrição, valor, data prevista e situação; sem ação se liquidado, antecipado ou previsto para hoje', async () => {
    api([
      recebivel({ dataPrevista: '2099-01-07' }),
      recebivel({ recebivelId: 'a', descricao: 'Antecipado antes', ...ANTECIPADO }),
      recebivel({ recebivelId: 'l', descricao: 'Já liquidado', situacao: 'Liquidado', recebidoEm: '2026-11-20T12:00:00Z' }),
      recebivel({ recebivelId: 'h', descricao: 'Para hoje', dataPrevista: hojeBrasilia() }),
    ]);
    abrir();

    const lista = await screen.findByRole('region', { name: 'Lista de recebíveis' });
    const itens = within(lista).getAllByRole('listitem');
    expect(itens[0]).toHaveTextContent(/Kit 3 PacotesPrevisto para 7 jan 2099PendenteR\$\s299,90/);
    expect(itens[1]).toHaveTextContent(/R\$\s299,90 menos taxa de R\$\s6,00 · Recebido em 24 nov 2026/);
    expect(itens[1]).toHaveTextContent(/AntecipadoR\$\s293,90/);
    expect(itens[2]).toHaveTextContent('Recebido em 20 nov 2026');
    expect(within(lista).getAllByRole('button')).toHaveLength(1);
    const secoes = screen.getByRole('navigation', { name: 'Seções de cobranças' });
    expect(within(secoes).getByRole('link', { name: 'Recebíveis' })).toHaveAttribute('aria-current', 'page');
    await waitFor(() => expect(document.title).toBe('Recebíveis · One Bank'));
  });

  it('filtro no endereço, páginas e vazio', async () => {
    api(Array.from({ length: 21 }, (_, i) => recebivel({ recebivelId: `r${i}`, descricao: `Item ${i + 1}` })));
    const { usuario } = abrir('/cobrancas/recebiveis?situacao=pendentes');
    await usuario.click(await screen.findByRole('button', { name: 'Próxima' }));
    expect(await screen.findByText('Item 21')).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/cobrancas/recebiveis?situacao=pendentes&pagina=2');

    await usuario.click(screen.getByRole('button', { name: 'Liquidados' }));
    expect(await screen.findByText('Nenhum recebível nesta situação')).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/cobrancas/recebiveis?situacao=liquidados');
  });

  it('vazia, erro e PF', async () => {
    api([]);
    const vazia = abrir();
    expect(await screen.findByText('Nenhum recebível ainda')).toBeInTheDocument();
    vazia.unmount();

    servidor.use(http.get('*/api/v1/recebiveis', () => problema('x', 500)));
    const erro = abrir();
    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
    erro.unmount();

    renderizarApp('/cobrancas/recebiveis', { sessao: 'PessoaFisica' });
    expect(await screen.findByText('Área indisponível para o seu tipo de conta')).toBeInTheDocument();
  });
});

describe('Antecipar', () => {
  it('confirma valor, taxa, líquido, data e aviso, e mostra o resultado da API', async () => {
    const chaves = api([recebivel({ dataPrevista: '2099-01-07' })], [
      async () => {
        await delay(20);
        return HttpResponse.json(recebivel({ ...ANTECIPADO, taxa: 6.01, valorLiquido: 293.89 }));
      },
    ]);
    const { usuario } = abrir();
    const confirmacao = await abrirConfirmacao(usuario);
    expect(within(confirmacao).getByRole('heading')).toHaveFocus();
    expect(confirmacao).toHaveTextContent(/ValorR\$\s299,90/);
    expect(confirmacao).toHaveTextContent(/Taxa \(2%\)− R\$\s6,00/);
    expect(confirmacao).toHaveTextContent(/Você recebe agoraR\$\s293,90/);
    expect(confirmacao).toHaveTextContent('Sem antecipar, o valor integral cai em 7 jan 2099. A antecipação não pode ser desfeita.');
    await usuario.dblClick(within(confirmacao).getByRole('button', { name: /^Receber R\$/ }));

    expect(await screen.findByText('Antecipado')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Lista de recebíveis' })).toHaveTextContent(/menos taxa de R\$\s6,01/);
    expect(screen.getByText(/Recebível antecipado: R\$\s293,89/)).toHaveAttribute('role', 'status');
    expect(chaves).toHaveLength(1);
    expect(screen.queryByRole('button', { name: /Antecipar/ })).not.toBeInTheDocument();
  });

  it('falha de rede e nova tentativa usam a mesma chave', async () => {
    const chaves = api([recebivel()], [() => HttpResponse.error()]);
    const { usuario } = abrir();
    const confirmacao = await abrirConfirmacao(usuario);
    await usuario.click(within(confirmacao).getByRole('button', { name: /^Receber R\$/ }));
    await usuario.click(await screen.findByRole('button', { name: 'Tentar de novo' }));

    expect(await screen.findByText('Antecipado')).toBeInTheDocument();
    expect(chaves[1]).toBe(chaves[0]);
    expect(chaves[0]).toMatch(/^[0-9a-f-]{36}$/);
  });

  it.each([
    ['recebivel-nao-antecipavel', 409, 'Este recebível não pode mais ser antecipado'],
    ['recebivel-nao-encontrado', 404, 'Recebível não encontrado'],
  ])('%s: explica e atualiza a lista', async (codigo, status, titulo) => {
    const itens = [recebivel()];
    api(itens, [
      () => {
        itens[0] = recebivel({ situacao: 'Liquidado', recebidoEm: '2026-11-24T18:00:00Z' });
        return problema(codigo, status);
      },
    ]);
    const { usuario } = abrir();
    const confirmacao = await abrirConfirmacao(usuario);
    await usuario.click(within(confirmacao).getByRole('button', { name: /^Receber R\$/ }));

    expect(await screen.findByText(titulo)).toBeInTheDocument();
    expect(await screen.findByText('Liquidado')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Ver recebíveis' }));
    await waitFor(() => expect(screen.queryByText(titulo)).not.toBeInTheDocument());
  });

  it('Voltar fecha e devolve o foco, sem enviar', async () => {
    const chaves = api([recebivel()]);
    const { usuario } = abrir();
    const confirmacao = await abrirConfirmacao(usuario);
    await usuario.click(within(confirmacao).getByRole('button', { name: 'Voltar' }));

    expect(screen.getByRole('button', { name: 'Antecipar Kit 3 Pacotes' })).toHaveFocus();
    expect(chaves).toEqual([]);
  });

  it('erro inesperado mantém a confirmação, com código de suporte', async () => {
    api([recebivel()], [() => problema('x', 500)]);
    const { usuario } = abrir();
    const confirmacao = await abrirConfirmacao(usuario);
    await usuario.click(within(confirmacao).getByRole('button', { name: /^Receber R\$/ }));
    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
    expect(confirmacao).toBeInTheDocument();
  });
});
