import { formatarMoeda } from '../formatacao/moeda';
import { NOME_BANCO } from '../marca';

/**
 * Código curto e link da cobrança (specs/015-frontend-cartao-credito, FR-003, FR-009 e FR-012; data-model "Código e
 * link"). O alfabeto é o da API (contrato 1.1.0): sem 0, 1, I, L, O e U.
 */

const CODIGO = /^[2-9A-HJKMNP-TV-Z]{8}$/;

/** Código sem hífen, a partir do código digitado ou do link colado; nulo se inválido. */
export function extrairCodigo(texto: string): string | null {
  const noLink = /\/c\/([^/?#]+)\/?(?:[?#].*)?$/i.exec(texto.trim());
  const candidato = noLink ? decodificar(noLink[1] ?? '') : texto;
  const codigo = candidato.replace(/[\s-]/g, '').toUpperCase();
  return CODIGO.test(codigo) ? codigo : null;
}

/** `7K3F-9QXM`. */
export function formatarCodigo(codigo: string): string {
  const limpo = codigo.replace(/[\s-]/g, '').toUpperCase();
  return `${limpo.slice(0, 4)}-${limpo.slice(4)}`;
}

/** Endereço dentro do site: `/c/7K3F-9QXM`. */
export function caminhoDaCobranca(codigo: string): string {
  return `/c/${formatarCodigo(codigo)}`;
}

export function linkDaCobranca(codigo: string): string {
  return `${window.location.origin}${caminhoDaCobranca(codigo)}`;
}

export function textoDeCompartilhamento(cobranca: {
  codigo: string;
  lojista: { nome: string };
  valor: number | string;
  descricao: string;
}): string {
  const { codigo, lojista, valor, descricao } = cobranca;
  return `${lojista.nome}: ${formatarMoeda(valor)} — ${descricao}. Pague com o cartão ${NOME_BANCO}: ${linkDaCobranca(codigo)}`;
}

function decodificar(texto: string): string {
  try {
    return decodeURIComponent(texto);
  } catch {
    return texto;
  }
}
