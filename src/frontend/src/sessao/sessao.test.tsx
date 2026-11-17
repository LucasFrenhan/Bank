import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { api } from '../api/cliente';
import { carteira, problema, tokenFalso } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';
import { CHAVE_SESSAO, criarSessao, lerSessao, removerSessao, salvarSessao } from './armazenamento';
import { decodificarClaims } from './claims';

describe('armazenamento da sessão (research R5)', () => {
  it('salva só token e expiração em bank.sessao e lê as claims', () => {
    const sessao = criarSessao(tokenFalso('PessoaJuridica'), 3600, 1_000)!;
    salvarSessao(sessao);

    expect(JSON.parse(sessionStorage.getItem(CHAVE_SESSAO)!)).toEqual({ token: sessao.token, expiraEm: 3_601_000 });
    expect(lerSessao(2_000)).toMatchObject({ usuarioId: 'usuario-1', tipoConta: 'PessoaJuridica', carteiraId: 'carteira-1' });
  });

  it('descarta sessão vencida ao ler', () => {
    salvarSessao(criarSessao(tokenFalso(), 10, 0)!);

    expect(lerSessao(10_000)).toBeNull();
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toBeNull();
  });

  it.each(['nao-e-jwt', 'a.b.c', `x.${btoa('{"sub":"1"}')}.y`])('token ilegível ou sem claims é inválido: %s', (token) => {
    expect(decodificarClaims(token)).toBeNull();
    expect(criarSessao(token, 3600)).toBeNull();
  });

  it('removerSessao apaga a chave', () => {
    salvarSessao(criarSessao(tokenFalso(), 3600)!);
    removerSessao();
    expect(lerSessao()).toBeNull();
  });
});

describe('sessão na aplicação (US2; FR-009 a FR-013)', () => {
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

  it('401 em rota protegida encerra a sessão e leva ao login com aviso e retorno', async () => {
    servidor.use(http.get('*/api/v1/carteiras/minha', () => problema('nao-autenticado', 401)));

    renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    expect(await screen.findByText('Sua sessão expirou. Entre novamente.')).toBeInTheDocument();
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar?expirada=1');
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toBeNull();
  });

  it('401 do login (sem token) não é tratado como sessão expirada', async () => {
    servidor.use(http.post('*/api/v1/autenticacao/login', () => problema('credenciais-invalidas', 401)));

    const resultado = await api.entrar('a@b.com', 'x');

    expect(resultado.ok).toBe(false);
  });

  it('a sessão expira sozinha no prazo do token', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    renderizarApp('/', { sessao: 'PessoaFisica', expiraEmSegundos: 60 });
    await screen.findByText(/Olá, Lucas/);

    await vi.advanceTimersByTimeAsync(60_000);

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar?expirada=1'));
  });

  it('sair remove a sessão e volta ao login', async () => {
    const { usuario } = renderizarApp('/perfil', { sessao: 'PessoaFisica' });

    await screen.findByText('O que sua conta pode fazer');
    await usuario.click(screen.getAllByRole('button', { name: 'Sair' })[0]!);

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar'));
    expect(sessionStorage.getItem(CHAVE_SESSAO)).toBeNull();
  });
});
