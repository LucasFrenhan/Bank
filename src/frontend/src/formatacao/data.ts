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
