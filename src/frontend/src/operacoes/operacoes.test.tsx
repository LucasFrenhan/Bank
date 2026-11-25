import { act, renderHook } from '@testing-library/react';
import type { Resultado } from '../api/cliente';
import { removerSessao } from '../sessao/armazenamento';
import { CHAVE_OPERACOES, chaveDaOperacao, descartarChave, limparOperacoes } from './chaves';
import { useOperacao } from './useOperacao';

/** Chave de idempotência por operação (specs/015, FR-016, SC-004; research R6). */

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const falha = (codigo: string): Resultado<string> => ({ ok: false, erro: { codigo, mensagem: 'x' } });
const sucesso: Resultado<string> = { ok: true, dados: 'ok', status: 200 };

afterEach(() => {
  vi.restoreAllMocks();
  limparOperacoes();
});

describe('chaves', () => {
  it('uma por operação, mantida ao recarregar; só operação e chave são gravadas', () => {
    const chave = chaveDaOperacao('cobranca:7K3F9QXM');

    expect(chave).toMatch(UUID);
    expect(chaveDaOperacao('cobranca:7K3F9QXM')).toBe(chave);
    expect(chaveDaOperacao('fatura:2026-12')).not.toBe(chave);
    const gravado = JSON.parse(sessionStorage.getItem(CHAVE_OPERACOES)!) as Record<string, string>;
    expect(Object.keys(gravado)).toEqual(['cobranca:7K3F9QXM', 'fatura:2026-12']);
    expect(Object.values(gravado).every((v) => UUID.test(v))).toBe(true);
  });

  it('descartar troca só a operação; sair apaga todas', () => {
    const a = chaveDaOperacao('a');
    const b = chaveDaOperacao('b');
    descartarChave('a');

    expect(chaveDaOperacao('a')).not.toBe(a);
    expect(chaveDaOperacao('b')).toBe(b);

    removerSessao();
    expect(sessionStorage.getItem(CHAVE_OPERACOES)).toBeNull();
    expect(chaveDaOperacao('b')).not.toBe(b);
  });

  it('sem armazenamento ou com conteúdo inválido, funciona', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });
    const chave = chaveDaOperacao('a');
    expect(chaveDaOperacao('a')).toBe(chave);
    vi.restoreAllMocks();

    limparOperacoes();
    sessionStorage.setItem(CHAVE_OPERACOES, '{');
    expect(chaveDaOperacao('a')).toMatch(UUID);
  });
});

describe('useOperacao', () => {
  async function enviar(hook: { current: ReturnType<typeof useOperacao> }, resposta: Resultado<string>) {
    let usada = '';
    await act(async () => {
      await hook.current.executar((chave) => {
        usada = chave;
        return Promise.resolve(resposta);
      });
    });
    return usada;
  }

  it.each(['rede', 'inesperado', 'cartao-nao-autorizado', 'limite-cartao-insuficiente', 'cartao-bloqueado-fatura-vencida', 'saldo-insuficiente', 'validacao'])(
    'mesma chave depois de %s',
    async (codigo) => {
      const { result } = renderHook(() => useOperacao('cobranca:X'));
      const primeira = await enviar(result, falha(codigo));

      expect(result.current.erro?.codigo).toBe(codigo);
      expect(await enviar(result, sucesso)).toBe(primeira);
      expect(result.current.erro).toBeNull();
    },
  );

  it.each([
    'idempotencia-divergente',
    'cobranca-nao-pendente',
    'cobranca-nao-encontrada',
    'fatura-nao-disponivel',
    'fatura-nao-encontrada',
    'recebivel-nao-antecipavel',
    'recebivel-nao-encontrado',
  ])('chave nova depois de %s', async (codigo) => {
    const { result } = renderHook(() => useOperacao('cobranca:X'));
    const primeira = await enviar(result, falha(codigo));

    expect(await enviar(result, sucesso)).not.toBe(primeira);
  });

  it('chave nova depois do sucesso', async () => {
    const { result } = renderHook(() => useOperacao('cobranca:X'));
    const primeira = await enviar(result, sucesso);

    expect(await enviar(result, sucesso)).not.toBe(primeira);
  });

  it('segundo clique não envia; sessão encerrada não vira aviso', async () => {
    const { result } = renderHook(() => useOperacao('cobranca:X'));
    const chamada = vi.fn(() => Promise.resolve(sucesso));
    await act(async () => {
      const [, segundo] = await Promise.all([result.current.executar(chamada), result.current.executar(chamada)]);
      expect(segundo).toBeNull();
    });
    expect(chamada).toHaveBeenCalledTimes(1);

    await enviar(result, { ok: false, erro: { codigo: 'nao-autenticado', mensagem: 'x', encerrarSessao: true } });
    expect(result.current.erro).toBeNull();
  });
});
