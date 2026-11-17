import type { TipoConta } from '../sessao/claims';

/**
 * Máscaras de documento (specs/004-frontend-base, data-model "Máscaras e formatação").
 * A API normaliza e valida o documento; aqui só se formata o que é digitado (FR-005, FR-006).
 */

const TAMANHO_CPF = 11;
const TAMANHO_CNPJ = 14;

/** CPF `000.000.000-00`, apenas dígitos, formatado progressivamente durante a digitação. */
export function mascararCpf(valor: string): string {
  const digitos = valor.replace(/\D/g, '').slice(0, TAMANHO_CPF);
  return aplicarPadrao(digitos, [3, 3, 3, 2], ['.', '.', '-']);
}

/**
 * CNPJ `XX.XXX.XXX/XXXX-00`: 12 primeiras posições `[0-9A-Z]` (minúsculas viram maiúsculas) e
 * 2 dígitos verificadores numéricos (IN RFB nº 2.229/2024).
 */
export function mascararCnpj(valor: string): string {
  const caracteres: string[] = [];
  for (const c of valor.toUpperCase()) {
    if (caracteres.length === TAMANHO_CNPJ) {
      break;
    }

    const permitido = caracteres.length < 12 ? /[0-9A-Z]/ : /[0-9]/;
    if (permitido.test(c)) {
      caracteres.push(c);
    }
  }

  return aplicarPadrao(caracteres.join(''), [2, 3, 3, 4, 2], ['.', '.', '/', '-']);
}

export function mascararDocumento(valor: string, tipoConta: TipoConta): string {
  return tipoConta === 'PessoaFisica' ? mascararCpf(valor) : mascararCnpj(valor);
}

/** Documento mascarado para exibição: CPF `•••.982.247-••`; CNPJ `••.•••.345/01DE-••`. */
export function ocultarDocumento(documento: string): string {
  const valor = documento.replace(/[^0-9A-Za-z]/g, '').toUpperCase();

  if (valor.length === TAMANHO_CPF) {
    return `•••.${valor.slice(3, 6)}.${valor.slice(6, 9)}-••`;
  }

  if (valor.length === TAMANHO_CNPJ) {
    return `••.•••.${valor.slice(5, 8)}/${valor.slice(8, 12)}-••`;
  }

  return '•'.repeat(valor.length);
}

function aplicarPadrao(valor: string, blocos: number[], separadores: string[]): string {
  let resultado = '';
  let posicao = 0;

  blocos.forEach((tamanho, indice) => {
    const parte = valor.slice(posicao, posicao + tamanho);
    if (parte.length === 0) {
      return;
    }

    if (indice > 0) {
      resultado += separadores[indice - 1] ?? '';
    }

    resultado += parte;
    posicao += tamanho;
  });

  return resultado;
}
