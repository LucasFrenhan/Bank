import type { CartaoDigitado } from '../api/cliente';
import { hojeBrasilia } from '../formatacao/dia';

/** Campos do cartão digitado no pagamento (specs/015-frontend-cartao-credito, FR-015; contracts/frontend.md). */

export type CampoCartao = keyof CartaoDigitado;
export type ErrosCartao = Partial<Record<CampoCartao, string[]>>;

export const CARTAO_VAZIO: CartaoDigitado = { nome: '', numero: '', validade: '', cvv: '' };

export const MENSAGENS_CARTAO = {
  nome: 'Informe o nome impresso no cartão',
  numero: 'Confira o número do cartão',
  validade: 'Informe a validade no formato MM/AA',
  vencido: 'O cartão está vencido',
  cvv: 'O CVV tem 3 dígitos',
} as const;

const digitos = (texto: string, maximo: number) => texto.replace(/\D/g, '').slice(0, maximo);

/** `4000 0012 3456 7899`. */
export function mascararNumero(texto: string): string {
  return (digitos(texto, 16).match(/.{1,4}/g) ?? []).join(' ');
}

/** `11/31`. */
export function mascararValidade(texto: string): string {
  const d = digitos(texto, 4);
  return d.length > 2 ? `${d.slice(0, 2)}/${d.slice(2)}` : d;
}

export function mascararCvv(texto: string): string {
  return digitos(texto, 3);
}

function luhnValido(numero: string): boolean {
  let soma = 0;
  for (let i = 0; i < numero.length; i++) {
    let valor = Number(numero[numero.length - 1 - i]);
    if (i % 2 === 1) {
      valor *= 2;
      if (valor > 9) valor -= 9;
    }
    soma += valor;
  }

  return soma % 10 === 0;
}

function erroDaValidade(validade: string, hoje: string): string | null {
  const partes = /^(0[1-9]|1[0-2])\/(\d{2})$/.exec(validade);
  if (!partes) return MENSAGENS_CARTAO.validade;
  // O cartão vale até o fim do mês: compara só ano e mês, em Brasília.
  return `20${partes[2]}-${partes[1]}` < hoje.slice(0, 7) ? MENSAGENS_CARTAO.vencido : null;
}

/** Erros por campo; objeto vazio se pode enviar. */
export function validarCartao(cartao: CartaoDigitado, hoje = hojeBrasilia()): ErrosCartao {
  const erros: ErrosCartao = {};
  const numero = cartao.numero.replace(/\s/g, '');

  if (!cartao.nome.trim()) erros.nome = [MENSAGENS_CARTAO.nome];
  if (!/^\d{16}$/.test(numero) || !luhnValido(numero)) erros.numero = [MENSAGENS_CARTAO.numero];
  const validade = erroDaValidade(cartao.validade, hoje);
  if (validade) erros.validade = [validade];
  if (!/^\d{3}$/.test(cartao.cvv)) erros.cvv = [MENSAGENS_CARTAO.cvv];

  return erros;
}

/** O que vai para a API: sem máscara nem espaços nas pontas. */
export function paraEnvio(cartao: CartaoDigitado): CartaoDigitado {
  return { ...cartao, nome: cartao.nome.trim(), numero: cartao.numero.replace(/\s/g, '') };
}

const CAMPOS_API: Record<string, CampoCartao> = {
  nomeTitularCartao: 'nome',
  numeroCartao: 'numero',
  validade: 'validade',
  cvv: 'cvv',
};

/** Erros por campo da API nos campos da tela; os de outros campos vão para o aviso geral. */
export function separarErrosDaApi(campos: Record<string, string[]> | undefined): { erros: ErrosCartao; restantes: string[] } {
  const erros: ErrosCartao = {};
  const restantes: string[] = [];
  for (const [nome, mensagens] of Object.entries(campos ?? {})) {
    const campo = CAMPOS_API[nome];
    if (campo) erros[campo] = mensagens;
    else restantes.push(...mensagens);
  }

  return { erros, restantes };
}
