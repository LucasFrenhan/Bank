import { apenasCaminho, atributosFalhaApi, NOME_SERVICO, telemetriaHabilitada, urlBaseOtlp } from './config';
import { definirTelemetria, registrarFalhaApi, registrarNavegacao } from './ponte';

describe('configuração da telemetria (FR-019a–d; research R7)', () => {
  it('desligada por padrão e ligada só com "true"', () => {
    expect(telemetriaHabilitada({})).toBe(false);
    expect(telemetriaHabilitada({ VITE_TELEMETRIA_HABILITADA: 'false' })).toBe(false);
    expect(telemetriaHabilitada({ VITE_TELEMETRIA_HABILITADA: 'true' })).toBe(true);
  });

  it('identifica o serviço como bank-frontend e usa /otlp por padrão', () => {
    expect(NOME_SERVICO).toBe('bank-frontend');
    expect(urlBaseOtlp({})).toBe('/otlp');
    expect(urlBaseOtlp({ VITE_OTLP_URL: 'http://coletor:4318/' })).toBe('http://coletor:4318');
  });

  it('falha de API exporta só código, status e traceId (sem dados sensíveis)', () => {
    const atributos = atributosFalhaApi({ codigo: 'validacao', status: 400, traceId: 'abc' });

    expect(atributos).toEqual({ 'bank.problema': 'validacao', 'http.response.status_code': 400, 'bank.trace_id_api': 'abc' });
  });

  it('URL reduzida ao caminho, sem query nem fragmento', () => {
    expect(apenasCaminho('http://localhost:3000/api/v1/carteiras/minha?email=a@b.com#x')).toBe('/api/v1/carteiras/minha');
  });

  it('sem telemetria carregada, a ponte não faz nada; com ela, repassa os eventos', () => {
    expect(() => registrarNavegacao('/')).not.toThrow();

    const eventos: string[] = [];
    definirTelemetria({
      registrarNavegacao: (rota) => eventos.push(`nav:${rota}`),
      registrarFalhaApi: (falha) => eventos.push(`falha:${falha.codigo}`),
    });
    registrarNavegacao('/perfil');
    registrarFalhaApi({ codigo: 'inesperado', status: 500 });
    definirTelemetria(null);

    expect(eventos).toEqual(['nav:/perfil', 'falha:inesperado']);
  });

  it('navegação registrada pelo padrão da rota, sem identificador nem query (specs/007, FR-029)', () => {
    const eventos: string[] = [];
    definirTelemetria({ registrarNavegacao: (rota) => eventos.push(rota), registrarFalhaApi: () => undefined });
    registrarNavegacao('/transferencias/019a8f55-2222-7890-abcd-ef1234567890');
    registrarNavegacao('/extrato?direcao=entradas&pagina=2');
    registrarNavegacao('/transferir/confirmar');
    definirTelemetria(null);

    expect(eventos).toEqual(['/transferencias/:id', '/extrato', '/transferir/confirmar']);
  });
});
