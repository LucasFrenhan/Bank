import type { components } from '../api/schema';

/**
 * Transferência em andamento e comprovantes desta aba em `sessionStorage`, no escopo da sessão
 * (specs/007-frontend-transferencias, research R4; FR-010). `removerSessao()` apaga as duas
 * chaves. Nenhum acesso pode lançar erro: sem armazenamento, o fluxo segue só em memória.
 */

export const CHAVE_TRANSFERENCIA = 'bank.transferencia';
export const CHAVE_COMPROVANTES = 'bank.comprovantes';
const MAXIMO_COMPROVANTES = 20;

export type ComprovanteGuardado = components['schemas']['ComprovanteResposta'];

export interface DestinatarioConfirmado {
  /** Documento normalizado para o qual o titular foi confirmado. */
  documento: string;
  nome: string;
  tipoConta: string;
}

export interface TransferenciaEmAndamento {
  /** Documento como digitado (com máscara). */
  documento: string;
  destinatario: DestinatarioConfirmado | null;
  valorTexto: string;
  descricao: string;
  chave: string | null;
  assinatura: string | null;
  /** `transferenciaId` quando o envio já foi aceito (201 ou 200). */
  concluida: string | null;
}

export const TRANSFERENCIA_VAZIA: TransferenciaEmAndamento = {
  documento: '',
  destinatario: null,
  valorTexto: '',
  descricao: '',
  chave: null,
  assinatura: null,
  concluida: null,
};

function ler<T>(chave: string): T | null {
  try {
    const bruto = sessionStorage.getItem(chave);
    return bruto ? (JSON.parse(bruto) as T) : null;
  } catch {
    return null;
  }
}

function gravar(chave: string, valor: unknown): void {
  try {
    sessionStorage.setItem(chave, JSON.stringify(valor));
  } catch {
    // Armazenamento indisponível: segue só em memória.
  }
}

export function lerTransferencia(): TransferenciaEmAndamento {
  const dados = ler<Partial<TransferenciaEmAndamento>>(CHAVE_TRANSFERENCIA);
  return dados && typeof dados === 'object' ? { ...TRANSFERENCIA_VAZIA, ...dados } : { ...TRANSFERENCIA_VAZIA };
}

export function salvarTransferencia(transferencia: TransferenciaEmAndamento): void {
  gravar(CHAVE_TRANSFERENCIA, transferencia);
}

export function limparTransferencia(): void {
  try {
    sessionStorage.removeItem(CHAVE_TRANSFERENCIA);
  } catch {
    // ignorado
  }
}

function lerComprovantes(): ComprovanteGuardado[] {
  const lista = ler<unknown>(CHAVE_COMPROVANTES);
  return Array.isArray(lista) ? (lista as ComprovanteGuardado[]) : [];
}

export function guardarComprovante(comprovante: ComprovanteGuardado): void {
  const outros = lerComprovantes().filter((c) => c.transferenciaId !== comprovante.transferenciaId);
  gravar(CHAVE_COMPROVANTES, [comprovante, ...outros].slice(0, MAXIMO_COMPROVANTES));
}

export function lerComprovante(transferenciaId: string): ComprovanteGuardado | null {
  return lerComprovantes().find((c) => c.transferenciaId === transferenciaId) ?? null;
}

/** Apaga tudo o que esta spec guarda na aba (chamado por `removerSessao`). */
export function limparDadosDeTransferencia(): void {
  try {
    sessionStorage.removeItem(CHAVE_TRANSFERENCIA);
    sessionStorage.removeItem(CHAVE_COMPROVANTES);
  } catch {
    // ignorado
  }
}
