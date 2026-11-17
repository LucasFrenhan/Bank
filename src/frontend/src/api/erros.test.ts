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
