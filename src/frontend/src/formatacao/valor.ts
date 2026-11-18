import { formatarMoeda } from './moeda';

/**
 * Valores monetários em centavos inteiros (specs/007-frontend-transferencias, research R3;
 * constituição, Princípio IV): nenhuma aritmética de dinheiro é feita em ponto flutuante.
 */

export const ERROS_VALOR = {
  vazio: 'Informe o valor',
  minimo: 'Informe um valor a partir de R$ 0,01',
  casas: 'Use no máximo 2 casas decimais',
  formato: 'Digite só números, com vírgula para os centavos',
} as const;

export type ResultadoValor = { ok: true; centavos: number } | { ok: false; erro: string };

/** Até R$ 99.999.999.999,99 — acima disso o número deixaria de ser exato. */
const MAXIMO_DIGITOS_INTEIROS = 11;

/**
 * Interpreta um valor digitado no formato brasileiro: com vírgula, pontos são milhar; sem
 * vírgula, um único ponto seguido de 1–2 dígitos no fim é decimal. Não arredonda.
 */
export function interpretarValor(texto: string): ResultadoValor {
  const limpo = texto.replace(/R\$/gi, '').replace(/\s/g, '');

  if (!/\d/.test(limpo)) {
    return { ok: false, erro: ERROS_VALOR.vazio };
  }

  if (!/^[\d.,]+$/.test(limpo)) {
    return { ok: false, erro: ERROS_VALOR.formato };
  }

  let inteiro: string;
  let decimal: string;

  if (limpo.includes(',')) {
    const partes = limpo.split(',');
    if (partes.length !== 2) {
      return { ok: false, erro: ERROS_VALOR.formato };
    }

    inteiro = (partes[0] ?? '').replace(/\./g, '');
    decimal = partes[1] ?? '';
  } else {
    const decimalComPonto = /^(\d*)\.(\d{1,2})$/.exec(limpo);
    if (decimalComPonto) {
      inteiro = decimalComPonto[1] ?? '';
      decimal = decimalComPonto[2] ?? '';
    } else {
      inteiro = limpo.replace(/\./g, '');
      decimal = '';
    }
  }

  if (/\D/.test(decimal)) {
    return { ok: false, erro: ERROS_VALOR.formato };
  }

  if (decimal.length > 2) {
    return { ok: false, erro: ERROS_VALOR.casas };
  }

  const inteiroSemZeros = inteiro.replace(/^0+/, '');
  if (inteiroSemZeros.length > MAXIMO_DIGITOS_INTEIROS) {
    return { ok: false, erro: ERROS_VALOR.formato };
  }

  const centavos = Number(inteiroSemZeros || '0') * 100 + Number(decimal.padEnd(2, '0'));
  return centavos < 1 ? { ok: false, erro: ERROS_VALOR.minimo } : { ok: true, centavos };
}

/** Saldo da API (`number | string`, 2 casas) em centavos, lido pelo texto e não por multiplicação. */
export function saldoEmCentavos(saldo: number | string): number {
  const texto = typeof saldo === 'number' ? saldo.toFixed(2) : saldo.trim();
  const negativo = texto.startsWith('-');
  const [inteiro = '0', decimal = ''] = texto.replace(/^[-+]/, '').split('.');
  const centavos = Number(inteiro || '0') * 100 + Number(decimal.padEnd(2, '0').slice(0, 2));
  return negativo ? -centavos : centavos;
}

/** Número JSON enviado à API (no máximo 2 casas, lido exatamente pelo `decimal` do backend). */
export function centavosParaNumero(centavos: number): number {
  return centavos / 100;
}

export function formatarCentavos(centavos: number): string {
  return formatarMoeda(centavos / 100);
}
