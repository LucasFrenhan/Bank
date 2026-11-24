/**
 * Ponte leve entre a aplicação e a telemetria. O SDK do OpenTelemetry só é carregado quando a
 * telemetria está ligada (research R7); até lá, estas funções não fazem nada.
 */

export interface ImplementacaoTelemetria {
  registrarNavegacao(rota: string): void;
  registrarFalhaApi(falha: { codigo: string; status?: number; traceId?: string }): void;
}

let implementacao: ImplementacaoTelemetria | null = null;

export function definirTelemetria(impl: ImplementacaoTelemetria | null): void {
  implementacao = impl;
}

export function registrarNavegacao(rota: string): void {
  implementacao?.registrarNavegacao(padraoDaRota(rota));
}

/**
 * Rota sem query string e com identificadores trocados pelo parâmetro (specs/007, contrato
 * "Telemetria"): `/transferencias/{uuid}` → `/transferencias/:id`; `/c/{codigo}` → `/c/:codigo`; mês da fatura →
 * `/cartao/faturas/:competencia` (specs/015, research R10).
 */
export function padraoDaRota(rota: string): string {
  const caminho = rota.split(/[?#]/)[0] ?? rota;
  return caminho
    .replace(/^\/transferencias\/[^/]+$/, '/transferencias/:id')
    .replace(/^\/c\/[^/]+\/?$/i, '/c/:codigo')
    .replace(/^\/cartao\/faturas\/[^/]+\/?$/i, '/cartao/faturas/:competencia');
}

export function registrarFalhaApi(falha: { codigo: string; status?: number; traceId?: string }): void {
  implementacao?.registrarFalhaApi(falha);
}
