import type { ContaBancaria } from '../api/cliente';

/**
 * Apresentação dos dados bancários (specs/013, research R2). Os valores são sempre texto: nunca
 * convertidos em número, para preservar os zeros (FR-003).
 */

const NOMES_TIPO = new Map([
  ['ContaCorrente', 'Conta corrente'],
  ['ContaPoupanca', 'Conta poupança'],
]);

/** Tipo ainda desconhecido da interface → "Conta" (FR-005). */
export function nomeTipoContaBancaria(tipo: string): string {
  return NOMES_TIPO.get(tipo) ?? 'Conta';
}

export function nomeBanco({ banco }: ContaBancaria): string {
  return `${banco.codigo} – ${banco.nome}`;
}

export function contaComDigito({ numero, digito }: ContaBancaria): string {
  return `${numero}-${digito}`;
}

/** Dígitos separados, para o leitor de tela não ler o valor como um número (FR-004). */
export function lerDigitos(valor: string): string {
  return Array.from(valor).join(' ');
}

export function lerConta({ numero, digito }: ContaBancaria): string {
  return `${lerDigitos(numero)}, dígito ${digito}`;
}

/** Texto da ação "Copiar": só os dados bancários, em três linhas (FR-006). */
export function textoDadosDaConta(conta: ContaBancaria): string {
  return [
    `Banco ${nomeBanco(conta)}`,
    `Agência ${conta.agencia}`,
    `${nomeTipoContaBancaria(conta.tipo)} ${contaComDigito(conta)}`,
  ].join('\n');
}
