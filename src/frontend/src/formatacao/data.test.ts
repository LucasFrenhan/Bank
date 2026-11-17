import { formatarData, formatarDataHora } from './data';

describe('formatação de datas (fuso do navegador: America/Sao_Paulo nos testes)', () => {
  it('data e hora no padrão do protótipo', () => {
    expect(formatarDataHora('2026-11-17T17:32:00Z')).toBe('17 nov 2026, 14:32');
  });

  it('somente data', () => {
    expect(formatarData('2026-11-09T12:00:00Z')).toBe('9 nov 2026');
  });
});
