import { cpfValido, documentoCompleto, mascararCnpj, mascararCpf, mascararDocumentoDigitado, normalizarDocumento, ocultarDocumento, tipoDocumento } from './mascaras';

describe('mascararCpf', () => {
  it.each([
    ['52998224725', '529.982.247-25'],
    ['529.982.247-25', '529.982.247-25'],
    [' 529 982 247 25 ', '529.982.247-25'],
    ['529', '529'],
    ['5299', '529.9'],
    ['5299822', '529.982.2'],
    ['5299822472', '529.982.247-2'],
    ['529982247251234', '529.982.247-25'],
    ['abc529', '529'],
  ])('%s → %s', (entrada, esperado) => {
    expect(mascararCpf(entrada)).toBe(esperado);
  });
});

describe('mascararCnpj', () => {
  it.each([
    ['12abc34501de35', '12.ABC.345/01DE-35'],
    ['12.ABC.345/01DE-35', '12.ABC.345/01DE-35'],
    ['11222333000181', '11.222.333/0001-81'],
    ['12ABC34501DE3A', '12.ABC.345/01DE-3'],
    ['12ABC34501DEAB35', '12.ABC.345/01DE-35'],
    ['12_ABC 345', '12.ABC.345'],
    ['1', '1'],
    ['12A', '12.A'],
  ])('%s → %s', (entrada, esperado) => {
    expect(mascararCnpj(entrada)).toBe(esperado);
  });
});

describe('ocultarDocumento', () => {
  it('CPF mostra só o meio', () => {
    expect(ocultarDocumento('52998224725')).toBe('•••.982.247-••');
  });

  it('CNPJ alfanumérico mostra só o meio', () => {
    expect(ocultarDocumento('12ABC34501DE35')).toBe('••.•••.345/01DE-••');
  });
});

describe('documento do destinatário (specs/007, research R6)', () => {
  it.each([
    ['529.982.247-25', '52998224725'],
    ['12.abc.345/01de-35', '12ABC34501DE35'],
    [' 12 345 ', '12345'],
  ])('normalizarDocumento(%s) → %s', (entrada, esperado) => {
    expect(normalizarDocumento(entrada)).toBe(esperado);
  });

  it.each([
    ['529', 'cpf'],
    ['52998224725', 'cpf'],
    ['529982247251', 'cnpj'],
    ['12a', 'cnpj'],
    ['12.ABC', 'cnpj'],
  ])('tipoDocumento(%s) → %s', (entrada, tipo) => {
    expect(tipoDocumento(entrada)).toBe(tipo);
  });

  it.each([
    ['5299822', '529.982.2'],
    ['52998224725', '529.982.247-25'],
    ['529.982.247-251', '52.998.224/7251'],
    ['12abc34501de35', '12.ABC.345/01DE-35'],
  ])('mascararDocumentoDigitado(%s) → %s', (entrada, esperado) => {
    expect(mascararDocumentoDigitado(entrada)).toBe(esperado);
  });

  it('documentoCompleto: CPF com 11 dígitos e CNPJ com 14 caracteres', () => {
    expect(documentoCompleto('529.982.247-25')).toBe(true);
    expect(documentoCompleto('529.982.247-2')).toBe(false);
    expect(documentoCompleto('12.ABC.345/01DE-35')).toBe(true);
    expect(documentoCompleto('12.ABC.345/01DE-3')).toBe(false);
  });

  it.each([
    ['52998224725', true],
    ['52998224726', false],
    ['11111111111', false],
    ['5299822472', false],
  ])('cpfValido(%s) → %s', (cpf, valido) => {
    expect(cpfValido(cpf)).toBe(valido);
  });
});
