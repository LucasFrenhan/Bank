const formatador = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

/** `R$ 1.234,56` (pt-BR, BRL). A API envia o valor já com 2 casas; nada é calculado aqui. */
export function formatarMoeda(valor: number | string): string {
  return formatador.format(typeof valor === 'string' ? Number(valor) : valor);
}
