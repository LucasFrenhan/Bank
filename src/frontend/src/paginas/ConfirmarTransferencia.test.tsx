import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { CHAVE_TRANSFERENCIA, TRANSFERENCIA_VAZIA, type TransferenciaEmAndamento } from '../transferencia/armazenamento';
import { carteira, comprovante, falhaEm, ID_TRANSFERENCIA, problema, TRACE_ID } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';

/** Etapa Confirmação e envio (specs/007: US1, US2, US3; FR-007 a FR-010, FR-012; research R2). */

const CHAVE = '019a8f50-1111-4890-abcd-ef1234567890';

function emAndamento(extra: Partial<TransferenciaEmAndamento> = {}): TransferenciaEmAndamento {
  return {
    ...TRANSFERENCIA_VAZIA,
    documento: '12.ABC.345/01DE-35',
    destinatario: { documento: '12ABC34501DE35', nome: 'Padaria Boa Massa Ltda', tipoConta: 'PessoaJuridica' },
    valorTexto: '45,90',
    descricao: 'Café e pães',
    chave: CHAVE,
    assinatura: '12ABC34501DE35|4590|Café e pães',
    ...extra,
  };
}

function abrirConfirmacao(extra: Partial<TransferenciaEmAndamento> = {}) {
  sessionStorage.setItem(CHAVE_TRANSFERENCIA, JSON.stringify(emAndamento(extra)));
  servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json({ ...carteira(), saldo: 842.5 })));
  return renderizarApp('/transferir/confirmar', { sessao: 'PessoaFisica' });
}

function capturarEnvios(resposta: () => Response = () => HttpResponse.json(comprovante(), { status: 201 })) {
  const envios: { chave: string | null; corpo: unknown }[] = [];
  servidor.use(
    http.post('*/api/v1/transferencias', async ({ request }) => {
      envios.push({ chave: request.headers.get('Idempotency-Key'), corpo: await request.json() });
      return resposta();
    }),
  );
  return envios;
}

function botaoTransferir() {
  return screen.getByRole('button', { name: /^Transferir R\$/ });
}

describe('Confirmação (US1)', () => {
  it('resumo com "Saldo depois" calculado em centavos e aviso de que não pode ser desfeita', async () => {
    abrirConfirmacao();

    expect(await screen.findByRole('heading', { level: 1, name: 'Confira antes de enviar' })).toBeInTheDocument();
    expect(screen.getByText('Você vai transferir')).toBeInTheDocument();
    const resumo = screen.getByRole('list', { name: 'Resumo da transferência' });
    expect(within(resumo).getByText('Padaria Boa Massa Ltda')).toBeInTheDocument();
    expect(within(resumo).getByText('CNPJ ••.•••.345/01DE-••')).toBeInTheDocument();
    expect(within(resumo).getByText('Pessoa Jurídica')).toBeInTheDocument();
    expect(within(resumo).getByText('Café e pães')).toBeInTheDocument();
    expect(await within(resumo).findByText(/R\$\s796,60/)).toBeInTheDocument();
    expect(screen.getByText(/não podem ser desfeitas/)).toBeInTheDocument();
    expect(botaoTransferir()).toHaveTextContent(/Transferir R\$\s45,90/);
    expect(within(screen.getByRole('list', { name: 'Etapas da transferência' })).getByText('Confirmar').closest('li')).toHaveAttribute('aria-current', 'step');
  });

  it('descrição só com espaços não aparece e não é enviada', async () => {
    const envios = capturarEnvios();
    const { usuario } = abrirConfirmacao({ descricao: '   ', assinatura: '12ABC34501DE35|4590|' });

    await screen.findByRole('heading', { name: 'Confira antes de enviar' });
    expect(screen.queryByText('Descrição')).not.toBeInTheDocument();

    await usuario.click(botaoTransferir());
    await waitFor(() => expect(envios).toHaveLength(1));
    expect(envios[0]?.corpo).toEqual({ documentoDestino: '12ABC34501DE35', valor: 45.9, descricao: null });
  });

  it('"Voltar" e "Editar dados" voltam à etapa Dados com tudo preenchido', async () => {
    const { usuario } = abrirConfirmacao();

    await usuario.click(await screen.findByRole('button', { name: 'Voltar' }));
    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/transferir$/));
    expect(await screen.findByLabelText('Valor')).toHaveValue('45,90');
    expect(screen.getByLabelText('Descrição (opcional)')).toHaveValue('Café e pães');
  });

  it('envia com Idempotency-Key e abre o comprovante', async () => {
    const envios = capturarEnvios();
    const { usuario } = abrirConfirmacao();

    await usuario.click(await screen.findByRole('button', { name: /^Transferir R\$/ }));

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(`/transferencias/${ID_TRANSFERENCIA}`));
    expect(envios).toEqual([{ chave: CHAVE, corpo: { documentoDestino: '12ABC34501DE35', valor: 45.9, descricao: 'Café e pães' } }]);
    expect(await screen.findByRole('heading', { name: 'Transferência concluída' })).toBeInTheDocument();
  });

  it('sem transferência em andamento válida, volta à etapa Dados', async () => {
    renderizarApp('/transferir/confirmar', { sessao: 'PessoaFisica' });

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/transferir$/));
  });
});

describe('Confirmação — recusas (US2)', () => {
  it('422 saldo-insuficiente recarrega a carteira e mostra o saldo atual', async () => {
    let leituras = 0;
    sessionStorage.setItem(CHAVE_TRANSFERENCIA, JSON.stringify(emAndamento()));
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => {
        leituras++;
        return HttpResponse.json({ ...carteira(), saldo: leituras === 1 ? 842.5 : 12.34 });
      }),
      falhaEm('post', '/api/v1/transferencias', 'saldo-insuficiente', 422),
    );
    const { usuario } = renderizarApp('/transferir/confirmar', { sessao: 'PessoaFisica' });

    await usuario.click(await screen.findByRole('button', { name: /^Transferir R\$/ }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('Saldo insuficiente');
    await waitFor(() => expect(aviso).toHaveTextContent(/Você tem R\$\s12,34 disponíveis/));
  });

  it.each([
    ['autotransferencia', 422, 'Esse documento é o seu'],
    ['destinatario-nao-encontrado', 404, 'Nenhuma conta Bank com esse documento.'],
    ['acesso-negado', 403, 'Indisponível para Pessoa Jurídica'],
  ])('%s → "%s"', async (codigo, status, texto) => {
    servidor.use(falhaEm('post', '/api/v1/transferencias', codigo, status));
    const { usuario } = abrirConfirmacao();

    await usuario.click(await screen.findByRole('button', { name: /^Transferir R\$/ }));

    expect(await screen.findByText(texto)).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/transferir/confirmar');
  });

  it('400 por campo mostra os erros e o link para editar os dados', async () => {
    servidor.use(falhaEm('post', '/api/v1/transferencias', 'validacao', 400, { errors: { valor: ['O valor deve ter no máximo 2 casas decimais.'] } }));
    const { usuario } = abrirConfirmacao();

    await usuario.click(await screen.findByRole('button', { name: /^Transferir R\$/ }));

    expect(await screen.findByText('O valor deve ter no máximo 2 casas decimais.')).toBeInTheDocument();
  });

  it('409 → "Essa transferência mudou" e o próximo envio usa outra chave', async () => {
    let primeira = true;
    const envios = capturarEnvios(() => {
      if (primeira) {
        primeira = false;
        return problema('idempotencia-divergente', 409);
      }
      return HttpResponse.json(comprovante(), { status: 201 });
    });
    const { usuario } = abrirConfirmacao();

    await usuario.click(await screen.findByRole('button', { name: /^Transferir R\$/ }));
    expect(await screen.findByText('Essa transferência mudou')).toBeInTheDocument();
    expect(screen.getByText('Os dados não batem com o envio anterior. Revise e envie de novo.')).toBeInTheDocument();

    await usuario.click(botaoTransferir());
    await waitFor(() => expect(envios).toHaveLength(2));
    expect(envios[1]?.chave).not.toBe(CHAVE);
    expect(envios[1]?.chave).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('erro inesperado mostra o código de suporte e permite tentar de novo com a mesma chave', async () => {
    let primeira = true;
    const envios = capturarEnvios(() => {
      if (primeira) {
        primeira = false;
        return problema('erro-interno', 500);
      }
      return HttpResponse.json(comprovante(), { status: 201 });
    });
    const { usuario } = abrirConfirmacao();

    await usuario.click(await screen.findByRole('button', { name: /^Transferir R\$/ }));
    expect(await screen.findByText('Algo deu errado')).toBeInTheDocument();
    expect(screen.getByText(TRACE_ID)).toBeInTheDocument();

    await usuario.click(botaoTransferir());
    await waitFor(() => expect(envios).toHaveLength(2));
    expect(envios.map((e) => e.chave)).toEqual([CHAVE, CHAVE]);
  });
});

describe('Confirmação — reenvio sem cobrar duas vezes (US3)', () => {
  it('duplo clique gera um único envio', async () => {
    let liberar: () => void = () => undefined;
    const envios: string[] = [];
    servidor.use(
      http.post('*/api/v1/transferencias', async ({ request }) => {
        envios.push(request.headers.get('Idempotency-Key') ?? '');
        await new Promise<void>((r) => (liberar = r));
        return HttpResponse.json(comprovante(), { status: 201 });
      }),
    );
    const { usuario } = abrirConfirmacao();

    const botao = await screen.findByRole('button', { name: /^Transferir R\$/ });
    await usuario.dblClick(botao);
    await usuario.click(botao);
    liberar();

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/transferencias/'));
    expect(envios).toHaveLength(1);
  });

  it('falha de rede e nova tentativa reaproveitam a chave', async () => {
    let primeira = true;
    const envios = capturarEnvios(() => {
      if (primeira) {
        primeira = false;
        return HttpResponse.error();
      }
      return HttpResponse.json(comprovante(), { status: 201 });
    });
    const { usuario } = abrirConfirmacao();

    await usuario.click(await screen.findByRole('button', { name: /^Transferir R\$/ }));
    expect(await screen.findByText('Não foi possível conectar. Verifique sua internet e tente de novo.')).toBeInTheDocument();

    await usuario.click(botaoTransferir());
    await waitFor(() => expect(envios).toHaveLength(2));
    expect(envios.map((e) => e.chave)).toEqual([CHAVE, CHAVE]);
  });

  it('200 → "Essa transferência já foi feita" com link para o comprovante', async () => {
    capturarEnvios(() => HttpResponse.json(comprovante(), { status: 200 }));
    const { usuario } = abrirConfirmacao({ concluida: ID_TRANSFERENCIA });

    await usuario.click(await screen.findByRole('button', { name: /^Transferir R\$/ }));

    expect(await screen.findByText('Essa transferência já foi feita')).toBeInTheDocument();
    expect(screen.getByText('Nada foi cobrado de novo.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver comprovante' })).toHaveAttribute('href', `/transferencias/${ID_TRANSFERENCIA}`);
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/transferir/confirmar');
  });

  it('recarregar a confirmação mantém a chave guardada na aba', async () => {
    const envios = capturarEnvios();
    const { usuario, unmount } = abrirConfirmacao();
    await screen.findByRole('heading', { name: 'Confira antes de enviar' });
    unmount();

    renderizarApp('/transferir/confirmar', { sessao: 'PessoaFisica' });
    await usuario.click(await screen.findByRole('button', { name: /^Transferir R\$/ }));

    await waitFor(() => expect(envios).toHaveLength(1));
    expect(envios[0]?.chave).toBe(CHAVE);
  });

  it('editar o valor gera nova chave; manter os dados mantém a chave', async () => {
    const { usuario } = abrirConfirmacao();

    await usuario.click(await screen.findByRole('button', { name: 'Voltar' }));
    await usuario.click(await screen.findByRole('button', { name: 'Continuar' }));
    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/transferir/confirmar'));
    expect((JSON.parse(sessionStorage.getItem(CHAVE_TRANSFERENCIA) ?? '{}') as { chave: string }).chave).toBe(CHAVE);

    await usuario.click(await screen.findByRole('button', { name: 'Voltar' }));
    const valor = await screen.findByLabelText('Valor');
    await usuario.clear(valor);
    await usuario.type(valor, '45,91');
    await usuario.click(screen.getByRole('button', { name: 'Continuar' }));
    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/transferir/confirmar'));
    expect((JSON.parse(sessionStorage.getItem(CHAVE_TRANSFERENCIA) ?? '{}') as { chave: string }).chave).not.toBe(CHAVE);
  });
});
