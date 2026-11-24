import { act, renderHook, waitFor } from '@testing-library/react';
import { delay, http, HttpResponse } from 'msw';
import { DADOS_CARTAO, problema, TRACE_ID } from '../testes/handlers';
import { servidor } from '../testes/setup';
import { TEMPO_EXIBICAO_MS, useDadosCartao } from './useDadosCartao';

/** Número e CVV só em memória e por pouco tempo (specs/015, FR-007, FR-008; research R5). */

function esconderAba(oculta: boolean) {
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue(oculta ? 'hidden' : 'visible');
  document.dispatchEvent(new Event('visibilitychange'));
}

beforeEach(() => {
  servidor.use(http.get('*/api/v1/cartoes/meu/dados', () => HttpResponse.json(DADOS_CARTAO)));
});

afterEach(() => {
  vi.restoreAllMocks();
});

async function exibido() {
  const hook = renderHook(() => useDadosCartao());
  await act(() => hook.result.current.exibir());
  expect(hook.result.current.estado).toEqual({ estado: 'exibido', dados: DADOS_CARTAO });
  return hook;
}

describe('useDadosCartao', () => {
  it('começa oculto, busca só no pedido e oculta no clique', async () => {
    const pedidos: string[] = [];
    servidor.events.on('request:start', ({ request }) => pedidos.push(request.url));
    const { result } = renderHook(() => useDadosCartao());
    expect(result.current.estado).toEqual({ estado: 'oculto' });
    expect(pedidos).toEqual([]);

    await act(() => result.current.exibir());
    expect(result.current.estado.estado).toBe('exibido');
    act(() => result.current.ocultar());
    expect(result.current.estado).toEqual({ estado: 'oculto' });
    servidor.events.removeAllListeners();
  });

  it('oculta ao esconder a aba e não volta sozinho', async () => {
    const { result } = await exibido();
    act(() => esconderAba(true));
    expect(result.current.estado).toEqual({ estado: 'oculto' });
    act(() => esconderAba(false));
    expect(result.current.estado).toEqual({ estado: 'oculto' });
  });

  it('oculta no pagehide (cache de volta do navegador)', async () => {
    const { result } = await exibido();
    act(() => void window.dispatchEvent(new Event('pagehide')));
    expect(result.current.estado).toEqual({ estado: 'oculto' });
  });

  it('oculta depois de 1 minuto, não antes', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['setTimeout', 'clearTimeout'] });
    const { result } = await exibido();

    act(() => void vi.advanceTimersByTime(TEMPO_EXIBICAO_MS - 2_000));
    expect(result.current.estado.estado).toBe('exibido');
    act(() => void vi.advanceTimersByTime(2_500));
    expect(result.current.estado).toEqual({ estado: 'oculto' });
    expect(TEMPO_EXIBICAO_MS).toBe(60_000);
  });

  it('resposta que chega depois de ocultar é descartada', async () => {
    servidor.use(
      http.get('*/api/v1/cartoes/meu/dados', async () => {
        await delay(50);
        return HttpResponse.json(DADOS_CARTAO);
      }),
    );
    const { result } = renderHook(() => useDadosCartao());
    let pendente: Promise<void> = Promise.resolve();
    act(() => {
      pendente = result.current.exibir();
    });
    expect(result.current.estado.estado).toBe('buscando');
    act(() => esconderAba(true));
    await act(() => pendente);
    await new Promise((r) => setTimeout(r, 80));

    expect(result.current.estado).toEqual({ estado: 'oculto' });
  });

  it('erro mantém os dados ocultos, com o código de suporte', async () => {
    servidor.use(http.get('*/api/v1/cartoes/meu/dados', () => problema('algo', 500)));
    const { result } = renderHook(() => useDadosCartao());
    await act(() => result.current.exibir());

    await waitFor(() => expect(result.current.estado).toMatchObject({ estado: 'erro', erro: { traceId: TRACE_ID } }));
  });

  it('nada fica no armazenamento', async () => {
    const { unmount } = await exibido();
    unmount();

    const tudo = JSON.stringify({ ...sessionStorage }) + JSON.stringify({ ...localStorage });
    expect(tudo).not.toContain(DADOS_CARTAO.numero);
    expect(tudo).not.toContain('7899');
  });
});
