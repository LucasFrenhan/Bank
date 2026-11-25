import { api } from './cliente';
import { MENSAGENS, traduzirErro, type ContextoErro } from './erros';
import { definirTelemetria } from '../telemetria/ponte';
import { http, HttpResponse } from 'msw';
import { problema } from '../testes/handlers';
import { servidor } from '../testes/setup';

/** Tradução de erros do contrato do frontend v1.4.0 (specs/015, FR-017 e SC-006). */

const traduzir = (codigo: string, status: number, contexto?: ContextoErro) =>
  traduzirErro(status, { type: `urn:bank:problema:${codigo}`, status, detail: 'técnico', traceId: 'abc' }, contexto);

describe('recusas de cartão, cobrança, fatura e recebível', () => {
  it.each([
    ['cartao-nao-autorizado', 422, 'Cartão não autorizado', 'Confira os dados do cartão e digite o CVV de novo.', { rotulo: 'Ver os dados do meu cartão', para: '/cartao' }],
    ['cartao-bloqueado-fatura-vencida', 422, 'Cartão bloqueado por fatura vencida', 'Pague a fatura para voltar a usar o cartão.', { rotulo: 'Ver faturas', para: '/cartao/faturas' }],
    ['limite-cartao-insuficiente', 422, 'Limite insuficiente', 'O valor passa do seu limite disponível.', { rotulo: 'Ver limite', para: '/cartao' }],
    ['cobranca-nao-encontrada', 404, 'Cobrança não encontrada', 'Confira o link com quem enviou.', { rotulo: 'Ir para o início', para: '/' }],
    ['cobranca-nao-pendente', 409, 'Esta cobrança não está mais disponível', 'Ela já foi paga ou cancelada.', { rotulo: 'Ir para o cartão', para: '/cartao' }],
    ['cartao-nao-encontrado', 404, 'Você ainda não tem um cartão', 'Peça o seu para pagar esta cobrança.', { rotulo: 'Solicitar cartão', para: '/cartao' }],
    ['cartao-ja-existente', 409, 'Você já tem um cartão', 'Mostramos o seu cartão abaixo.', { rotulo: 'Ver cartão', para: '/cartao' }],
    ['fatura-nao-encontrada', 404, 'Fatura não encontrada', 'Confira o mês na lista de faturas.', { rotulo: 'Ver faturas', para: '/cartao/faturas' }],
    ['fatura-nao-disponivel', 409, 'Esta fatura não está disponível para pagamento', 'Veja a situação atualizada abaixo.', { rotulo: 'Ver faturas', para: '/cartao/faturas' }],
    ['recebivel-nao-encontrado', 404, 'Recebível não encontrado', 'Veja a lista atualizada.', { rotulo: 'Ver recebíveis' }],
    ['recebivel-nao-antecipavel', 409, 'Este recebível não pode mais ser antecipado', 'Veja a situação atualizada abaixo.', { rotulo: 'Ver recebíveis' }],
  ])('%s', (codigo, status, mensagem, detalhe, proximaAcao) => {
    expect(traduzir(codigo, status)).toMatchObject({ codigo, mensagem, detalhe, proximaAcao, traceId: 'abc' });
  });

  it.each(['pagamento', 'fatura', 'recebivel'] as const)('idempotencia-divergente em %s pede para conferir; na transferência não muda', (contexto) => {
    expect(traduzir('idempotencia-divergente', 409, contexto)).toMatchObject({
      mensagem: 'Confira antes de tentar de novo',
      detalhe: 'Os dados não batem com o envio anterior.',
      proximaAcao: { rotulo: 'Conferir' },
    });
    expect(traduzir('idempotencia-divergente', 409, 'transferencia').mensagem).toBe(MENSAGENS.idempotenciaDivergente);
  });

  it('saldo-insuficiente da fatura tem próxima ação; o da transferência não muda', () => {
    expect(traduzir('saldo-insuficiente', 422, 'fatura')).toMatchObject({ mensagem: 'Saldo insuficiente', proximaAcao: { rotulo: 'Voltar à fatura' } });
    expect(traduzir('saldo-insuficiente', 422, 'transferencia').detalhe).toBe(MENSAGENS.saldoInsuficienteDetalhe);
  });

  it.each([
    ['cartao', MENSAGENS.areaSoPf],
    ['fatura', MENSAGENS.areaSoPf],
    ['cobranca', MENSAGENS.areaSoPj],
    ['emissao', MENSAGENS.areaSoPj],
    ['recebivel', MENSAGENS.areaSoPj],
  ] as const)('acesso-negado em %s é área indisponível', (contexto, detalhe) => {
    expect(traduzir('acesso-negado', 403, contexto)).toMatchObject({
      mensagem: 'Área indisponível para o seu tipo de conta',
      detalhe,
      proximaAcao: { rotulo: 'Ir para o início', para: '/' },
    });
  });

  it('acesso-negado ao pagar diz que só PF paga; fora das áreas novas não muda', () => {
    expect(traduzir('acesso-negado', 403, 'pagamento').mensagem).toBe('Só contas Pessoa Física pagam cobranças');
    expect(traduzir('acesso-negado', 403).mensagem).toBe(MENSAGENS.acessoNegado);
  });

  it('falha de rede ao emitir orienta a conferir a lista, sem "Tentar de novo"', () => {
    const erro = traduzirErro(null, null, 'emissao');
    expect(erro).toMatchObject({ codigo: 'rede', mensagem: 'Não foi possível confirmar a emissão', proximaAcao: { rotulo: 'Ver cobranças', para: '/cobrancas' } });
    expect(erro.acao).toBeUndefined();
    expect(traduzirErro(null, null, 'pagamento').acao).toBe('tentar-de-novo');
  });
});

describe('falhas esperadas não viram log (research R10)', () => {
  it('sem cartão e atualização automática não registram; as demais, sim', async () => {
    const falhas: string[] = [];
    definirTelemetria({ registrarNavegacao: () => undefined, registrarFalhaApi: (f) => falhas.push(f.codigo) });
    servidor.use(http.get('*/api/v1/cobrancas/codigo/:codigo', () => HttpResponse.error()));

    await api.obterMeuCartao();
    await api.listarFaturas();
    await api.consultarCobranca('7K3F9QXM', { silenciosa: true });
    expect(falhas).toEqual([]);

    await api.consultarCobranca('7K3F9QXM');
    servidor.use(http.get('*/api/v1/cartoes/meu', () => problema('inesperado', 500)));
    await api.obterMeuCartao();
    definirTelemetria(null);

    expect(falhas).toEqual(['rede', 'inesperado']);
  });
});
