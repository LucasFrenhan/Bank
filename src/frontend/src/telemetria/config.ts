/**
 * Configuração da telemetria do navegador (FR-019a–d; research R7). Funções puras, sem o SDK
 * do OpenTelemetry, para poderem ficar no bundle inicial.
 */

export const NOME_SERVICO = 'bank-frontend';

export interface AmbienteTelemetria {
  VITE_TELEMETRIA_HABILITADA?: string;
  VITE_OTLP_URL?: string;
}

/** Desligada por padrão (produção); ligada só com `VITE_TELEMETRIA_HABILITADA=true`. */
export function telemetriaHabilitada(ambiente: AmbienteTelemetria): boolean {
  return ambiente.VITE_TELEMETRIA_HABILITADA === 'true';
}

export function urlBaseOtlp(ambiente: AmbienteTelemetria): string {
  return (ambiente.VITE_OTLP_URL ?? '/otlp').replace(/\/$/, '');
}

/**
 * Atributos permitidos para uma falha de chamada à API: só código do problema, status e
 * traceId — nunca corpo, cabeçalhos, e-mail, documento, senha ou token (FR-019c).
 */
export function atributosFalhaApi(falha: { codigo: string; status?: number; traceId?: string }): Record<string, string | number> {
  return {
    'bank.problema': falha.codigo,
    ...(falha.status !== undefined ? { 'http.response.status_code': falha.status } : {}),
    ...(falha.traceId ? { 'bank.trace_id_api': falha.traceId } : {}),
  };
}

/** Caminho da URL sem query string nem fragmento (rotas não carregam dados pessoais). */
export function apenasCaminho(url: string): string {
  try {
    return new URL(url, 'http://localhost').pathname;
  } catch {
    return '';
  }
}
