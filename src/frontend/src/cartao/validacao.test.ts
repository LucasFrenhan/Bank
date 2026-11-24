import { mascararCvv, mascararNumero, mascararValidade, MENSAGENS_CARTAO, paraEnvio, separarErrosDaApi, validarCartao } from './validacao';

/** Validação do cartão digitado (specs/015, FR-015). */

const VALIDO = { nome: 'LUCAS SILVA', numero: '4000 0012 3456 7899', validade: '11/31', cvv: '123' };
const HOJE = '2026-11-24';

describe('validarCartao', () => {
  it('dados válidos', () => {
    expect(validarCartao(VALIDO, HOJE)).toEqual({});
  });

  it('todos os campos vazios: um erro por campo', () => {
    expect(validarCartao({ nome: '  ', numero: '', validade: '', cvv: '' }, HOJE)).toEqual({
      nome: [MENSAGENS_CARTAO.nome],
      numero: [MENSAGENS_CARTAO.numero],
      validade: [MENSAGENS_CARTAO.validade],
      cvv: [MENSAGENS_CARTAO.cvv],
    });
  });

  it.each(['4000001234567890', '400000123456789', '40000012345678999', '4000 0012 3456 789a'])('número %s inválido', (numero) => {
    expect(validarCartao({ ...VALIDO, numero }, HOJE).numero).toEqual([MENSAGENS_CARTAO.numero]);
  });

  it.each(['1131', '13/31', '00/31', '1/31', '11/2031'])('validade %s fora do formato', (validade) => {
    expect(validarCartao({ ...VALIDO, validade }, HOJE).validade).toEqual([MENSAGENS_CARTAO.validade]);
  });

  it('vale até o fim do mês de Brasília', () => {
    expect(validarCartao({ ...VALIDO, validade: '11/26' }, '2026-11-30').validade).toBeUndefined();
    expect(validarCartao({ ...VALIDO, validade: '11/26' }, '2026-12-01').validade).toEqual([MENSAGENS_CARTAO.vencido]);
    expect(validarCartao({ ...VALIDO, validade: '12/25' }, '2026-01-01').validade).toEqual([MENSAGENS_CARTAO.vencido]);
  });

  it.each(['12', '1234', 'abc'])('CVV %s inválido', (cvv) => {
    expect(validarCartao({ ...VALIDO, cvv }, HOJE).cvv).toEqual([MENSAGENS_CARTAO.cvv]);
  });
});

describe('máscaras e envio', () => {
  it('número, validade e CVV', () => {
    expect(mascararNumero('40000012345678991')).toBe('4000 0012 3456 7899');
    expect(mascararNumero('4000-0012')).toBe('4000 0012');
    expect(mascararNumero('')).toBe('');
    expect(mascararValidade('1131')).toBe('11/31');
    expect(mascararValidade('11')).toBe('11');
    expect(mascararValidade('11/3')).toBe('11/3');
    expect(mascararCvv('12a34')).toBe('123');
  });

  it('envia sem máscara e separa os erros da API', () => {
    expect(paraEnvio({ ...VALIDO, nome: ' LUCAS ' })).toEqual({ nome: 'LUCAS', numero: '4000001234567899', validade: '11/31', cvv: '123' });
    expect(separarErrosDaApi({ numeroCartao: ['a'], nomeTitularCartao: ['b'], 'Idempotency-Key': ['c'] })).toEqual({
      erros: { numero: ['a'], nome: ['b'] },
      restantes: ['c'],
    });
  });
});
