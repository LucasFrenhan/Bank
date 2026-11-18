import { MENSAGENS, traduzirErro } from './erros';

const traceId = '4bf92f3577b34da6a3ce929d0e0e4736';
const problema = (codigo: string, status: number, extra: object = {}) => ({
  type: `urn:bank:problema:${codigo}`,
  title: 'Título técnico',
  status,
  detail: 'Detalhe técnico que não deve aparecer',
  traceId,
  ...extra,
});

describe('traduzirErro (data-model, tabela de tradução de erros)', () => {
  it('validacao devolve os erros por campo, inclusive desconhecidos', () => {
    const erro = traduzirErro(400, problema('validacao', 400, { errors: { documento: ['CPF inválido.'], outro: ['X'] } }));

    expect(erro.codigo).toBe('validacao');
    expect(erro.campos).toEqual({ documento: ['CPF inválido.'], outro: ['X'] });
    expect(erro.mensagem).toBe(MENSAGENS.validacao);
  });

  it.each([
    ['email-ja-cadastrado', 409, MENSAGENS.emailJaCadastrado, 'ir-para-login'],
    ['documento-ja-cadastrado', 409, MENSAGENS.documentoJaCadastrado, 'ir-para-login'],
    ['credenciais-invalidas', 401, MENSAGENS.credenciaisInvalidas, undefined],
    ['acesso-negado', 403, MENSAGENS.acessoNegado, 'ir-para-inicio'],
  ])('%s → mensagem do protótipo', (codigo, status, mensagem, acao) => {
    const erro = traduzirErro(status, problema(codigo, status));

    expect(erro.mensagem).toBe(mensagem);
    expect(erro.acao).toBe(acao);
    expect(erro.mensagem).not.toContain('técnico');
  });

  it.each(['nao-autenticado', 'carteira-nao-encontrada', 'usuario-nao-encontrado'])('%s encerra a sessão', (codigo) => {
    expect(traduzirErro(codigo === 'nao-autenticado' ? 401 : 404, problema(codigo, 401)).encerrarSessao).toBe(true);
  });

  it('401 sem type de negócio é tratado como sessão expirada', () => {
    expect(traduzirErro(401, null).mensagem).toBe(MENSAGENS.sessaoExpirada);
  });

  it('rede indisponível', () => {
    const erro = traduzirErro(null, null);

    expect(erro.codigo).toBe('rede');
    expect(erro.mensagem).toBe(MENSAGENS.rede);
    expect(erro.acao).toBe('tentar-de-novo');
  });

  it('500 mostra "Algo deu errado" com o traceId', () => {
    const erro = traduzirErro(500, { type: 'https://tools.ietf.org/html/rfc9110#section-15.6.1', status: 500, traceId });

    expect(erro.mensagem).toBe(MENSAGENS.inesperado);
    expect(erro.traceId).toBe(traceId);
  });

  it('corpo não-JSON ou type desconhecido vira erro inesperado', () => {
    expect(traduzirErro(502, '<html>').codigo).toBe('inesperado');
    expect(traduzirErro(409, problema('algo-novo', 409)).codigo).toBe('inesperado');
  });
});

describe('traduzirErro — transferências (specs/007, data-model "Tradução de erros")', () => {
  it('destinatario-nao-encontrado vira erro no campo do documento', () => {
    const erro = traduzirErro(404, problema('destinatario-nao-encontrado', 404));

    expect(erro.codigo).toBe('destinatario-nao-encontrado');
    expect(erro.variante).toBe('erro-campo');
    expect(erro.campos).toEqual({ documento: ['Nenhuma conta Bank com esse documento.'] });
  });

  it.each([
    ['autotransferencia', 422, 'Esse documento é o seu', 'Não é possível transferir para a própria carteira.', 'atencao'],
    ['saldo-insuficiente', 422, 'Saldo insuficiente', 'Ajuste o valor para continuar.', 'atencao'],
    ['idempotencia-divergente', 409, 'Essa transferência mudou', 'Os dados não batem com o envio anterior. Revise e envie de novo.', 'atencao'],
    ['transferencia-nao-encontrada', 404, 'Comprovante não encontrado', 'Confira o endereço ou procure a transferência no extrato.', 'neutro'],
  ])('%s (%i) → mensagem do protótipo', (codigo, status, mensagem, detalhe, variante) => {
    const erro = traduzirErro(status, problema(codigo, status));

    expect(erro.codigo).toBe(codigo);
    expect(erro.mensagem).toBe(mensagem);
    expect(erro.detalhe).toBe(detalhe);
    expect(erro.variante).toBe(variante);
    expect(`${erro.mensagem} ${erro.detalhe}`).not.toContain('técnico');
  });

  it('acesso-negado em transferência → "Indisponível para Pessoa Jurídica"', () => {
    const erro = traduzirErro(403, problema('acesso-negado', 403), 'transferencia');

    expect(erro.mensagem).toBe('Indisponível para Pessoa Jurídica');
    expect(erro.detalhe).toBe('Contas PJ recebem transferências e geram cobranças, mas não enviam.');
    expect(erro.variante).toBe('neutro');
  });

  it('acesso-negado fora de transferência mantém a mensagem da spec 004', () => {
    expect(traduzirErro(403, problema('acesso-negado', 403)).mensagem).toBe(MENSAGENS.acessoNegado);
  });
});
