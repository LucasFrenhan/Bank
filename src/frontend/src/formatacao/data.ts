const partesDataHora = new Intl.DateTimeFormat('pt-BR', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

const partesData = new Intl.DateTimeFormat('pt-BR', { day: 'numeric', month: 'short', year: 'numeric' });

/** `17 nov 2026, 14:32` no fuso do navegador. */
export function formatarDataHora(iso: string): string {
  const p = partes(partesDataHora, iso);
  return `${p.day} ${p.month} ${p.year}, ${p.hour}:${p.minute}`;
}

/** `9 nov 2026` no fuso do navegador. */
export function formatarData(iso: string): string {
  const p = partes(partesData, iso);
  return `${p.day} ${p.month} ${p.year}`;
}

function partes(formatador: Intl.DateTimeFormat, iso: string): Record<string, string> {
  const resultado: Record<string, string> = {};
  for (const parte of formatador.formatToParts(new Date(iso))) {
    resultado[parte.type] = parte.type === 'month' ? parte.value.replace('.', '') : parte.value;
  }

  return resultado;
}

/* ---------- Extrato e início (specs/007-frontend-transferencias, data-model) ---------- */

const partesCompletas = new Intl.DateTimeFormat('pt-BR', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

const partesDiaNumerico = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });
const mesPorExtenso = new Intl.DateTimeFormat('pt-BR', { month: 'long' });

/** `AAAA-MM-DD` no fuso do navegador (agrupamento por dia). */
export function chaveDia(data: string | Date): string {
  const p = partes(partesDiaNumerico, typeof data === 'string' ? data : data.toISOString());
  return `${p.year}-${p.month}-${p.day}`;
}

function ontem(agora: Date): Date {
  const dia = new Date(agora);
  dia.setDate(dia.getDate() - 1);
  return dia;
}

function relacaoComHoje(iso: string, agora: Date): 'hoje' | 'ontem' | 'outro' {
  const dia = chaveDia(iso);
  if (dia === chaveDia(agora)) return 'hoje';
  if (dia === chaveDia(ontem(agora))) return 'ontem';
  return 'outro';
}

/** `17 nov, 14:32` (com o ano quando diferente do atual: `17 nov 2025, 14:32`). */
export function formatarDataCurta(iso: string, agora = new Date()): string {
  const p = partes(partesCompletas, iso);
  const ano = p.year === chaveDia(agora).slice(0, 4) ? '' : ` ${p.year}`;
  return `${p.day} ${p.month}${ano}, ${p.hour}:${p.minute}`;
}

/** `Hoje, 14:32` · `Ontem, 19:47` · `15 nov, 11:05`. */
export function formatarDataRelativa(iso: string, agora = new Date()): string {
  const relacao = relacaoComHoje(iso, agora);
  if (relacao === 'outro') {
    return formatarDataCurta(iso, agora);
  }

  const p = partes(partesCompletas, iso);
  return `${relacao === 'hoje' ? 'Hoje' : 'Ontem'}, ${p.hour}:${p.minute}`;
}

/** Cabeçalho do grupo do extrato mobile: `Hoje` · `Ontem` · `15 de novembro` (· `de 2025`). */
export function rotuloDia(iso: string, agora = new Date()): string {
  const relacao = relacaoComHoje(iso, agora);
  if (relacao !== 'outro') {
    return relacao === 'hoje' ? 'Hoje' : 'Ontem';
  }

  const [ano, , dia] = chaveDia(iso).split('-');
  const mes = mesPorExtenso.format(new Date(iso));
  const sufixoAno = ano === chaveDia(agora).slice(0, 4) ? '' : ` de ${ano}`;
  return `${Number(dia)} de ${mes}${sufixoAno}`;
}

/** `HH:MM` no fuso do navegador. */
export function formatarHora(iso: string): string {
  const p = partes(partesCompletas, iso);
  return `${p.hour}:${p.minute}`;
}
