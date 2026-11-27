import { act, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { api } from '../api/cliente';
import { CHAVE_OPERACOES, chaveDaOperacao } from '../operacoes/chaves';
import { carteira, problema, sessaoEmitida, tokenFalso } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';
import { CHAVE_TRANSFERENCIA, salvarTransferencia, TRANSFERENCIA_VAZIA } from '../transferencia/armazenamento';
import { CHAVE_SESSAO_LEGADA, criarSessao, lerSessao, removerSessao, salvarSessao } from './armazenamento';
import { NOME_CANAL, type MensagemSessao } from './canalAbas';
import { decodificarClaims } from './claims';

/** Outra aba do mesmo navegador (o `BroadcastChannel` do jsdom entrega entre instâncias). */
function outraAba() {
  const canal = new BroadcastChannel(NOME_CANAL);
  const recebidas: MensagemSessao[] = [];
  canal.onmessage = (evento: MessageEvent<MensagemSessao>) => recebidas.push(evento.data);
  return {
    recebidas,
    publicar: (mensagem: MensagemSessao) => act(() => canal.postMessage(mensagem)),
    fechar: () => canal.close(),
  };
}

function valoresDoArmazenamento(): string[] {
  const valores: string[] = [];
  for (const armazenamento of [sessionStorage, localStorage]) {
    for (let i = 0; i < armazenamento.length; i++) {
      valores.push(armazenamento.getItem(armazenamento.key(i)!) ?? '');
    }
  }

  return valores;
}

describe('sessão em memória (specs/016, FR-005)', () => {
  it('cria a sessão com claims e prazos locais a partir dos segundos', () => {
    const sessao = criarSessao(tokenFalso('PessoaJuridica'), { sessaoExpiraPorInatividadeEmSegundos: 900, sessaoExpiraEmSegundos: 28800 }, 1_000)!;

    expect(sessao).toMatchObject({ usuarioId: 'usuario-1', tipoConta: 'PessoaJuridica', carteiraId: 'carteira-1' });
    expect(sessao.inatividadeAte).toBe(901_000);
    expect(sessao.absolutoAte).toBe(28_801_000);
    expect(sessao.antecedenciaAvisoMs).toBe(60_000);
  });

  it('com prazo de inatividade curto (e2e), o aviso vem na metade do prazo', () => {
    const sessao = criarSessao(tokenFalso(), { sessaoExpiraPorInatividadeEmSegundos: 25, sessaoExpiraEmSegundos: 28800 }, 0)!;

    expect(sessao.antecedenciaAvisoMs).toBe(12_500);
  });

  it.each(['nao-e-jwt', 'a.b.c', `x.${btoa('{"sub":"1"}')}.y`])('token ilegível ou sem claims é inválido: %s', (token) => {
    expect(decodificarClaims(token)).toBeNull();
    expect(criarSessao(token, { sessaoExpiraPorInatividadeEmSegundos: 1, sessaoExpiraEmSegundos: 1 })).toBeNull();
  });

  it('removerSessao apaga a memória, a transferência e as chaves de idempotência', () => {
    salvarSessao(criarSessao(tokenFalso(), { sessaoExpiraPorInatividadeEmSegundos: 900, sessaoExpiraEmSegundos: 28800 })!);
    salvarTransferencia({ ...TRANSFERENCIA_VAZIA, documento: 'x' });
    chaveDaOperacao('pagar-cobranca:1');

    removerSessao();

    expect(lerSessao()).toBeNull();
    expect(sessionStorage.getItem(CHAVE_TRANSFERENCIA)).toBeNull();
    expect(sessionStorage.getItem(CHAVE_OPERACOES)).toBeNull();
  });

  it('após entrar, nenhum token fica em sessionStorage ou localStorage', async () => {
    const { usuario } = renderizarApp('/entrar');
    await usuario.type(await screen.findByLabelText('E-mail'), 'lucas@exemplo.com.br');
    await usuario.type(screen.getByLabelText('Senha'), 'SenhaForte@123');
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }));

    await screen.findByText(/Olá, Lucas/);
    expect(valoresDoArmazenamento().some((v) => v.includes(tokenFalso()) || v.includes(tokenFalso().split('.')[1]!))).toBe(false);
  });

  it('a chave legada bank.sessao é removida na primeira carga', async () => {
    sessionStorage.setItem(CHAVE_SESSAO_LEGADA, JSON.stringify({ token: tokenFalso(), expiraEm: Date.now() + 60_000 }));

    renderizarApp('/');

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar'));
    expect(sessionStorage.getItem(CHAVE_SESSAO_LEGADA)).toBeNull();
  });
});

describe('renovação na carga e no 401 (specs/016, US1; FR-014, FR-017, FR-018)', () => {
  it('ao abrir a página, mostra o carregamento, renova e segue na rota pedida', async () => {
    let renovacoes = 0;
    servidor.use(
      http.post('*/api/v1/autenticacao/renovar', () => {
        renovacoes++;
        return HttpResponse.json(sessaoEmitida());
      }),
    );

    renderizarApp('/perfil');

    expect(screen.getByRole('status')).toHaveTextContent('Carregando sua sessão…');
    expect(await screen.findByText('O que sua conta pode fazer')).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/perfil');
    expect(renovacoes).toBe(1);
  });

  it('sem sessão no servidor, vai ao login guardando a rota (sem aviso de expiração)', async () => {
    renderizarApp('/extrato');

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/entrar$/));
    expect(screen.queryByText('Sua sessão expirou. Entre novamente.')).not.toBeInTheDocument();
  });

  it('envia Authorization: Bearer nas chamadas autenticadas', async () => {
    let autorizacao: string | null = null;
    servidor.use(
      http.get('*/api/v1/carteiras/minha', ({ request }) => {
        autorizacao = request.headers.get('Authorization');
        return HttpResponse.json(carteira());
      }),
    );

    renderizarApp('/', { sessao: 'PessoaFisica' });

    await screen.findByText(/Olá, Lucas/);
    expect(autorizacao).toBe(`Bearer ${tokenFalso('PessoaFisica')}`);
  });

  it('401 com renovação aceita repete a chamada e a pessoa continua na tela', async () => {
    let chamadas = 0;
    servidor.use(
      http.get('*/api/v1/usuarios/me', () => (++chamadas === 1 ? problema('nao-autenticado', 401) : undefined)),
      http.post('*/api/v1/autenticacao/renovar', () => HttpResponse.json(sessaoEmitida())),
    );

    renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('O que sua conta pode fazer')).toBeInTheDocument();
    await waitFor(() => expect(chamadas).toBe(2));
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/perfil');
  });

  it('401 com renovação recusada encerra a sessão e leva ao login com aviso e retorno', async () => {
    servidor.use(http.get('*/api/v1/usuarios/me', () => problema('nao-autenticado', 401)));

    renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Sua sessão expirou. Entre novamente.')).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar?expirada=1');
    expect(lerSessao()).toBeNull();
  });

  it('a renovação não apaga a transferência em revisão nem as chaves de idempotência', async () => {
    salvarTransferencia({ ...TRANSFERENCIA_VAZIA, documento: '12ABC34501DE35' });
    const chave = chaveDaOperacao('pagar-fatura:2026-11');
    servidor.use(http.post('*/api/v1/autenticacao/renovar', () => HttpResponse.json(sessaoEmitida())));

    renderizarApp('/perfil');
    await screen.findByText('O que sua conta pode fazer');

    expect(sessionStorage.getItem(CHAVE_TRANSFERENCIA)).toContain('12ABC34501DE35');
    expect(chaveDaOperacao('pagar-fatura:2026-11')).toBe(chave);
  });

  it('401 do login (sem token) não é tratado como sessão expirada', async () => {
    servidor.use(http.post('*/api/v1/autenticacao/login', () => problema('credenciais-invalidas', 401)));

    const resultado = await api.entrar('a@b.com', 'x');

    expect(resultado.ok).toBe(false);
  });
});

describe('sessão compartilhada entre abas (specs/016, FR-016)', () => {
  it('entrar em outra aba leva esta tela de login ao início', async () => {
    let renovar = false;
    servidor.use(
      http.post('*/api/v1/autenticacao/renovar', () => (renovar ? HttpResponse.json(sessaoEmitida()) : problema('nao-autenticado', 401))),
    );
    renderizarApp('/entrar');
    await screen.findByLabelText('E-mail');
    const aba = outraAba();

    renovar = true;
    aba.publicar({ tipo: 'entrou', sub: 'usuario-1' });

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/$/));
    aba.fechar();
  });

  it('entrar com outra pessoa em outra aba descarta a sessão desta aba e a operação em andamento', async () => {
    servidor.use(http.post('*/api/v1/autenticacao/renovar', () => HttpResponse.json(sessaoEmitida('PessoaJuridica', {}))));
    renderizarApp('/perfil', { sessao: 'PessoaFisica' });
    await screen.findByText('O que sua conta pode fazer');
    salvarTransferencia({ ...TRANSFERENCIA_VAZIA, documento: 'x' });
    const aba = outraAba();

    aba.publicar({ tipo: 'entrou', sub: 'outra-pessoa' });

    await waitFor(() => expect(sessionStorage.getItem(CHAVE_TRANSFERENCIA)).toBeNull());
    aba.fechar();
  });

  it('renovou em outra aba atualiza os prazos desta', async () => {
    renderizarApp('/perfil', { sessao: 'PessoaFisica', inatividadeEmSegundos: 30 });
    await screen.findByText('O que sua conta pode fazer');
    const antes = lerSessao()!.inatividadeAte;
    const aba = outraAba();

    aba.publicar({ tipo: 'renovou', sessaoExpiraPorInatividadeEmSegundos: 900, sessaoExpiraEmSegundos: 28000 });

    await waitFor(() => expect(lerSessao()!.inatividadeAte).toBeGreaterThan(antes + 800_000));
    aba.fechar();
  });

  it('a renovação publica os novos prazos para as outras abas, sem o token', async () => {
    servidor.use(http.post('*/api/v1/autenticacao/renovar', () => HttpResponse.json(sessaoEmitida())));
    const aba = outraAba();

    renderizarApp('/perfil');
    await screen.findByText('O que sua conta pode fazer');

    await waitFor(() => expect(aba.recebidas).toContainEqual({ tipo: 'renovou', sessaoExpiraPorInatividadeEmSegundos: 900, sessaoExpiraEmSegundos: 28800 }));
    expect(JSON.stringify(aba.recebidas)).not.toContain(tokenFalso().split('.')[1]);
    aba.fechar();
  });
});

describe('sair (specs/016, US2; FR-019)', () => {
  it('sair chama o servidor, publica saiu e volta ao login', async () => {
    let saiu = false;
    servidor.use(
      http.post('*/api/v1/autenticacao/sair', () => {
        saiu = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const aba = outraAba();
    const { usuario } = renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    await screen.findByText('O que sua conta pode fazer');
    await usuario.click(screen.getAllByRole('button', { name: 'Sair' })[0]!);

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/entrar$/));
    expect(lerSessao()).toBeNull();
    await waitFor(() => expect(saiu).toBe(true));
    await waitFor(() => expect(aba.recebidas).toContainEqual({ tipo: 'saiu' }));
    aba.fechar();
  });

  it('falha de rede no servidor não impede sair', async () => {
    servidor.use(http.post('*/api/v1/autenticacao/sair', () => HttpResponse.error()));
    const { usuario } = renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    await screen.findByText('O que sua conta pode fazer');
    await usuario.click(screen.getAllByRole('button', { name: 'Sair' })[0]!);

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/entrar$/));
  });

  it('saiu em outra aba leva esta ao login sem aviso e limpa a operação em andamento', async () => {
    renderizarApp('/perfil', { sessao: 'PessoaFisica' });
    await screen.findByText('O que sua conta pode fazer');
    salvarTransferencia({ ...TRANSFERENCIA_VAZIA, documento: 'x' });
    const aba = outraAba();

    aba.publicar({ tipo: 'saiu' });

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/entrar$/));
    expect(screen.queryByText('Sua sessão expirou. Entre novamente.')).not.toBeInTheDocument();
    expect(sessionStorage.getItem(CHAVE_TRANSFERENCIA)).toBeNull();
    aba.fechar();
  });

  it('uma renovação que termina depois de sair não restabelece a sessão', async () => {
    let liberar: () => void = () => undefined;
    const pendente = new Promise<void>((resolve) => {
      liberar = resolve;
    });
    servidor.use(
      http.get('*/api/v1/usuarios/me', () => problema('nao-autenticado', 401)),
      http.post('*/api/v1/autenticacao/renovar', async () => {
        await pendente;
        return HttpResponse.json(sessaoEmitida());
      }),
    );
    const { usuario } = renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    await usuario.click(await screen.findByRole('button', { name: 'Sair' }));
    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/entrar/));
    liberar();

    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(lerSessao()).toBeNull();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent(/^\/entrar/);
  });
});
