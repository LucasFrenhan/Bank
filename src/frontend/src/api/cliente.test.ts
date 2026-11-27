import { http, HttpResponse } from 'msw';
import { criarSessao, salvarSessao } from '../sessao/armazenamento';
import { comprovante, PRAZOS_SESSAO, problema, tokenFalso } from '../testes/handlers';
import { servidor } from '../testes/setup';
import { api, integrarSessao } from './cliente';

/** Renovação e repetição única no 401 (specs/016-sessao-refresh-token, FR-014, FR-015, FR-018; research R9). */
describe('cliente da API: 401 → renova e repete uma vez', () => {
  let renovacoes: number;
  let naoAutenticado: number;

  beforeEach(() => {
    renovacoes = 0;
    naoAutenticado = 0;
    salvarSessao(criarSessao(tokenFalso(), PRAZOS_SESSAO)!);
    integrarSessao({
      renovar: async () => {
        renovacoes++;
        await new Promise((resolve) => setTimeout(resolve, 10));
        salvarSessao(criarSessao(tokenFalso('PessoaFisica', 'novo@exemplo.com.br'), PRAZOS_SESSAO)!);
        return true;
      },
      aoNaoAutenticado: () => {
        naoAutenticado++;
      },
    });
  });

  it('repete a transferência com o mesmo corpo, a mesma Idempotency-Key e o token novo', async () => {
    const recebidas: { corpo: string; chave: string | null; autorizacao: string | null }[] = [];
    servidor.use(
      http.post('*/api/v1/transferencias', async ({ request }) => {
        recebidas.push({ corpo: await request.text(), chave: request.headers.get('Idempotency-Key'), autorizacao: request.headers.get('Authorization') });
        return recebidas.length === 1 ? problema('nao-autenticado', 401) : HttpResponse.json(comprovante(), { status: 201 });
      }),
    );

    const resultado = await api.transferir({ documentoDestino: '12ABC34501DE35', valor: 2 }, 'chave-1');

    expect(resultado.ok).toBe(true);
    expect(recebidas).toHaveLength(2);
    expect(recebidas[1]!.corpo).toBe(recebidas[0]!.corpo);
    expect(recebidas.map((r) => r.chave)).toEqual(['chave-1', 'chave-1']);
    expect(recebidas[0]!.autorizacao).toBe(`Bearer ${tokenFalso()}`);
    expect(recebidas[1]!.autorizacao).toBe(`Bearer ${tokenFalso('PessoaFisica', 'novo@exemplo.com.br')}`);
    expect(renovacoes).toBe(1);
    expect(naoAutenticado).toBe(0);
  });

  it('401 também na repetição encerra a sessão (uma única repetição)', async () => {
    let chamadas = 0;
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => {
        chamadas++;
        return problema('nao-autenticado', 401);
      }),
    );

    const resultado = await api.obterMinhaCarteira();

    expect(resultado.ok).toBe(false);
    expect(chamadas).toBe(2);
    expect(renovacoes).toBe(1);
    expect(naoAutenticado).toBe(1);
  });

  it('renovação recusada encerra a sessão sem repetir', async () => {
    integrarSessao({ renovar: () => Promise.resolve(false) });
    let chamadas = 0;
    servidor.use(
      http.get('*/api/v1/carteiras/minha', () => {
        chamadas++;
        return problema('nao-autenticado', 401);
      }),
    );

    await api.obterMinhaCarteira();

    expect(chamadas).toBe(1);
    expect(naoAutenticado).toBe(1);
  });

  it.each([
    ['login', () => api.entrar('a@b.com', 'x'), '*/api/v1/autenticacao/login'],
    ['renovar', () => api.renovar(), '*/api/v1/autenticacao/renovar'],
  ] as const)('401 em /autenticacao/%s nunca dispara renovação nem leva Authorization', async (_, chamar, rota) => {
    let autorizacao: string | null = 'nao-lida';
    servidor.use(
      http.post(rota, ({ request }) => {
        autorizacao = request.headers.get('Authorization');
        return problema('nao-autenticado', 401);
      }),
    );

    await chamar();

    expect(autorizacao).toBeNull();
    expect(renovacoes).toBe(0);
    expect(naoAutenticado).toBe(0);
  });

  it('sem sessão (sem Authorization), um 401 não renova', async () => {
    salvarSessao(null as never);
    servidor.use(http.get('*/api/v1/carteiras/minha', () => problema('nao-autenticado', 401)));

    await api.obterMinhaCarteira();

    expect(renovacoes).toBe(0);
  });
});
