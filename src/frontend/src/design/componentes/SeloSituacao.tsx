import estilos from './SeloSituacao.module.css';

export type TomSituacao = 'atencao' | 'entrada' | 'info' | 'erro' | 'neutro';

/** Rótulo e cor por situação (specs/015, data-model "Situações e rótulos"). */
const SITUACOES: Record<string, { rotulo: string; tom: TomSituacao }> = {
  Pendente: { rotulo: 'Pendente', tom: 'atencao' },
  Paga: { rotulo: 'Paga', tom: 'entrada' },
  Cancelada: { rotulo: 'Cancelada', tom: 'neutro' },
  Aberta: { rotulo: 'Aberta', tom: 'info' },
  Fechada: { rotulo: 'Fechada', tom: 'neutro' },
  Disponivel: { rotulo: 'Disponível', tom: 'info' },
  Vencida: { rotulo: 'Vencida', tom: 'erro' },
  Liquidado: { rotulo: 'Liquidado', tom: 'entrada' },
  Antecipado: { rotulo: 'Antecipado', tom: 'info' },
};

/** Situação com texto e cor (nunca só a cor). Desconhecida: o próprio texto da API, sem cor. */
export function SeloSituacao({ situacao }: { situacao: string }) {
  const { rotulo, tom } = SITUACOES[situacao] ?? { rotulo: situacao, tom: 'neutro' as const };
  return <span className={[estilos.selo, estilos[tom]].join(' ')}>{rotulo}</span>;
}
