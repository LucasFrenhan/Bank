import { centavosParaNumero, ERROS_VALOR, formatarCentavos, interpretarValor, saldoEmCentavos, taxaAntecipacao } from './valor';

/** specs/007-frontend-transferencias, data-model "Interpretação de valor" (research R3). */
describe('interpretarValor', () => {
  it.each([
    ['45,90', 4590],
    ['45.90', 4590],
    ['R$ 45,90', 4590],
    ['1.234,56', 123456],
    ['1234,56', 123456],
    ['1.234', 123400],
    ['10,5', 1050],
    ['0,01', 1],
    [' 7 ', 700],
  ])('%s → %i centavos', (texto, centavos) => {
    expect(interpretarValor(texto)).toEqual({ ok: true, centavos });
  });

  it.each([
    ['0', ERROS_VALOR.minimo],
    ['0,00', ERROS_VALOR.minimo],
    ['10,005', ERROS_VALOR.casas],
    ['', ERROS_VALOR.vazio],
    ['R$ ', ERROS_VALOR.vazio],
    ['12a', ERROS_VALOR.formato],
    ['1,2,3', ERROS_VALOR.formato],
  ])('%s → erro "%s"', (texto, erro) => {
    expect(interpretarValor(texto)).toEqual({ ok: false, erro });
  });

  it('textos de erro do data-model', () => {
    expect(ERROS_VALOR.vazio).toBe('Informe o valor');
    expect(ERROS_VALOR.minimo).toBe('Informe um valor a partir de R$ 0,01');
    expect(ERROS_VALOR.casas).toBe('Use no máximo 2 casas decimais');
  });
});

describe('centavos', () => {
  it.each([
    [842.5, 84250],
    ['842.50', 84250],
    ['842.5', 84250],
    [0, 0],
    [0.1, 10],
    [1234.56, 123456],
  ])('saldo %s → %i centavos', (saldo, centavos) => {
    expect(saldoEmCentavos(saldo)).toBe(centavos);
  });

  it('subtração em centavos não sofre erro de ponto flutuante', () => {
    expect(formatarCentavos(saldoEmCentavos(842.5) - 4590)).toMatch(/^R\$\s796,60$/);
  });

  it('número enviado à API tem no máximo 2 casas', () => {
    expect(centavosParaNumero(4590)).toBe(45.9);
    expect(centavosParaNumero(1)).toBe(0.01);
    expect(JSON.stringify(centavosParaNumero(123456))).toBe('1234.56');
  });
});

describe('taxaAntecipacao (specs/015, research R8)', () => {
  it.each([
    [29990, 600, 29390],
    [1, 0, 1],
    [10000, 200, 9800],
    [25, 1, 24],
    [24, 0, 24],
  ])('%i centavos', (centavos, taxa, liquido) => {
    expect(taxaAntecipacao(centavos)).toEqual({ taxa, liquido });
  });
});
