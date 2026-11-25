/**
 * Datas de regra (`AAAA-MM-DD`) e meses de fatura (`AAAA-MM`), lidos pelas partes do texto: nunca viram `Date`, para não
 * mudar de dia com o fuso do navegador (specs/015-frontend-cartao-credito, FR-024; research R8).
 */

const MESES = [
  'janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho',
  'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro',
];

function partes(texto: string): { ano: string; mes: string; dia: number } {
  const [ano = '', mes = '', dia = ''] = texto.split('-');
  return { ano, mes: MESES[Number(mes) - 1] ?? mes, dia: Number(dia) };
}

/** `7 dez`. */
export function formatarDiaCurto(dia: string): string {
  const p = partes(dia);
  return `${p.dia} ${p.mes.slice(0, 3)}`;
}

/** `7 dez 2026`. */
export function formatarDia(dia: string): string {
  return `${formatarDiaCurto(dia)} ${partes(dia).ano}`;
}

/** `dezembro` de `2026-12`. */
export function nomeMes(competencia: string): string {
  return partes(competencia).mes;
}

/** `dezembro de 2026` de `2026-12`. */
export function formatarCompetencia(competencia: string): string {
  const p = partes(competencia);
  return `${p.mes} de ${p.ano}`;
}

/** `Dezembro de 2026`. */
export function tituloCompetencia(competencia: string): string {
  const texto = formatarCompetencia(competencia);
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

const formatadorBrasilia = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'America/Sao_Paulo',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

/** Dia de hoje em Brasília, `AAAA-MM-DD`. */
export function hojeBrasilia(agora = new Date()): string {
  const p = Object.fromEntries(formatadorBrasilia.formatToParts(agora).map(({ type, value }) => [type, value]));
  return `${p.year}-${p.month}-${p.day}`;
}
