import { formatarMoeda } from './moeda';

describe('formatarMoeda', () => {
  it.each([
    [10, 'R$ 10,00'],
    [1234.56, 'R$ 1.234,56'],
    [0, 'R$ 0,00'],
    ['842.5', 'R$ 842,50'],
  ])('%s → %s', (valor, esperado) => {
    expect(formatarMoeda(valor).replace(/\s/g, ' ')).toBe(esperado);
  });
});
