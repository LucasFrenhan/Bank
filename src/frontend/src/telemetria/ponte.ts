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
  implementacao?.registrarNavegacao(rota);
}

export function registrarFalhaApi(falha: { codigo: string; status?: number; traceId?: string }): void {
  implementacao?.registrarFalhaApi(falha);
}
