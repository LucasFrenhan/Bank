import { act, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { cartaoCredito, DADOS_CARTAO, fatura, paginado, problema, TRACE_ID } from '../../testes/handlers';
import { renderizarApp } from '../../testes/renderizar';
import { servidor } from '../../testes/setup';

/** Solicitar e ver o cartão (specs/015, US1; FR-005 a FR-009). */

const abrir = () => renderizarApp('/cartao', { sessao: 'PessoaFisica' });

function comCartao(extra: object = {}) {
  servidor.use(
    http.get('*/api/v1/cartoes/meu', () => HttpResponse.json(cartaoCredito(extra))),
    http.get('*/api/v1/cartoes/meu/dados', () => HttpResponse.json(DADOS_CARTAO)),
    paginado('/api/v1/faturas', [fatura()]),
  );
}

describe('Solicitar cartão', () => {
  it('explica, oferece as bandeiras sem marcar nenhuma e sem prometer limite', async () => {
    abrir();

    expect(await screen.findByRole('heading', { level: 1, name: 'Peça o seu cartão de crédito' })).toBeInTheDocument();
    for (const nome of ['Visa', 'Mastercard']) expect(screen.getByRole('radio', { name: nome })).not.toBeChecked();
    expect(screen.getByText('A bandeira não pode ser trocada depois, e cada CPF pode ter só um cartão.')).toBeInTheDocument();
    expect(screen.queryByText(/R\$/)).not.toBeInTheDocument();
  });

  it('sem bandeira: erro no campo e nada enviado', async () => {
    let enviados = 0;
    servidor.use(http.post('*/api/v1/cartoes', () => { enviados++; return HttpResponse.json(cartaoCredito(), { status: 201 }); }));
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Solicitar cartão' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Escolha a bandeira');
    expect(screen.getByRole('radio', { name: 'Visa' })).toHaveFocus();
    expect(enviados).toBe(0);
  });

  it('emite com a bandeira escolhida e mostra o cartão e os limites', async () => {
    const corpos: unknown[] = [];
    servidor.use(
      http.post('*/api/v1/cartoes', async ({ request }) => {
        corpos.push(await request.json());
        return HttpResponse.json(cartaoCredito({ bandeira: { codigo: '002', nome: 'Mastercard' }, limiteDisponivel: 5000, comprasALancar: 0 }), { status: 201 });
      }),
      paginado('/api/v1/faturas', [fatura()]),
    );
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('radio', { name: 'Mastercard' }));
    await usuario.dblClick(screen.getByRole('button', { name: 'Solicitar cartão Mastercard' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Cartão' })).toBeInTheDocument();
    expect(screen.getByText('Mastercard')).toBeInTheDocument();
    const limite = screen.getByRole('region', { name: 'Limite' });
    expect(limite).toHaveTextContent(/Limite disponívelR\$\s5\.000,00/);
    expect(corpos).toEqual([{ bandeira: '002' }]);
  });

  it('já existe (outra aba): informa e mostra o cartão', async () => {
    let emitido = false;
    servidor.use(
      http.get('*/api/v1/cartoes/meu', () => (emitido ? HttpResponse.json(cartaoCredito()) : problema('cartao-nao-encontrado', 404))),
      http.post('*/api/v1/cartoes', () => { emitido = true; return problema('cartao-ja-existente', 409); }),
      paginado('/api/v1/faturas', [fatura()]),
    );
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('radio', { name: 'Visa' }));
    await usuario.click(screen.getByRole('button', { name: 'Solicitar cartão Visa' }));

    expect(await screen.findByText('Você já tem um cartão')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Meu cartão' })).toBeInTheDocument();
  });

  it('erro inesperado com código de suporte e nova tentativa', async () => {
    let tentativas = 0;
    servidor.use(
      http.post('*/api/v1/cartoes', () => (++tentativas === 1 ? problema('x', 500) : HttpResponse.json(cartaoCredito(), { status: 201 }))),
      paginado('/api/v1/faturas', [fatura()]),
    );
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('radio', { name: 'Visa' }));
    await usuario.click(screen.getByRole('button', { name: 'Solicitar cartão Visa' }));
    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent(TRACE_ID);
    expect(screen.getByRole('radio', { name: 'Visa' })).toBeChecked();

    await usuario.click(within(alerta).getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('region', { name: 'Meu cartão' })).toBeInTheDocument();
  });
});

describe('Ver o cartão', () => {
  it('dados ocultos, limites e fatura atual com links', async () => {
    comCartao();
    abrir();

    const cartao = await screen.findByRole('region', { name: 'Meu cartão' });
    expect(cartao).toHaveTextContent('•••• •••• •••• 7899');
    expect(cartao).toHaveTextContent('LUCAS SILVA');
    expect(cartao).toHaveTextContent('11/31');
    expect(cartao).not.toHaveTextContent('4000');
    expect(within(cartao).queryByRole('button', { name: 'Copiar número' })).not.toBeInTheDocument();

    const limite = screen.getByRole('region', { name: 'Limite' });
    expect(limite).toHaveTextContent(/Limite disponívelR\$\s4\.700,10/);
    expect(limite).toHaveTextContent(/UsadoR\$\s299,90/);
    expect(limite).toHaveTextContent(/Limite totalR\$\s5\.000,00/);
    expect(limite).toHaveTextContent(/Compras que ainda vão entrar na faturaR\$\s299,90/);

    const atual = screen.getByRole('region', { name: 'Fatura atual' });
    expect(await within(atual).findByRole('heading', { name: 'Fatura de dezembro' })).toBeInTheDocument();
    expect(atual).toHaveTextContent('Aberta');
    expect(atual).toHaveTextContent('Fecha em 7 dez · Vence em 10 dez');
    expect(within(atual).getByRole('link', { name: 'Abrir fatura' })).toHaveAttribute('href', '/cartao/faturas/2026-12');
    expect(screen.getByRole('link', { name: 'Ver faturas' })).toHaveAttribute('href', '/cartao/faturas');
    await waitFor(() => expect(document.title).toBe('Cartão · One Bank'));
  });

  it('erro na fatura atual fica só na região dela', async () => {
    comCartao();
    servidor.use(http.get('*/api/v1/faturas', () => problema('x', 500)));
    abrir();

    const atual = await screen.findByRole('region', { name: 'Fatura atual' });
    expect(await within(atual).findByRole('alert')).toHaveTextContent(TRACE_ID);
    expect(screen.getByRole('region', { name: 'Meu cartão' })).toBeInTheDocument();
  });

  it('o olho exibe e oculta, com nome, leitura por dígito e anúncio', async () => {
    comCartao();
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Exibir dados do cartão' }));

    const cartao = screen.getByRole('region', { name: 'Meu cartão' });
    expect(await within(cartao).findByText('4000 0012 3456 7899')).toHaveAttribute('aria-hidden', 'true');
    expect(within(cartao).getByText(/4 0 0 0 0 0 1 2 3 4 5 6 7 8 9 9/)).toHaveClass('apenas-leitor-de-tela');
    expect(within(cartao).getByText('1 2 3')).toHaveClass('apenas-leitor-de-tela');
    expect(within(cartao).getByText('Dados do cartão exibidos')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Ocultar dados do cartão' }));
    expect(cartao).not.toHaveTextContent('4000 0012');
    expect(within(cartao).getByText('Dados do cartão ocultos')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Exibir dados do cartão' })).toBeInTheDocument();
  });

  it('copia só o número', async () => {
    comCartao();
    const { usuario } = abrir();
    const copiar = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue();
    await usuario.click(await screen.findByRole('button', { name: 'Exibir dados do cartão' }));
    await usuario.click(await screen.findByRole('button', { name: 'Copiar número' }));

    expect(copiar.mock.calls).toEqual([['4000001234567899']]);
    expect(await screen.findByText('Número copiado')).toBeInTheDocument();
    copiar.mockRestore();
  });

  it('trocar de aba oculta e anuncia', async () => {
    comCartao();
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Exibir dados do cartão' }));
    await screen.findByRole('button', { name: 'Copiar número' });

    const visibilidade = vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');
    act(() => void document.dispatchEvent(new Event('visibilitychange')));
    visibilidade.mockRestore();

    expect(screen.queryByRole('button', { name: 'Copiar número' })).not.toBeInTheDocument();
    expect(screen.getByText('Dados do cartão ocultos')).toBeInTheDocument();
  });

  it('erro ao exibir: código de suporte e cartão oculto', async () => {
    comCartao();
    servidor.use(http.get('*/api/v1/cartoes/meu/dados', () => problema('x', 500)));
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Exibir dados do cartão' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
    expect(screen.getByRole('button', { name: 'Exibir dados do cartão' })).toBeInTheDocument();
  });

  it('nome de 150 caracteres fica completo para leitores de tela', async () => {
    const nome = 'MARIA '.repeat(25).trim();
    comCartao({ nomeImpresso: nome });
    abrir();

    expect(await screen.findByText(nome)).toHaveAttribute('title', nome);
  });

  it('erro ao carregar: nova tentativa', async () => {
    let chamadas = 0;
    servidor.use(
      http.get('*/api/v1/cartoes/meu', () => (++chamadas === 1 ? problema('x', 500) : HttpResponse.json(cartaoCredito()))),
      paginado('/api/v1/faturas', [fatura()]),
    );
    const { usuario } = abrir();
    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
    await usuario.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByRole('region', { name: 'Meu cartão' })).toBeInTheDocument();
  });

  it('PJ: área indisponível, sem chamar a API', async () => {
    let chamadas = 0;
    servidor.use(http.get('*/api/v1/cartoes/meu', () => { chamadas++; return problema('acesso-negado', 403); }));
    renderizarApp('/cartao', { sessao: 'PessoaJuridica' });

    expect(await screen.findByText('Área indisponível para o seu tipo de conta')).toBeInTheDocument();
    expect(screen.getByText('O cartão é para contas Pessoa Física.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ir para o início' })).toHaveAttribute('href', '/');
    expect(chamadas).toBe(0);
  });
});

describe('Abrir cobrança pelo link ou código', () => {
  it.each(['7k3f 9qxm', 'https://onebank.exemplo/c/7K3F-9QXM'])('%s', async (texto) => {
    comCartao();
    const { usuario } = abrir();
    await usuario.type(await screen.findByLabelText('Link ou código da cobrança'), texto);
    await usuario.click(screen.getByRole('button', { name: 'Abrir' }));

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/c/7K3F-9QXM'));
  });

  it('inválido: erro no campo, que some ao digitar', async () => {
    comCartao();
    const { usuario } = abrir();
    const campo = await screen.findByLabelText('Link ou código da cobrança');
    await usuario.type(campo, 'abc');
    await usuario.click(screen.getByRole('button', { name: 'Abrir' }));

    expect(campo).toHaveAccessibleDescription('Confira o link ou o código');
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/cartao');
    await usuario.type(campo, 'd');
    expect(campo).not.toHaveAttribute('aria-invalid');
  });
});
