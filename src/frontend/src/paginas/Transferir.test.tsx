import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { CHAVE_TRANSFERENCIA, TRANSFERENCIA_VAZIA } from '../transferencia/armazenamento';
import { carteira, destinatario, falhaEm, limitesDe, problema, TRACE_ID } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';

/** Etapa Dados (specs/007: US1, US2; FR-001 a FR-006; research R6). */

function campoDocumento() {
  return screen.getByLabelText('CPF ou CNPJ de quem vai receber');
}

function campoValor() {
  return screen.getByLabelText('Valor');
}

function contarConsultas() {
  const documentos: string[] = [];
  servidor.use(
    http.post('*/api/v1/transferencias/destinatarios/consultar', async ({ request }) => {
      const corpo = (await request.json()) as { documento: string };
      documentos.push(corpo.documento);
      return HttpResponse.json(destinatario(corpo.documento.length === 11 ? 'PessoaFisica' : 'PessoaJuridica'));
    }),
  );
  return documentos;
}

describe('Transferir — etapa Dados (US1)', () => {
  it('título, etapas e texto de ajuda do valor com o saldo disponível', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json({ ...carteira(), saldo: 842.5 })));
    renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    expect(await screen.findByRole('heading', { level: 1, name: 'Transferir dinheiro' })).toBeInTheDocument();
    const etapas = screen.getByRole('list', { name: 'Etapas da transferência' });
    expect(within(etapas).getByText('Dados').closest('li')).toHaveAttribute('aria-current', 'step');
    expect(within(etapas).getByText('Confirmar')).toBeInTheDocument();
    expect(within(etapas).getByText('Comprovante')).toBeInTheDocument();
    expect(await screen.findByText(/Disponível: R\$\s842,50 · mínimo R\$\s0,01/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Voltar ao início/ })).toHaveAttribute('href', '/');
  });

  it('CNPJ em minúsculas vira maiúsculas, consulta na hora e mostra o titular mascarado', async () => {
    const consultas = contarConsultas();
    const { usuario } = renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    await usuario.type(await screen.findByLabelText('CPF ou CNPJ de quem vai receber'), '12abc34501de35');

    expect(campoDocumento()).toHaveValue('12.ABC.345/01DE-35');
    const titular = await screen.findByRole('group', { name: 'Destinatário' });
    expect(within(titular).getByText('Padaria Boa Massa Ltda')).toBeInTheDocument();
    expect(within(titular).getByText('PB')).toBeInTheDocument();
    expect(within(titular).getByText(/Pessoa Jurídica · CNPJ ••\.•••\.345\/01DE-••/)).toBeInTheDocument();
    expect(consultas).toEqual(['12ABC34501DE35']);
  });

  it('CPF com dígito válido é consultado após a pausa; com dígito inválido, só ao sair do campo', async () => {
    const consultas = contarConsultas();
    const { usuario } = renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    await usuario.type(await screen.findByLabelText('CPF ou CNPJ de quem vai receber'), '52998224725');
    expect(await screen.findByText('Mariana Costa', {}, { timeout: 2000 })).toBeInTheDocument();
    expect(consultas).toEqual(['52998224725']);

    await usuario.clear(campoDocumento());
    servidor.use(falhaEm('post', '/api/v1/transferencias/destinatarios/consultar', 'validacao', 400, { errors: { documento: ['CPF inválido. Confira os dígitos.'] } }));
    await usuario.type(campoDocumento(), '52998224726');
    await new Promise((r) => setTimeout(r, 700));
    expect(screen.queryByText('CPF inválido. Confira os dígitos.')).not.toBeInTheDocument();

    await usuario.tab();
    expect(await screen.findByText('CPF inválido. Confira os dígitos.')).toBeInTheDocument();
    expect(campoDocumento()).toHaveAttribute('aria-invalid', 'true');
  });

  it('"Buscando titular…" durante a consulta e resposta atrasada de documento anterior é ignorada', async () => {
    let liberarPrimeira: () => void = () => undefined;
    let chamadas = 0;
    servidor.use(
      http.post('*/api/v1/transferencias/destinatarios/consultar', async ({ request }) => {
        const { documento } = (await request.json()) as { documento: string };
        chamadas++;
        if (chamadas === 1) {
          await new Promise<void>((r) => (liberarPrimeira = r));
          return HttpResponse.json({ nome: 'Antigo Ltda', tipoConta: 'PessoaJuridica' });
        }
        return HttpResponse.json({ nome: `Novo ${documento.slice(0, 2)}`, tipoConta: 'PessoaJuridica' });
      }),
    );
    const { usuario } = renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    await usuario.type(await screen.findByLabelText('CPF ou CNPJ de quem vai receber'), '12ABC34501DE35');
    expect(await screen.findByText('Buscando titular…')).toBeInTheDocument();

    await usuario.clear(campoDocumento());
    await usuario.type(campoDocumento(), '98ABC34501DE35');
    expect(await screen.findByText('Novo 98')).toBeInTheDocument();

    liberarPrimeira();
    await new Promise((r) => setTimeout(r, 50));
    expect(screen.queryByText('Antigo Ltda')).not.toBeInTheDocument();
  });

  it('alterar o documento descarta o titular confirmado', async () => {
    const { usuario } = renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    await usuario.type(await screen.findByLabelText('CPF ou CNPJ de quem vai receber'), '12ABC34501DE35');
    expect(await screen.findByText('Padaria Boa Massa Ltda')).toBeInTheDocument();

    await usuario.type(campoDocumento(), '{Backspace}');
    expect(screen.queryByText('Padaria Boa Massa Ltda')).not.toBeInTheDocument();
  });

  it('contador da descrição e limite de 200 caracteres', async () => {
    const { usuario } = renderizarApp('/transferir', { sessao: 'PessoaFisica' });
    const descricao = await screen.findByLabelText('Descrição (opcional)');

    await usuario.type(descricao, 'Café e pães');
    expect(screen.getByText('11/200')).toBeInTheDocument();
    expect(descricao).toHaveAttribute('maxLength', '200');
  });

  it('"Continuar" com documento incompleto, sem titular ou sem valor mostra os erros no campo', async () => {
    const { usuario } = renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    await usuario.type(await screen.findByLabelText('CPF ou CNPJ de quem vai receber'), '529982');
    await usuario.click(screen.getByRole('button', { name: 'Continuar' }));

    expect(await screen.findByText('Informe o CPF ou CNPJ completo')).toBeInTheDocument();
    expect(screen.getByText('Informe o valor')).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/transferir');
  });

  it('dados válidos levam à confirmação; dados da aba são restaurados ao voltar', async () => {
    const { usuario } = renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    await usuario.type(await screen.findByLabelText('CPF ou CNPJ de quem vai receber'), '12ABC34501DE35');
    await screen.findByText('Padaria Boa Massa Ltda');
    await usuario.type(campoValor(), '4,50');
    await usuario.type(screen.getByLabelText('Descrição (opcional)'), 'Café');
    await usuario.click(screen.getByRole('button', { name: 'Continuar' }));

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/transferir/confirmar'));
    const guardada = JSON.parse(sessionStorage.getItem(CHAVE_TRANSFERENCIA) ?? '{}') as { chave: string; valorTexto: string };
    expect(guardada.valorTexto).toBe('4,50');
    expect(guardada.chave).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('restaura a transferência em andamento da aba', async () => {
    sessionStorage.setItem(
      CHAVE_TRANSFERENCIA,
      JSON.stringify({ ...TRANSFERENCIA_VAZIA, documento: '12.ABC.345/01DE-35', valorTexto: '4,50', descricao: 'Café', destinatario: { documento: '12ABC34501DE35', nome: 'Padaria Boa Massa Ltda', tipoConta: 'PessoaJuridica' } }),
    );
    renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    expect(await screen.findByLabelText('CPF ou CNPJ de quem vai receber')).toHaveValue('12.ABC.345/01DE-35');
    expect(campoValor()).toHaveValue('4,50');
    expect(screen.getByLabelText('Descrição (opcional)')).toHaveValue('Café');
    expect(screen.getByText('Padaria Boa Massa Ltda')).toBeInTheDocument();
  });

  it('após uma transferência concluída, começa vazia', async () => {
    sessionStorage.setItem(CHAVE_TRANSFERENCIA, JSON.stringify({ ...TRANSFERENCIA_VAZIA, documento: '12.ABC.345/01DE-35', valorTexto: '4,50', concluida: 'x' }));
    renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    expect(await screen.findByLabelText('CPF ou CNPJ de quem vai receber')).toHaveValue('');
    expect(campoValor()).toHaveValue('');
  });

  it('PJ vê "Indisponível para Pessoa Jurídica", sem formulário (FR-001)', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira('PessoaJuridica'))));
    renderizarApp('/transferir', { sessao: 'PessoaJuridica' });

    expect(await screen.findByText('Indisponível para Pessoa Jurídica')).toBeInTheDocument();
    expect(screen.getByText('Contas PJ recebem transferências e geram cobranças, mas não enviam.')).toBeInTheDocument();
    expect(screen.queryByLabelText('CPF ou CNPJ de quem vai receber')).not.toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /Voltar ao início/ }).length).toBeGreaterThan(0);
  });
});

describe('Transferir — recusas na etapa Dados (US2)', () => {
  async function digitarCnpj() {
    const resultado = renderizarApp('/transferir', { sessao: 'PessoaFisica' });
    await resultado.usuario.type(await screen.findByLabelText('CPF ou CNPJ de quem vai receber'), '12ABC34501DE35');
    return resultado;
  }

  it('404 → erro no campo "Nenhuma conta One Bank com esse documento."', async () => {
    servidor.use(falhaEm('post', '/api/v1/transferencias/destinatarios/consultar', 'destinatario-nao-encontrado', 404));
    await digitarCnpj();

    expect(await screen.findByText('Nenhuma conta One Bank com esse documento.')).toBeInTheDocument();
    expect(campoDocumento()).toHaveAttribute('aria-invalid', 'true');
  });

  it('422 autotransferencia → aviso "Esse documento é o seu"', async () => {
    servidor.use(falhaEm('post', '/api/v1/transferencias/destinatarios/consultar', 'autotransferencia', 422));
    const { usuario } = await digitarCnpj();

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('Esse documento é o seu');
    expect(aviso).toHaveTextContent('Não é possível transferir para a própria carteira.');

    await usuario.type(campoValor(), '1');
    await usuario.click(screen.getByRole('button', { name: 'Continuar' }));
    expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/transferir$/);
  });

  it('valor acima do saldo → aviso de saldo insuficiente antes do envio', async () => {
    const { usuario } = await digitarCnpj();
    await screen.findByText('Padaria Boa Massa Ltda');

    await usuario.type(campoValor(), '10,01');
    await usuario.click(screen.getByRole('button', { name: 'Continuar' }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('Saldo insuficiente');
    expect(aviso).toHaveTextContent(/Você tem R\$\s10,00 disponíveis\. Ajuste o valor para continuar\./);
    expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/transferir$/);
  });

  it.each([
    ['0', 'Informe um valor a partir de R$ 0,01'],
    ['10,005', 'Use no máximo 2 casas decimais'],
  ])('valor %s → erro de campo', async (valor, erro) => {
    const { usuario } = await digitarCnpj();
    await screen.findByText('Padaria Boa Massa Ltda');

    await usuario.type(campoValor(), valor);
    await usuario.click(screen.getByRole('button', { name: 'Continuar' }));

    expect(await screen.findByText(erro)).toBeInTheDocument();
    expect(campoValor()).toHaveAttribute('aria-invalid', 'true');
    expect(campoValor()).toHaveValue(valor);
  });

  it('403 na consulta → "Indisponível para Pessoa Jurídica"', async () => {
    servidor.use(falhaEm('post', '/api/v1/transferencias/destinatarios/consultar', 'acesso-negado', 403));
    await digitarCnpj();

    expect(await screen.findByText('Indisponível para Pessoa Jurídica')).toBeInTheDocument();
  });

  it('500 na consulta → "Algo deu errado" com o código de suporte, dados preservados', async () => {
    servidor.use(http.post('*/api/v1/transferencias/destinatarios/consultar', () => problema('erro-interno', 500)));
    await digitarCnpj();

    expect(await screen.findByText('Algo deu errado')).toBeInTheDocument();
    expect(screen.getByText(TRACE_ID)).toBeInTheDocument();
    expect(campoDocumento()).toHaveValue('12.ABC.345/01DE-35');
  });
});

describe('Transferir — limites (specs/009, US4; contracts/frontend.md)', () => {
  async function digitarCnpj() {
    const resultado = renderizarApp('/transferir', { sessao: 'PessoaFisica' });
    await resultado.usuario.type(await screen.findByLabelText('CPF ou CNPJ de quem vai receber'), '12ABC34501DE35');
    await screen.findByText('Padaria Boa Massa Ltda');
    return resultado;
  }

  it('dica mostra o limite agora (menor entre por operação e disponível hoje)', async () => {
    servidor.use(limitesDe({ porOperacao: 4, diario: 6, transferidoHoje: 3, disponivelHoje: 3 }));
    renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    expect(await screen.findByText(/Disponível: R\$\s10,00 · Limite agora: R\$\s3,00 · mínimo R\$\s0,01/)).toBeInTheDocument();
  });

  it('consulta de limites indisponível → dica como antes', async () => {
    servidor.use(falhaEm('get', '/api/v1/transferencias/limites', 'rede'));
    renderizarApp('/transferir', { sessao: 'PessoaFisica' });

    expect(await screen.findByText(/Disponível: R\$\s10,00 · mínimo R\$\s0,01/)).toBeInTheDocument();
    expect(screen.queryByText(/Limite agora/)).not.toBeInTheDocument();
  });

  it('acima do limite por operação (e do saldo) → aviso de limite, sem avançar', async () => {
    servidor.use(limitesDe({ porOperacao: 4, diario: 6, transferidoHoje: 0, disponivelHoje: 6 }));
    const { usuario } = await digitarCnpj();
    await screen.findByText(/Limite agora: R\$\s4,00/);

    await usuario.type(campoValor(), '50');
    await usuario.click(screen.getByRole('button', { name: 'Continuar' }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent('Limite excedido');
    expect(aviso).toHaveTextContent(/O limite por transferência é R\$\s4,00\. Ajuste o valor para continuar\./);
    expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/transferir$/);
  });

  it('valor igual ao limite avança para a confirmação', async () => {
    servidor.use(limitesDe({ porOperacao: 4, diario: 6, transferidoHoje: 0, disponivelHoje: 6 }));
    const { usuario } = await digitarCnpj();
    await screen.findByText(/Limite agora: R\$\s4,00/);

    await usuario.type(campoValor(), '4,00');
    await usuario.click(screen.getByRole('button', { name: 'Continuar' }));

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/transferir/confirmar'));
  });

  it('limite diário esgotado → "Você já usou todo o limite diário…"', async () => {
    servidor.use(limitesDe({ porOperacao: 4, diario: 6, transferidoHoje: 6, disponivelHoje: 0 }));
    const { usuario } = await digitarCnpj();
    await screen.findByText(/Limite agora: R\$\s0,00/);

    await usuario.type(campoValor(), '0,01');
    await usuario.click(screen.getByRole('button', { name: 'Continuar' }));

    const aviso = await screen.findByRole('alert');
    expect(aviso).toHaveTextContent(/Você já usou todo o limite diário de R\$\s6,00\. Ele renova à meia-noite \(horário de Brasília\)\./);
  });
});
