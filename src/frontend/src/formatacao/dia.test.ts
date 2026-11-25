import { formatarCompetencia, formatarDia, formatarDiaCurto, hojeBrasilia, nomeMes, tituloCompetencia } from './dia';

/** Datas de regra sem fuso (specs/015, FR-024). */
describe('dia', () => {
  const fuso = process.env.TZ;
  afterEach(() => {
    if (fuso === undefined) delete process.env.TZ;
    else process.env.TZ = fuso;
  });

  it.each(['America/Sao_Paulo', 'UTC', 'Pacific/Kiritimati', 'Pacific/Pago_Pago'])('mesmo dia em %s', (tz) => {
    process.env.TZ = tz;
    expect(formatarDia('2026-12-07')).toBe('7 dez 2026');
    expect(formatarDia('2027-01-01')).toBe('1 jan 2027');
    expect(formatarDiaCurto('2026-03-31')).toBe('31 mar');
    expect(nomeMes('2026-12')).toBe('dezembro');
    expect(formatarCompetencia('2027-01')).toBe('janeiro de 2027');
    expect(tituloCompetencia('2026-12')).toBe('Dezembro de 2026');
  });

  it('hoje em Brasília vira à meia-noite de lá', () => {
    expect(hojeBrasilia(new Date('2026-12-11T02:59:59Z'))).toBe('2026-12-10');
    expect(hojeBrasilia(new Date('2026-12-11T03:00:00Z'))).toBe('2026-12-11');
  });
});
