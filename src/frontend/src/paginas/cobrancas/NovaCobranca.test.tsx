import { screen, waitFor, within } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { CODIGO, cobranca, problema, TRACE_ID } from '../../testes/handlers';
import { renderizarApp } from '../../testes/renderizar';
import { servidor } from '../../testes/setup';

/** Emissão de cobrança (specs/015, US2; FR-010). */

const abrir = () => renderizarApp('/cobrancas/nova', { sessao: 'PessoaJuridica' });
const minha = (extra: object = {}) => cobranca({ lojista: { usuarioId: 'usuario-1', nome: 'Loja', documento: '98765432000199' }, ...extra });

function emitir(resposta: () => Response | Promise<Response>) {
  const corpos: unknown[] = [];
  servidor.use(
    http.post('*/api/v1/cobrancas', async ({ request }) => {
      corpos.push(await request.json());
      return resposta();
    }),
    http.get('*/api/v1/cobrancas/codigo/:codigo', () => HttpResponse.json(minha())),
  );
  return corpos;
}

describe('Nova cobrança', () => {
  it('emite uma só vez e mostra o detalhe com código e link', async () => {
    const corpos = emitir(async () => {
      await delay(20);
      return HttpResponse.json(minha(), { status: 201 });
    });
    const { usuario } = abrir();
    await usuario.type(await screen.findByLabelText('Valor'), '1.299,90');
    await usuario.type(screen.getByLabelText('Descrição'), '  Kit 3 Pacotes ');
    await usuario.dblClick(screen.getByRole('button', { name: 'Emitir cobrança' }));

    const link = await screen.findByRole('region', { name: 'Link de pagamento' });
    expect(link).toHaveTextContent(CODIGO);
    expect(screen.getByTestId('rota-atual')).toHaveTextContent(`/c/${CODIGO}`);
    expect(corpos).toEqual([{ valor: 1299.9, descricao: 'Kit 3 Pacotes' }]);
  });

  it('valor e descrição inválidos: erro em cada campo, nada enviado', async () => {
    const corpos = emitir(() => HttpResponse.json(minha(), { status: 201 }));
    const { usuario } = abrir();
    await usuario.type(await screen.findByLabelText('Valor'), '1,999');
    await usuario.type(screen.getByLabelText('Descrição'), '   ');
    await usuario.click(screen.getByRole('button', { name: 'Emitir cobrança' }));

    expect(screen.getByLabelText('Valor')).toHaveAccessibleDescription(/Use no máximo 2 casas decimais/);
    expect(screen.getByLabelText('Descrição')).toHaveAccessibleDescription(/Informe a descrição da cobrança/);
    await waitFor(() => expect(screen.getByLabelText('Valor')).toHaveFocus());
    expect(corpos).toEqual([]);

    await usuario.type(screen.getByLabelText('Valor'), '{Backspace}');
    expect(screen.getByLabelText('Valor')).not.toHaveAttribute('aria-invalid');
    expect(screen.getByLabelText('Descrição')).toHaveAttribute('aria-invalid');
  });

  it('contador e limite de 200 caracteres', async () => {
    const { usuario } = abrir();
    const campo = await screen.findByLabelText('Descrição');
    expect(campo).toHaveAttribute('maxLength', '200');
    await usuario.type(campo, 'Café');
    expect(campo).toHaveAccessibleDescription('4/200');
  });

  it('erros da API por campo mantêm o que foi digitado', async () => {
    emitir(() => problema('validacao', 400, { errors: { valor: ['Valor acima do permitido.'] } }));
    const { usuario } = abrir();
    await usuario.type(await screen.findByLabelText('Valor'), '10');
    await usuario.type(screen.getByLabelText('Descrição'), 'Café');
    await usuario.click(screen.getByRole('button', { name: 'Emitir cobrança' }));

    await waitFor(() => expect(screen.getByLabelText('Valor')).toHaveAccessibleDescription(/Valor acima do permitido\./));
    expect(screen.getByLabelText('Valor')).toHaveValue('10');
    expect(screen.getByLabelText('Descrição')).toHaveValue('Café');
  });

  it('falha de rede: orienta a conferir a lista, sem reenviar nem oferecer "Tentar de novo"', async () => {
    const corpos = emitir(() => HttpResponse.error());
    const { usuario } = abrir();
    await usuario.type(await screen.findByLabelText('Valor'), '10');
    await usuario.type(screen.getByLabelText('Descrição'), 'Café');
    await usuario.click(screen.getByRole('button', { name: 'Emitir cobrança' }));

    const alerta = await screen.findByRole('alert');
    expect(alerta).toHaveTextContent('Confira na lista se a cobrança foi criada antes de enviar de novo.');
    expect(within(alerta).getByRole('link', { name: 'Ver cobranças' })).toHaveAttribute('href', '/cobrancas');
    expect(within(alerta).queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Descrição')).toHaveValue('Café');
    expect(corpos).toHaveLength(1);
  });

  it('erro inesperado: código de suporte', async () => {
    emitir(() => problema('x', 500));
    const { usuario } = abrir();
    await usuario.type(await screen.findByLabelText('Valor'), '10');
    await usuario.type(screen.getByLabelText('Descrição'), 'Café');
    await usuario.click(screen.getByRole('button', { name: 'Emitir cobrança' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
  });
});
