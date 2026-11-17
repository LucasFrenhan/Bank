import { mascararCnpj, mascararCpf, ocultarDocumento } from './mascaras';

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
