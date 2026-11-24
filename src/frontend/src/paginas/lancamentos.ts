import type { ItemExtrato } from '../api/cliente';
import { saldoEmCentavos, formatarCentavos } from '../formatacao/valor';

/**
 * Visão de um lançamento do extrato (specs/007-frontend-transferencias, data-model "Item de
 * extrato"), compartilhada pelo extrato e pelo início.
 */

export const NOME_ABERTURA = 'Saldo inicial';
export const NOME_GENERICO = 'Lançamento';
export const SEM_DESCRICAO = 'Sem descrição';

export function ehEntrada(item: ItemExtrato): boolean {
  return item.direcao === 'Entrada';
}

/** Lançamentos sem contraparte (extrato 1.3.0; specs/014-cartao-credito-cobrancas, FR-038). */
const NOMES_SEM_CONTRAPARTE = new Map([
  ['Abertura', NOME_ABERTURA],
  ['PagamentoFatura', 'Pagamento de fatura'],
  ['LiquidacaoRecebivel', 'Recebimento de cobrança'],
  ['AntecipacaoRecebivel', 'Antecipação de recebível'],
]);

export function nomeContraparte(item: ItemExtrato): string {
  return NOMES_SEM_CONTRAPARTE.get(item.tipo) ?? item.contraparte?.nome ?? NOME_GENERICO;
}

/** Só transferências têm direção no texto de apoio; os demais tipos mostram a descrição. */
export function ehTransferencia(item: ItemExtrato): boolean {
  return item.tipo === 'Transferencia';
}

export function descricaoItem(item: ItemExtrato): string {
  const descricao = item.descricao?.trim();
  return descricao ? descricao : SEM_DESCRICAO;
}

export function rotuloDirecao(item: ItemExtrato): string {
  return ehEntrada(item) ? 'Entrada' : 'Saída';
}

/** `+ R$ 120,00` ou `− R$ 45,90`: o sinal acompanha a cor (FR-015). */
export function valorComSinal(item: ItemExtrato): string {
  return `${ehEntrada(item) ? '+' : '−'} ${formatarCentavos(saldoEmCentavos(item.valor))}`;
}

/**
 * Endereço do comprovante de todo lançamento de transferência; o comprovante é carregado pela
 * consulta por identificador (spec 007 FR-014; spec 008). O saldo inicial não tem comprovante.
 */
export function linkComprovante(item: ItemExtrato): string | undefined {
  return item.tipo === 'Transferencia' && item.transferenciaId ? `/transferencias/${item.transferenciaId}` : undefined;
}
