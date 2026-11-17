import { metrics, trace } from '@opentelemetry/api';
import { SeverityNumber } from '@opentelemetry/api-logs';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { OTLPLogExporter } from '@opentelemetry/exporter-logs-otlp-http';
import { OTLPMetricExporter } from '@opentelemetry/exporter-metrics-otlp-http';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load';
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { BatchLogRecordProcessor, LoggerProvider } from '@opentelemetry/sdk-logs';
import { MeterProvider, PeriodicExportingMetricReader } from '@opentelemetry/sdk-metrics';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { ATTR_SERVICE_NAME } from '@opentelemetry/semantic-conventions';
import { apenasCaminho, atributosFalhaApi, NOME_SERVICO, urlBaseOtlp, type AmbienteTelemetria } from './config';
import { definirTelemetria } from './ponte';

/**
 * Telemetria do navegador (FR-019a–d; research R7): traces de carregamento, navegação e
 * chamadas à API (com `traceparent`, mesma origem), logs de erros e métrica de carregamento,
 * exportados por OTLP/HTTP. Falhas de envio são ignoradas pelo SDK.
 */
export function iniciarTelemetria(ambiente: AmbienteTelemetria): void {
  const base = urlBaseOtlp(ambiente);
  const resource = resourceFromAttributes({ [ATTR_SERVICE_NAME]: NOME_SERVICO });

  const tracerProvider = new WebTracerProvider({
    resource,
    spanProcessors: [new BatchSpanProcessor(new OTLPTraceExporter({ url: `${base}/v1/traces` }))],
  });
  tracerProvider.register({ contextManager: new ZoneContextManager() });

  registerInstrumentations({
    instrumentations: [
      new DocumentLoadInstrumentation(),
      new FetchInstrumentation({
        // O próprio envio de telemetria não gera spans.
        ignoreUrls: [new RegExp(`^${window.location.origin}${base}/`)],
        ignoreNetworkEvents: true,
        // Sem corpo nem cabeçalhos; URL reduzida ao caminho (FR-019c).
        applyCustomAttributesOnSpan: (span, requisicao) => {
          const url = typeof requisicao === 'object' && 'url' in requisicao ? String(requisicao.url) : '';
          span.setAttribute('url.path', apenasCaminho(url));
        },
      }),
    ],
  });

  const meterProvider = new MeterProvider({
    resource,
    readers: [
      new PeriodicExportingMetricReader({
        exporter: new OTLPMetricExporter({ url: `${base}/v1/metrics` }),
        exportIntervalMillis: 5000,
      }),
    ],
  });
  metrics.setGlobalMeterProvider(meterProvider);
  const meter = meterProvider.getMeter(NOME_SERVICO);
  const duracaoCarregamento = meter.createHistogram('bank.frontend.carregamento', {
    unit: 'ms',
    description: 'Tempo de carregamento da página (até o fim do evento load), por rota de entrada.',
  });
  const navegacoes = meter.createCounter('bank.frontend.navegacoes', {
    unit: '{navegacao}',
    description: 'Telas exibidas, por rota.',
  });

  const loggerProvider = new LoggerProvider({
    resource,
    processors: [new BatchLogRecordProcessor({ exporter: new OTLPLogExporter({ url: `${base}/v1/logs` }) })],
  });
  const logger = loggerProvider.getLogger(NOME_SERVICO);

  window.addEventListener('error', (evento) => {
    logger.emit({
      severityNumber: SeverityNumber.ERROR,
      severityText: 'ERROR',
      body: 'Erro não tratado no navegador',
      attributes: { 'exception.type': evento.error instanceof Error ? evento.error.name : 'Error', 'url.path': window.location.pathname },
    });
  });

  window.addEventListener('unhandledrejection', (evento) => {
    logger.emit({
      severityNumber: SeverityNumber.ERROR,
      severityText: 'ERROR',
      body: 'Promessa rejeitada sem tratamento no navegador',
      attributes: { 'exception.type': evento.reason instanceof Error ? evento.reason.name : typeof evento.reason, 'url.path': window.location.pathname },
    });
  });

  // A telemetria é carregada sob demanda e costuma iniciar depois do evento `load`: nesse caso
  // a medição é feita na hora; senão, logo após o `load` terminar (loadEventEnd preenchido).
  const registrarCarregamento = () => {
    const [entrada] = performance.getEntriesByType('navigation') as PerformanceNavigationTiming[];
    if (entrada && entrada.loadEventEnd > 0) {
      duracaoCarregamento.record(entrada.loadEventEnd, { 'url.path': window.location.pathname });
    }
  };
  if (document.readyState === 'complete') {
    registrarCarregamento();
  } else {
    window.addEventListener('load', () => window.setTimeout(registrarCarregamento, 0), { once: true });
  }

  // Envia o que estiver pendente quando a aba é escondida ou fechada (sessões curtas).
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') {
      void tracerProvider.forceFlush();
      void meterProvider.forceFlush();
      void loggerProvider.forceFlush();
    }
  });

  const tracer = trace.getTracer(NOME_SERVICO);

  definirTelemetria({
    registrarNavegacao(rota) {
      tracer.startSpan('navegacao', { attributes: { 'url.path': rota } }).end();
      navegacoes.add(1, { 'url.path': rota });
    },
    registrarFalhaApi(falha) {
      logger.emit({
        severityNumber: SeverityNumber.WARN,
        severityText: 'WARN',
        body: 'Falha em chamada à API',
        attributes: atributosFalhaApi(falha),
      });
    },
  });
}
