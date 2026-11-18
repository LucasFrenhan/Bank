import { CHAVE_SESSAO, removerSessao } from '../sessao/armazenamento';
import { assinatura, gerarChave, prepararConfirmacao } from './chave';
import {
  CHAVE_COMPROVANTES,
  CHAVE_TRANSFERENCIA,
  guardarComprovante,
  lerComprovante,
  lerTransferencia,
  limparTransferencia,
  salvarTransferencia,
  TRANSFERENCIA_VAZIA,
  type ComprovanteGuardado,
} from './armazenamento';

const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

function comprovante(id: string): ComprovanteGuardado {
  return {
    transferenciaId: id,
    chaveIdempotencia: 'chave',
    pagador: { usuarioId: 'usuario-1', nome: 'Lucas Silva' },
    recebedor: { usuarioId: 'usuario-2', nome: 'Padaria Boa Massa Ltda', documento: '12ABC34501DE35', tipoConta: 'PessoaJuridica' },
    valor: 45.9,
    descricao: 'Café e pães',
    status: 'Concluida',
    criadoEm: '2026-11-17T11:10:00Z',
  };
}

describe('chave de idempotência (research R2)', () => {
  it('gera UUID v4, inclusive sem crypto.randomUUID', () => {
    expect(gerarChave()).toMatch(UUID_V4);

    const original = crypto.randomUUID.bind(crypto);
    Object.defineProperty(crypto, 'randomUUID', { value: undefined, configurable: true });
    try {
      expect(gerarChave()).toMatch(UUID_V4);
      expect(gerarChave()).not.toBe(gerarChave());
    } finally {
      Object.defineProperty(crypto, 'randomUUID', { value: original, configurable: true });
    }
  });

  it('assinatura = documento|centavos|descrição', () => {
    expect(assinatura('52998224725', 4590, 'Café')).toBe('52998224725|4590|Café');
  });

  it('mesma assinatura mantém a chave; mudança gera outra', () => {
    const primeira = prepararConfirmacao({ chave: null, assinatura: null }, 'a|1|');
    expect(primeira.chave).toMatch(UUID_V4);

    expect(prepararConfirmacao(primeira, 'a|1|')).toEqual(primeira);

    const outra = prepararConfirmacao(primeira, 'a|2|');
    expect(outra.chave).not.toBe(primeira.chave);
    expect(outra.assinatura).toBe('a|2|');
  });
});

describe('armazenamento da transferência (research R4)', () => {
  it('grava e lê a transferência em andamento', () => {
    expect(lerTransferencia()).toEqual(TRANSFERENCIA_VAZIA);

    const dados = { ...TRANSFERENCIA_VAZIA, documento: '529.982.247-25', valorTexto: '45,90', descricao: 'Café' };
    salvarTransferencia(dados);
    expect(lerTransferencia()).toEqual(dados);

    limparTransferencia();
    expect(lerTransferencia()).toEqual(TRANSFERENCIA_VAZIA);
  });

  it('guarda até 20 comprovantes, mais recentes primeiro', () => {
    for (let i = 1; i <= 22; i++) guardarComprovante(comprovante(`t${i}`));

    expect(lerComprovante('t22')?.transferenciaId).toBe('t22');
    expect(lerComprovante('t3')).not.toBeNull();
    expect(lerComprovante('t2')).toBeNull();
    expect(lerComprovante('t1')).toBeNull();
  });

  it('regravar o mesmo comprovante não duplica', () => {
    guardarComprovante(comprovante('t1'));
    guardarComprovante(comprovante('t1'));

    expect(JSON.parse(sessionStorage.getItem(CHAVE_COMPROVANTES) ?? '[]')).toHaveLength(1);
  });

  it('sessionStorage indisponível não lança erro', () => {
    const espiao = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });
    const espiaoGravar = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });

    expect(() => salvarTransferencia(TRANSFERENCIA_VAZIA)).not.toThrow();
    expect(lerTransferencia()).toEqual(TRANSFERENCIA_VAZIA);
    expect(() => guardarComprovante(comprovante('t1'))).not.toThrow();
    expect(lerComprovante('t1')).toBeNull();

    espiao.mockRestore();
    espiaoGravar.mockRestore();
  });

  it('removerSessao() apaga a transferência e os comprovantes (FR-010)', () => {
    sessionStorage.setItem(CHAVE_SESSAO, '{}');
    salvarTransferencia({ ...TRANSFERENCIA_VAZIA, documento: 'x' });
    guardarComprovante(comprovante('t1'));

    removerSessao();

    expect(sessionStorage.getItem(CHAVE_TRANSFERENCIA)).toBeNull();
    expect(sessionStorage.getItem(CHAVE_COMPROVANTES)).toBeNull();
  });
});
