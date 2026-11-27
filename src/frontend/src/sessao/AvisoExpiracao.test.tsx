import { act, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { sessaoEmitida } from '../testes/handlers';
import { renderizarApp } from '../testes/renderizar';
import { servidor } from '../testes/setup';
import { lerSessao } from './armazenamento';
import { NOME_CANAL, type MensagemSessao } from './canalAbas';

/** Aviso antes do fim da sessão (specs/016-sessao-refresh-token, FR-016b; contracts/frontend-sessao.md). */
describe('aviso de expiração', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  async function abrirPerfil(opcoes: { inatividadeEmSegundos?: number; absolutoEmSegundos?: number } = {}) {
    const render = renderizarApp('/perfil', { sessao: 'PessoaFisica', ...opcoes });
    await screen.findByText('O que sua conta pode fazer');
    return render;
  }

  it('aparece 60 s antes do fim por inatividade, com Continuar conectado e Sair', async () => {
    await abrirPerfil();

    await act(() => vi.advanceTimersByTimeAsync(839_000));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();

    await act(() => vi.advanceTimersByTimeAsync(2_000));
    const aviso = screen.getByRole('alertdialog', { name: 'Sua sessão vai expirar' });
    expect(aviso).toHaveTextContent(/você sairá em \d+ s se não houver atividade/i);
    expect(screen.getByRole('button', { name: 'Continuar conectado' })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Sair' }).length).toBeGreaterThan(0);
  });

  it('com prazo curto (e2e), aparece na metade do prazo', async () => {
    await abrirPerfil({ inatividadeEmSegundos: 25 });

    await act(() => vi.advanceTimersByTimeAsync(12_000));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();

    await act(() => vi.advanceTimersByTimeAsync(1_000));
    expect(screen.getByRole('alertdialog', { name: 'Sua sessão vai expirar' })).toBeInTheDocument();
  });

  it('Continuar conectado renova, fecha o aviso e publica os novos prazos', async () => {
    servidor.use(http.post('*/api/v1/autenticacao/renovar', () => HttpResponse.json(sessaoEmitida())));
    const outra = new BroadcastChannel(NOME_CANAL);
    const recebidas: MensagemSessao[] = [];
    outra.onmessage = (evento: MessageEvent<MensagemSessao>) => recebidas.push(evento.data);
    const { usuario } = await abrirPerfil({ inatividadeEmSegundos: 25 });

    await act(() => vi.advanceTimersByTimeAsync(13_000));
    await usuario.click(screen.getByRole('button', { name: 'Continuar conectado' }));

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(screen.getByTestId('rota-atual')).toHaveTextContent('/perfil');
    await waitFor(() => expect(recebidas.some((m) => m.tipo === 'renovou')).toBe(true));
    outra.close();
  });

  it('sem resposta, no fim do prazo vai ao login com sessão expirada', async () => {
    await abrirPerfil({ inatividadeEmSegundos: 25 });

    await act(() => vi.advanceTimersByTimeAsync(26_000));

    await waitFor(() => expect(screen.getByTestId('rota-atual')).toHaveTextContent('/entrar?expirada=1'));
    expect(lerSessao()).toBeNull();
  });

  it('renovou em outra aba fecha o aviso nesta', async () => {
    await abrirPerfil({ inatividadeEmSegundos: 25 });
    await act(() => vi.advanceTimersByTimeAsync(13_000));
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();

    const outra = new BroadcastChannel(NOME_CANAL);
    act(() => outra.postMessage({ tipo: 'renovou', sessaoExpiraPorInatividadeEmSegundos: 25, sessaoExpiraEmSegundos: 28000 } satisfies MensagemSessao));

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    outra.close();
  });

  it('antes do limite absoluto só informa, com Entendi', async () => {
    const { usuario } = await abrirPerfil({ inatividadeEmSegundos: 900, absolutoEmSegundos: 120 });

    await act(() => vi.advanceTimersByTimeAsync(61_000));
    const aviso = screen.getByRole('alertdialog', { name: 'Sua sessão vai terminar' });
    expect(aviso).toHaveTextContent(/precisará entrar de novo/);
    expect(screen.queryByRole('button', { name: 'Continuar conectado' })).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Entendi' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('anuncia o contador a leitores de tela em blocos de 15 s', async () => {
    await abrirPerfil();

    await act(() => vi.advanceTimersByTimeAsync(841_000));
    expect(screen.getByText(/Você sairá em cerca de 60 segundos/)).toHaveAttribute('aria-live', 'polite');

    await act(() => vi.advanceTimersByTimeAsync(5_000));
    expect(screen.getByText(/Você sairá em cerca de 60 segundos/)).toBeInTheDocument();
  });
});
