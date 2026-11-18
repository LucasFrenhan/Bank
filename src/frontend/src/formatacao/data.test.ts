import { chaveDia, formatarData, formatarDataCurta, formatarDataHora, formatarDataRelativa, rotuloDia } from './data';

describe('formatação de datas (fuso do navegador: America/Sao_Paulo nos testes)', () => {
  it('data e hora no padrão do protótipo', () => {
    expect(formatarDataHora('2026-11-17T17:32:00Z')).toBe('17 nov 2026, 14:32');
  });

  it('somente data', () => {
    expect(formatarData('2026-11-09T12:00:00Z')).toBe('9 nov 2026');
  });
});

describe('datas do extrato e do início (specs/007, data-model "Item de extrato")', () => {
  // 18/11/2026 10:00 em São Paulo.
  const agora = new Date('2026-11-18T13:00:00Z');

  it('chaveDia usa o fuso do navegador', () => {
    expect(chaveDia('2026-11-18T02:30:00Z')).toBe('2026-11-17');
    expect(chaveDia('2026-11-18T03:30:00Z')).toBe('2026-11-18');
  });

  it.each([
    ['2026-11-18T17:32:00Z', 'Hoje, 14:32'],
    ['2026-11-17T22:47:00Z', 'Ontem, 19:47'],
    ['2026-11-15T14:05:00Z', '15 nov, 11:05'],
    ['2025-12-31T14:05:00Z', '31 dez 2025, 11:05'],
  ])('formatarDataRelativa(%s) → %s', (iso, esperado) => {
    expect(formatarDataRelativa(iso, agora)).toBe(esperado);
  });

  it.each([
    ['2026-11-17T17:32:00Z', '17 nov, 14:32'],
    ['2025-11-17T17:32:00Z', '17 nov 2025, 14:32'],
  ])('formatarDataCurta(%s) → %s', (iso, esperado) => {
    expect(formatarDataCurta(iso, agora)).toBe(esperado);
  });

  it.each([
    ['2026-11-18T12:00:00Z', 'Hoje'],
    ['2026-11-17T12:00:00Z', 'Ontem'],
    ['2026-11-15T12:00:00Z', '15 de novembro'],
    ['2025-11-15T12:00:00Z', '15 de novembro de 2025'],
  ])('rotuloDia(%s) → %s', (iso, esperado) => {
    expect(rotuloDia(iso, agora)).toBe(esperado);
  });
});
