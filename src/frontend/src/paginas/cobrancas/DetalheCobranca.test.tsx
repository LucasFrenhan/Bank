import { act, screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { definirTelemetria } from '../../telemetria/ponte';
import { CODIGO, COBRANCA_PAGA, cobranca, problema, TRACE_ID } from '../../testes/handlers';
import { renderizarApp } from '../../testes/renderizar';
import { servidor } from '../../testes/setup';
import { INTERVALO_ATUALIZACAO_MS } from './useAtualizacaoAutomatica';

/** Cobrança vista por quem a emitiu (specs/015, US2; FR-012). */

const LOJA = { usuarioId: 'usuario-1', nome: 'Cafeteria', documento: '98765432000199' };
const minha = (extra: object = {}) => cobranca({ lojista: LOJA, ...extra });
const LINK = `${window.location.origin}/c/${CODIGO}`;

/** A resposta da consulta pode mudar durante o teste. */
function responder(inicial: object) {
  const estado = { atual: inicial, consultas: 0, falhar: false };
  servidor.use(
    http.get('*/api/v1/cobrancas/codigo/:codigo', () => {
      estado.consultas++;
      return estado.falhar ? HttpResponse.error() : HttpResponse.json(estado.atual);
    }),
  );
  return estado;
}

const abrir = () => renderizarApp(`/c/${CODIGO}`, { sessao: 'PessoaJuridica' });

afterEach(() => {
  vi.restoreAllMocks();
  Reflect.deleteProperty(navigator, 'share');
});

describe('Detalhe da cobrança', () => {
  it('pendente: valor, situação, código, link e última recusa', async () => {
    responder(minha({ ultimaRecusa: { motivo: 'LimiteInsuficiente', em: '2026-11-24T16:33:00Z' } }));
    abrir();

    expect(await screen.findByRole('heading', { level: 1, name: /R\$\s299,90/ })).toBeInTheDocument();
    const link = screen.getByRole('region', { name: 'Link de pagamento' });
    expect(link).toHaveTextContent(CODIGO);
    expect(link).toHaveTextContent(LINK);
    const detalhes = screen.getByRole('region', { name: 'Detalhes' });
    expect(detalhes).toHaveTextContent('Última recusaLimite insuficiente ·');
    expect(screen.getByText('Kit 3 Pacotes')).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Cobrança · One Bank'));
  });

  it('motivo de recusa e situação desconhecidos não quebram a tela', async () => {
    responder(minha({ status: 'Estornada', ultimaRecusa: { motivo: 'Novo', em: '2026-11-24T16:33:00Z' } }));
    abrir();

    expect(await screen.findByText('Estornada')).toBeInTheDocument();
    expect(screen.getByText(/Pagamento recusado ·/)).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Link de pagamento' })).not.toBeInTheDocument();
  });

  it('paga: pagamento e link para os recebíveis, sem código para compartilhar nem cancelar', async () => {
    responder(minha(COBRANCA_PAGA));
    abrir();

    const detalhes = await screen.findByRole('region', { name: 'Detalhes' });
    expect(screen.queryByText(CODIGO)).not.toBeInTheDocument(); // só enquanto pendente (FR-012)
    expect(detalhes).toHaveTextContent('CartãoVisa final 7899');
    expect(detalhes).toHaveTextContent('Código de autorizaçãoAUT84K');
    expect(within(detalhes).getByRole('link', { name: 'Ver recebíveis' })).toHaveAttribute('href', '/cobrancas/recebiveis');
    expect(screen.queryByRole('button', { name: 'Copiar link' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancelar cobrança' })).not.toBeInTheDocument();
  });

  it('copia só o link; sem área de transferência, orienta a copiar à mão', async () => {
    responder(minha());
    const { usuario } = abrir();
    const copiar = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue();
    await usuario.click(await screen.findByRole('button', { name: 'Copiar link' }));
    expect(copiar).toHaveBeenCalledWith(LINK);
    expect(await screen.findByText('Link copiado')).toBeInTheDocument();

    copiar.mockRejectedValue(new Error('negado'));
    await usuario.click(screen.getByRole('button', { name: 'Copiar link' }));
    expect(await screen.findByText(/Selecione o link e copie manualmente/)).toBeInTheDocument();
  });

  it('compartilhar só onde o aparelho permite, com o texto completo', async () => {
    responder(minha());
    const primeira = abrir();
    await screen.findByRole('button', { name: 'Copiar link' });
    expect(screen.queryByRole('button', { name: 'Compartilhar' })).not.toBeInTheDocument();
    primeira.unmount();

    const compartilhar = vi.fn<(dados: { text: string }) => Promise<void>>().mockResolvedValue();
    Object.defineProperty(navigator, 'share', { configurable: true, value: compartilhar });
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Compartilhar' }));
    expect(compartilhar.mock.calls[0]![0].text.replace(/\s/g, ' ')).toBe(
      `Cafeteria: R$ 299,90 — Kit 3 Pacotes. Pague com o cartão One Bank: ${LINK}`,
    );

    compartilhar.mockRejectedValue(new DOMException('x', 'AbortError'));
    await usuario.click(screen.getByRole('button', { name: 'Compartilhar' }));
    expect(screen.queryByText('Não foi possível compartilhar')).not.toBeInTheDocument();
    compartilhar.mockRejectedValue(new Error('falhou'));
    await usuario.click(screen.getByRole('button', { name: 'Compartilhar' }));
    expect(await screen.findByText('Não foi possível compartilhar')).toBeInTheDocument();
  });

  it('não encontrada e erro com nova tentativa', async () => {
    servidor.use(http.get('*/api/v1/cobrancas/codigo/:codigo', () => problema('cobranca-nao-encontrada', 404)));
    const primeira = abrir();
    expect(await screen.findByText('Cobrança não encontrada')).toBeInTheDocument();
    primeira.unmount();

    servidor.use(http.get('*/api/v1/cobrancas/codigo/:codigo', () => problema('x', 500)));
    abrir();
    expect(await screen.findByRole('alert')).toHaveTextContent(TRACE_ID);
    expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
  });
});

describe('Cancelar', () => {
  it('pede confirmação e cancela', async () => {
    const estado = responder(minha());
    let cancelamentos = 0;
    servidor.use(
      http.post('*/api/v1/cobrancas/:id/cancelar', () => {
        cancelamentos++;
        estado.atual = minha({ status: 'Cancelada', canceladaEm: '2026-11-24T17:02:00Z' });
        return HttpResponse.json(estado.atual);
      }),
    );
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Cancelar cobrança' }));
    const confirmacao = screen.getByRole('region', { name: 'Cancelar esta cobrança?' });
    expect(cancelamentos).toBe(0);
    await usuario.dblClick(within(confirmacao).getByRole('button', { name: 'Cancelar cobrança' }));

    expect(await screen.findByText('Cancelada')).toBeInTheDocument();
    expect(screen.getByText('Cobrança cancelada')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Link de pagamento' })).not.toBeInTheDocument();
    expect(cancelamentos).toBe(1);
  });

  it.each([
    ['Paga', COBRANCA_PAGA, 'Esta cobrança acabou de ser paga', 'Não é mais possível cancelar.'],
    ['Cancelada', { status: 'Cancelada', canceladaEm: '2026-11-24T17:02:00Z' }, 'Esta cobrança já foi cancelada', 'Veja a situação atualizada abaixo.'],
  ])('já %s: explica e atualiza', async (situacao, atual, titulo, detalhe) => {
    const estado = responder(minha());
    servidor.use(
      http.post('*/api/v1/cobrancas/:id/cancelar', () => {
        estado.atual = minha(atual);
        return problema('cobranca-nao-pendente', 409);
      }),
    );
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Cancelar cobrança' }));
    await usuario.click(within(screen.getByRole('region', { name: 'Cancelar esta cobrança?' })).getByRole('button', { name: 'Cancelar cobrança' }));

    expect(await screen.findByText(titulo)).toBeInTheDocument();
    expect(screen.getByText(detalhe)).toBeInTheDocument();
    expect(screen.getByText(situacao)).toBeInTheDocument();
    if (situacao === 'Paga') {
      await usuario.click(screen.getByRole('button', { name: 'Ver pagamento' }));
      expect(screen.getByRole('heading', { level: 1 })).toHaveFocus();
    } else {
      expect(screen.getByRole('link', { name: 'Ver cobranças' })).toHaveAttribute('href', '/cobrancas');
    }
  });

  it('Voltar e Esc não cancelam e devolvem o foco', async () => {
    responder(minha());
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Cancelar cobrança' }));
    await usuario.click(screen.getByRole('button', { name: 'Voltar' }));
    expect(screen.getByRole('button', { name: 'Cancelar cobrança' })).toHaveFocus();
  });
});

describe('Atualização automática', () => {
  const avancar = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true, toFake: ['setInterval', 'clearInterval'] });
  });

  it('a cada 5 s, mostra e anuncia o pagamento, e para', async () => {
    const estado = responder(minha());
    abrir();
    await screen.findByRole('button', { name: 'Copiar link' });
    expect(estado.consultas).toBe(1);
    await avancar(INTERVALO_ATUALIZACAO_MS);
    await waitFor(() => expect(estado.consultas).toBe(2));

    estado.atual = minha(COBRANCA_PAGA);
    await avancar(INTERVALO_ATUALIZACAO_MS);
    expect(await screen.findByText('Paga')).toBeInTheDocument();
    expect(screen.getByText('Cobrança paga')).toHaveAttribute('role', 'status');
    const consultas = estado.consultas;
    await avancar(INTERVALO_ATUALIZACAO_MS * 3);
    expect(estado.consultas).toBe(consultas);
    expect(INTERVALO_ATUALIZACAO_MS).toBe(5_000);
  });

  it('com a aba escondida não consulta; ao voltar, atualiza na hora', async () => {
    const estado = responder(minha());
    abrir();
    await screen.findByRole('button', { name: 'Copiar link' });
    const visibilidade = vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');
    await avancar(INTERVALO_ATUALIZACAO_MS * 3);
    expect(estado.consultas).toBe(1);

    visibilidade.mockReturnValue('visible');
    estado.atual = minha(COBRANCA_PAGA);
    act(() => void document.dispatchEvent(new Event('visibilitychange')));
    expect(await screen.findByText('Paga')).toBeInTheDocument();
  });

  it('falha não muda a tela nem vira log, e tenta de novo', async () => {
    const falhas: string[] = [];
    definirTelemetria({ registrarNavegacao: () => undefined, registrarFalhaApi: (f) => falhas.push(f.codigo) });
    const estado = responder(minha());
    abrir();
    await screen.findByRole('button', { name: 'Copiar link' });
    estado.falhar = true;
    await avancar(INTERVALO_ATUALIZACAO_MS);
    await waitFor(() => expect(estado.consultas).toBe(2));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Copiar link' })).toBeInTheDocument();

    estado.falhar = false;
    estado.atual = minha(COBRANCA_PAGA);
    await avancar(INTERVALO_ATUALIZACAO_MS);
    expect(await screen.findByText('Paga')).toBeInTheDocument();
    definirTelemetria(null);
    expect(falhas).toEqual([]);
  });

  it('o pagamento substitui a confirmação de cancelamento e leva o foco ao título', async () => {
    const estado = responder(minha());
    const { usuario } = abrir();
    await usuario.click(await screen.findByRole('button', { name: 'Cancelar cobrança' }));
    expect(screen.getByRole('region', { name: 'Cancelar esta cobrança?' })).toBeInTheDocument();

    estado.atual = minha(COBRANCA_PAGA);
    await avancar(INTERVALO_ATUALIZACAO_MS);
    expect(await screen.findByText('Paga')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Cancelar esta cobrança?' })).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('heading', { level: 1 })).toHaveFocus());
  });

  it('cobrança cancelada não consulta de novo', async () => {
    const estado = responder(minha({ status: 'Cancelada', canceladaEm: '2026-11-24T17:02:00Z' }));
    abrir();
    await screen.findByText('Cancelada');
    await avancar(INTERVALO_ATUALIZACAO_MS * 2);
    expect(estado.consultas).toBe(1);
  });
});
