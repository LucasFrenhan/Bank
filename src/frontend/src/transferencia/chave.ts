/**
 * Chave de idempotência gerada no navegador (specs/007-frontend-transferencias, research R2;
 * constituição, Princípio IV). Uma chave vale para um conjunto de dados (assinatura) e é
 * reaproveitada em qualquer reenvio desses mesmos dados.
 */

export function gerarChave(): string {
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  // Fora de contexto seguro: UUID v4 a partir de bytes aleatórios (RFC 9562).
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x40;
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80;
  const hex = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

/** Identifica o conjunto de dados enviado: documento normalizado, valor e descrição aparada. */
export function assinatura(documento: string, valorCentavos: number, descricao: string): string {
  return `${documento}|${valorCentavos}|${descricao}`;
}

export interface EstadoChave {
  chave: string | null;
  assinatura: string | null;
}

/** Mantém a chave para a mesma assinatura; qualquer mudança (ou chave ausente) gera outra. */
export function prepararConfirmacao<T extends EstadoChave>(estado: T, assinaturaAtual: string): T {
  if (estado.chave && estado.assinatura === assinaturaAtual) {
    return estado;
  }

  return { ...estado, chave: gerarChave(), assinatura: assinaturaAtual };
}
