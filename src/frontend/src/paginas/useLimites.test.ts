import { maximoAgora, resumirLimites } from './useLimites';

/** Máximo que pode ser enviado agora (specs/009, contracts/frontend.md "Transferir — Dados"). */
describe('limites da tela Transferir', () => {
  const resumo = (porOperacao: number | null, diario: number | null, transferidoHoje: number, disponivelHoje: number | null) =>
    resumirLimites({ porOperacao, diario, transferidoHoje, disponivelHoje, dia: '2026-11-18' });

  it('converte a resposta em centavos (número ou texto)', () => {
    expect(resumirLimites({ porOperacao: '1000.00', diario: 5000, transferidoHoje: '4500.50', disponivelHoje: 499.5, dia: '2026-11-18' })).toEqual({
      porOperacaoCentavos: 100000,
      diarioCentavos: 500000,
      disponivelHojeCentavos: 49950,
    });
  });

  it('máximo = menor entre por operação e disponível hoje', () => {
    expect(maximoAgora(resumo(1000, 5000, 0, 5000))).toEqual({ centavos: 100000, tipo: 'PorOperacao' });
    expect(maximoAgora(resumo(1000, 5000, 4500, 500))).toEqual({ centavos: 50000, tipo: 'Diario' });
  });

  it('empate: por operação primeiro (mesma ordem da API)', () => {
    expect(maximoAgora(resumo(1000, 5000, 4000, 1000))).toEqual({ centavos: 100000, tipo: 'PorOperacao' });
  });

  it('ignora o limite nulo; sem nenhum, não há máximo', () => {
    expect(maximoAgora(resumo(null, 5000, 1000, 4000))).toEqual({ centavos: 400000, tipo: 'Diario' });
    expect(maximoAgora(resumo(1000, null, 0, null))).toEqual({ centavos: 100000, tipo: 'PorOperacao' });
    expect(maximoAgora(resumo(null, null, 0, null))).toBeNull();
    expect(maximoAgora(null)).toBeNull();
  });
});
