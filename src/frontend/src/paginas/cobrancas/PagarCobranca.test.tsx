import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { CHAVE_OPERACOES } from '../../operacoes/chaves';
import {
  cartaoCredito,
  CODIGO,
  COBRANCA_PAGA,
  cobranca,
  DADOS_CARTAO,
  ID_COBRANCA,
  pagamento,
  problema,
  TRACE_ID,
} from '../../testes/handlers';
import { renderizarApp } from '../../testes/renderizar';
import { servidor } from '../../testes/setup';

/** Pagamento da cobrança pela PF (specs/015, US3; FR-013 a FR-018). */

type Usuario = ReturnType<typeof renderizarApp>['usuario'];

interface Envio {
  chave: string | null;
  corpo: unknown;
  id: string;
}

/** API do pagamento: a cobrança e as respostas podem mudar durante o teste. */
function api(respostas: (() => Response | Promise<Response>)[] = []) {
  const estado = { cobranca: cobranca() as object, envios: [] as Envio[] };
  servidor.use(
    http.get('*/api/v1/cobrancas/codigo/:codigo', () => HttpResponse.json(estado.cobranca)),
    http.get('*/api/v1/cartoes/meu', () => HttpResponse.json(cartaoCredito({ limiteDisponivel: 150 }))),
    http.get('*/api/v1/cartoes/meu/dados', () => HttpResponse.json(DADOS_CARTAO)),
    http.post('*/api/v1/cobrancas/:id/pagar', async ({ request, params }) => {
      estado.envios.push({ chave: request.headers.get('Idempotency-Key'), corpo: await request.json(), id: String(params.id) });
      const resposta = respostas[estado.envios.length - 1];
      return (resposta ? resposta() : HttpResponse.json(pagamento()));
    }),
  );
  return estado;
}

const abrir = (rota = `/c/${CODIGO}`) => renderizarApp(rota, { sessao: 'PessoaFisica' });
const botaoPagar = () => screen.getByRole('button', { name: /^Pagar R\$/ });

async function preencher(usuario: Usuario, cvv = '123') {
  await usuario.click(await screen.findByRole('button', { name: 'Usar meu cartão' }));
  await waitFor(() => expect(screen.getByLabelText('Número do cartão')).toHaveValue('4000 0012 3456 7899'));
  await usuario.type(screen.getByLabelText('CVV'), cvv);
}

describe('Pagar cobrança', () => {
  it('mostra lojista, valor, descrição e o formulário com os campos reconhecíveis', async () => {
    api();
    abrir();

    expect(await screen.findByRole('heading', { level: 1, name: 'Pagar cobrança' })).toBeInTheDocument();
    const resumo = screen.getByRole('region', { name: 'Cobrança' });
    expect(resumo).toHaveTextContent(/R\$\s299,90/);
    expect(resumo).toHaveTextContent('Cafeteria Grão Especial LTDA');
    expect(resumo).toHaveTextContent('CNPJ 98.765.432/0001-99');
    expect(resumo).toHaveTextContent('Kit 3 Pacotes');
    for (const [rotulo, autocomplete] of [
      ['Nome impresso no cartão', 'cc-name'],
      ['Número do cartão', 'cc-number'],
      ['Validade', 'cc-exp'],
      ['CVV', 'cc-csc'],
    ]) {
      expect(await screen.findByLabelText(rotulo!)).toHaveAttribute('autocomplete', autocomplete);
    }
    expect(botaoPagar()).toHaveTextContent(/Pagar R\$\s299,90/);
  });

  it('sem sessão: entra e volta à mesma cobrança', async () => {
    api();
    const { usuario } = renderizarApp(`/c/${CODIGO}`);
    await usuario.type(await screen.findByLabelText('E-mail'), 'lucas@exemplo.com.br');
    await usuario.type(screen.getByLabelText('Senha'), 'SenhaForte@123');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Pagar cobrança' })).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent(`/c/${CODIGO}`);
  });

  it('"Usar meu cartão" preenche tudo menos o CVV, que recebe o foco', async () => {
    api();
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Usar meu cartão' }));

    await waitFor(() => expect(screen.getByLabelText('CVV')).toHaveFocus());
    expect(screen.getByLabelText('CVV')).toHaveValue('');
    expect(screen.getByLabelText('Nome impresso no cartão')).toHaveValue('LUCAS SILVA');
    expect(screen.getByLabelText('Validade')).toHaveValue('11/31');
  });

  it('"Usar meu cartão" apaga o CVV anterior no clique, mas não o digitado enquanto busca', async () => {
    api();
    servidor.use(
      http.get('*/api/v1/cartoes/meu/dados', async () => {
        await delay(60);
        return HttpResponse.json(DADOS_CARTAO);
      }),
    );
    const { usuario } = abrir();
    await usuario.type(await screen.findByLabelText('CVV'), '999');
    await usuario.click(screen.getByRole('button', { name: 'Usar meu cartão' }));
    expect(screen.getByLabelText('CVV')).toHaveValue('');
    fireEvent.change(screen.getByLabelText('CVV'), { target: { value: '123' } });

    await waitFor(() => expect(screen.getByLabelText('Número do cartão')).toHaveValue('4000 0012 3456 7899'));
    expect(screen.getByLabelText('CVV')).toHaveValue('123');
  });

  it('formato inválido: erro em cada campo, nada enviado', async () => {
    const estado = api();
    const { usuario } = abrir();
    await usuario.type(await screen.findByLabelText('Número do cartão'), '4000001234567890');
    await usuario.type(screen.getByLabelText('Validade'), '0120');
    await usuario.type(screen.getByLabelText('CVV'), '12');
    await usuario.click(botaoPagar());

    expect(screen.getByLabelText('Nome impresso no cartão')).toHaveAccessibleDescription('Informe o nome impresso no cartão');
    expect(screen.getByLabelText('Número do cartão')).toHaveAccessibleDescription('Confira o número do cartão');
    expect(screen.getByLabelText('Validade')).toHaveAccessibleDescription('O cartão está vencido');
    expect(screen.getByLabelText('CVV')).toHaveAccessibleDescription('O CVV tem 3 dígitos');
    await waitFor(() => expect(screen.getByLabelText('Nome impresso no cartão')).toHaveFocus());
    expect(estado.envios).toEqual([]);
  });

  it('aprovado: comprovante com foco no título, e um só envio no duplo clique', async () => {
    const estado = api([
      async () => {
        await delay(20);
        return HttpResponse.json(pagamento());
      },
    ]);
    const { usuario } = abrir();
    await preencher(usuario);
    await usuario.dblClick(botaoPagar());

    const titulo = await screen.findByRole('heading', { level: 1, name: 'Pagamento aprovado' });
    await waitFor(() => expect(titulo).toHaveFocus());
    const comprovante = screen.getByRole('region', { name: 'Comprovante' });
    for (const texto of ['Cafeteria Grão Especial LTDA', '98.765.432/0001-99', 'Kit 3 Pacotes', 'Visa final 7899', 'AUT84K']) {
      expect(comprovante).toHaveTextContent(texto);
    }
    expect(comprovante).toHaveTextContent(/24 nov 2026/);
    await waitFor(() => expect(document.title).toBe('Pagamento aprovado · One Bank'));
    expect(estado.envios).toHaveLength(1);
    expect(estado.envios[0]).toMatchObject({
      id: ID_COBRANCA,
      corpo: { nomeTitularCartao: 'LUCAS SILVA', numeroCartao: '4000001234567899', validade: '11/31', cvv: '123' },
    });
    expect(estado.envios[0]!.chave).toMatch(/^[0-9a-f-]{36}$/);
    expect(sessionStorage.getItem(CHAVE_OPERACOES)).toBe('{}');
  });

  it('não autorizado: não diz qual dado, limpa e foca o CVV, mantém o resto', async () => {
    api([() => problema('cartao-nao-autorizado', 422)]);
    const { usuario } = abrir();
    await preencher(usuario, '999');
    await usuario.click(botaoPagar());

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('Cartão não autorizado');
    expect(alerta).toHaveTextContent('Confira os dados do cartão e digite o CVV de novo.');
    expect(within(alerta).getByRole('link', { name: 'Ver os dados do meu cartão' })).toHaveAttribute('href', '/cartao');
    await waitFor(() => expect(screen.getByLabelText('CVV')).toHaveFocus());
    expect(screen.getByLabelText('CVV')).toHaveValue('');
    expect(screen.getByLabelText('Número do cartão')).toHaveValue('4000 0012 3456 7899');
  });

  it('fatura vencida leva às faturas; limite insuficiente mostra o limite atual', async () => {
    api([() => problema('cartao-bloqueado-fatura-vencida', 422), () => problema('limite-cartao-insuficiente', 422)]);
    const { usuario } = abrir();
    await preencher(usuario);
    await usuario.click(botaoPagar());
    expect(await screen.findByRole('link', { name: 'Ver faturas' })).toHaveAttribute('href', '/cartao/faturas');

    await usuario.type(screen.getByLabelText('CVV'), '123');
    await usuario.click(botaoPagar());
    expect(await screen.findByText(/Você tem R\$\s150,00 de limite disponível\./)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver limite' })).toHaveAttribute('href', '/cartao');
  });

  it('recusa e nova tentativa usam a mesma chave', async () => {
    const estado = api([() => problema('cartao-nao-autorizado', 422)]);
    const { usuario } = abrir();
    await preencher(usuario, '999');
    await usuario.click(botaoPagar());
    await screen.findByRole('alert');
    await usuario.type(screen.getByLabelText('CVV'), '123');
    await usuario.click(botaoPagar());

    await screen.findByRole('heading', { name: 'Pagamento aprovado' });
    expect(estado.envios[1]!.chave).toBe(estado.envios[0]!.chave);
  });

  it('falha de rede: mantém o CVV e "Tentar de novo" reenvia com a mesma chave', async () => {
    const estado = api([() => HttpResponse.error()]);
    const { usuario } = abrir();
    await preencher(usuario);
    await usuario.click(botaoPagar());
    const alerta = await screen.findByRole('alert');
    expect(screen.getByLabelText('CVV')).toHaveValue('123');
    await usuario.click(within(alerta).getByRole('button', { name: 'Tentar de novo' }));

    await screen.findByRole('heading', { name: 'Pagamento aprovado' });
    expect(estado.envios.map((e) => e.chave)).toEqual([estado.envios[0]!.chave, estado.envios[0]!.chave]);
  });

  it('recarregar mantém a chave e descarta os dados do cartão', async () => {
    const estado = api([() => HttpResponse.error()]);
    const primeira = abrir();
    await preencher(primeira.usuario);
    await primeira.usuario.click(botaoPagar());
    await screen.findByRole('alert');
    const guardado = JSON.stringify({ ...sessionStorage }) + JSON.stringify({ ...localStorage });
    for (const proibido of ['4000001234567899', '4000 0012', '11/31', 'LUCAS SILVA', '"123"']) expect(guardado).not.toContain(proibido);
    primeira.unmount();

    const { usuario } = abrir();
    expect(await screen.findByLabelText('Número do cartão')).toHaveValue('');
    await preencher(usuario);
    await usuario.click(botaoPagar());
    await screen.findByRole('heading', { name: 'Pagamento aprovado' });
    expect(estado.envios[1]!.chave).toBe(estado.envios[0]!.chave);
  });

  it('erros por campo da API', async () => {
    api([() => problema('validacao', 400, { errors: { cvv: ['CVV inválido.'], numeroCartao: ['Número inválido.'] } })]);
    const { usuario } = abrir();
    await preencher(usuario);
    await usuario.click(botaoPagar());

    await waitFor(() => expect(screen.getByLabelText('CVV')).toHaveAccessibleDescription('CVV inválido.'));
    expect(screen.getByLabelText('Número do cartão')).toHaveAccessibleDescription('Número inválido.');
    expect(screen.queryByText('Confira os campos destacados.')).not.toBeInTheDocument();
  });

  it('erro inesperado: código de suporte', async () => {
    api([() => problema('x', 500)]);
    const { usuario } = abrir();
    await preencher(usuario);
    await usuario.click(botaoPagar());
    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
  });

  it.each([
    ['paga por ela', COBRANCA_PAGA, 'Pagamento aprovado', undefined],
    ['paga por outra pessoa', { ...COBRANCA_PAGA, pagamento: null }, 'Cobrança', 'Esta cobrança não está mais disponível'],
    ['cancelada', { status: 'Cancelada', canceladaEm: '2026-11-24T17:00:00Z' }, 'Cobrança', 'Esta cobrança não está mais disponível'],
  ])('%s enquanto preenchia: mostra a situação', async (_, atual, titulo, aviso) => {
    const estado = api([
      () => {
        estado.cobranca = cobranca(atual);
        return problema('cobranca-nao-pendente', 409);
      },
    ]);
    const { usuario } = abrir();
    await preencher(usuario);
    await usuario.click(botaoPagar());

    expect(await screen.findByRole('heading', { level: 1, name: titulo })).toBeInTheDocument();
    if (aviso) {
      expect(screen.getByText(aviso)).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Ir para o cartão' })).toHaveAttribute('href', '/cartao');
    }
    expect(screen.queryByLabelText('CVV')).not.toBeInTheDocument();
  });

  it('chave usada com outros dados: já paga por ela vira o comprovante', async () => {
    const estado = api([
      () => {
        estado.cobranca = cobranca(COBRANCA_PAGA);
        return problema('idempotencia-divergente', 409);
      },
    ]);
    const { usuario } = abrir();
    await preencher(usuario);
    await usuario.click(botaoPagar());
    expect(await screen.findByRole('heading', { name: 'Pagamento aprovado' })).toBeInTheDocument();
  });

  it('chave usada com outros dados e ainda pendente: pede para conferir e troca a chave', async () => {
    const estado = api([() => problema('idempotencia-divergente', 409)]);
    const { usuario } = abrir();
    await preencher(usuario);
    await usuario.click(botaoPagar());
    expect(await screen.findByText('Confira antes de tentar de novo')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Conferir' }));

    await preencher(usuario);
    await usuario.click(botaoPagar());
    await screen.findByRole('heading', { name: 'Pagamento aprovado' });
    expect(estado.envios[1]!.chave).not.toBe(estado.envios[0]!.chave);
  });

  it('pagehide limpa o formulário (cache de volta do navegador)', async () => {
    api();
    const { usuario } = abrir();
    await preencher(usuario);
    act(() => void window.dispatchEvent(new Event('pagehide')));

    for (const rotulo of ['Nome impresso no cartão', 'Número do cartão', 'Validade', 'CVV']) expect(screen.getByLabelText(rotulo)).toHaveValue('');
  });

  it('sessão expirada no envio: vai ao login e os dados são descartados', async () => {
    api([() => problema('nao-autenticado', 401)]);
    const { usuario } = abrir();
    await preencher(usuario);
    await usuario.click(botaoPagar());

    expect(await screen.findByRole('button', { name: 'Entrar' })).toBeInTheDocument();
    expect(sessionStorage.getItem(CHAVE_OPERACOES)).toBeNull();
    await usuario.type(screen.getByLabelText('E-mail'), 'lucas@exemplo.com.br');
    await usuario.type(screen.getByLabelText('Senha'), 'SenhaForte@123');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));
    expect(await screen.findByLabelText('Número do cartão')).toHaveValue('');
  });

  it('erro em "Usar meu cartão" não impede digitar', async () => {
    api();
    servidor.use(http.get('*/api/v1/cartoes/meu/dados', () => problema('x', 500)));
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Usar meu cartão' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
    fireEvent.change(screen.getByLabelText('CVV'), { target: { value: '1a2b3' } });
    expect(screen.getByLabelText('CVV')).toHaveValue('123');
  });
});

describe('Quem pode pagar', () => {
  it('PF sem cartão: vê a cobrança e "Solicitar cartão"', async () => {
    api();
    servidor.use(http.get('*/api/v1/cartoes/meu', () => problema('cartao-nao-encontrado', 404)));
    abrir();

    expect(await screen.findByText('Você ainda não tem um cartão')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Solicitar cartão' })).toHaveAttribute('href', '/cartao');
    expect(screen.getByRole('region', { name: 'Cobrança' })).toHaveTextContent('Kit 3 Pacotes');
    expect(screen.queryByLabelText('CVV')).not.toBeInTheDocument();
  });

  it('outra PJ: dados sem ação', async () => {
    api();
    renderizarApp(`/c/${CODIGO}`, { sessao: 'PessoaJuridica' });

    expect(await screen.findByText('Só contas Pessoa Física pagam cobranças')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Cobrança' })).toHaveTextContent('Kit 3 Pacotes');
    expect(screen.queryByRole('button', { name: 'Copiar link' })).not.toBeInTheDocument();
  });

  it('reabrir o endereço depois de pagar mostra o comprovante', async () => {
    const estado = api();
    estado.cobranca = cobranca(COBRANCA_PAGA);
    abrir();
    expect(await screen.findByRole('heading', { name: 'Pagamento aprovado' })).toBeInTheDocument();
  });

  it.each(['/c/7k3f9qxm', '/c/7K3F%209QXM', '/c/7K3F-9QXM/'])('%s abre a mesma cobrança', async (rota) => {
    const pedidos: string[] = [];
    api();
    servidor.use(
      http.get('*/api/v1/cobrancas/codigo/:codigo', ({ params }) => {
        pedidos.push(String(params.codigo));
        return HttpResponse.json(cobranca());
      }),
    );
    abrir(rota);
    expect(await screen.findByRole('heading', { name: 'Pagar cobrança' })).toBeInTheDocument();
    expect(pedidos[0]).toBe('7K3F9QXM');
  });

  it.each(['/c/abc', '/c/0000-0000'])('%s: não encontrada, sem chamar a API', async (rota) => {
    let chamadas = 0;
    servidor.use(http.get('*/api/v1/cobrancas/codigo/:codigo', () => { chamadas++; return HttpResponse.json(cobranca()); }));
    abrir(rota);
    expect(await screen.findByText('Cobrança não encontrada')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ir para o início' })).toHaveAttribute('href', '/');
    expect(chamadas).toBe(0);
  });

  it('inexistente: não encontrada', async () => {
    servidor.use(http.get('*/api/v1/cobrancas/codigo/:codigo', () => problema('cobranca-nao-encontrada', 404)));
    abrir();
    expect(await screen.findByText('Cobrança não encontrada')).toBeInTheDocument();
  });
});
