/**
 * Tradução de respostas de erro da API (ProblemDetails) em mensagens para a pessoa
 * (specs/004-frontend-base, data-model "Tradução de erros"; FR-018, FR-019).
 * Decide pelo `type`, nunca pelo `detail`.
 */

export const PREFIXO_TIPO = 'urn:bank:problema:';

export type AcaoErro = 'ir-para-login' | 'tentar-de-novo' | 'ir-para-inicio';

export interface MensagemErro {
  /** Código do problema (sufixo do `type`), `rede` ou `inesperado`. */
  codigo: string;
  mensagem: string;
  /** Erros por campo (apenas em `validacao`). */
  campos?: Record<string, string[]>;
  traceId?: string;
  acao?: AcaoErro;
  /** A sessão deve ser encerrada (401 em rota protegida, conta inexistente). */
  encerrarSessao?: boolean;
  status?: number;
}

export const MENSAGENS = {
  validacao: 'Confira os campos destacados.',
  emailJaCadastrado: 'Este e-mail já tem conta. Entre com ele ou use outro e-mail.',
  documentoJaCadastrado: 'Este documento já tem conta. Entre com ela ou confira o documento.',
  credenciaisInvalidas: 'E-mail ou senha incorretos',
  sessaoExpirada: 'Sua sessão expirou. Entre novamente.',
  acessoNegado: 'Você não tem acesso a esta área',
  contaNaoEncontrada: 'Não encontramos sua conta. Entre novamente.',
  rede: 'Não foi possível conectar. Verifique sua internet e tente de novo.',
  inesperado: 'Algo deu errado',
} as const;

interface ProblemDetailsBruto {
  type?: unknown;
  status?: unknown;
  errors?: unknown;
  traceId?: unknown;
}

/**
 * @param status status HTTP, ou `null` quando a requisição nem chegou à API (rede).
 * @param corpo corpo já interpretado como JSON (ou qualquer outro valor).
 */
export function traduzirErro(status: number | null, corpo: unknown): MensagemErro {
  if (status === null) {
    return { codigo: 'rede', mensagem: MENSAGENS.rede, acao: 'tentar-de-novo' };
  }

  const problema: ProblemDetailsBruto = corpo !== null && typeof corpo === 'object' ? corpo : {};
  const traceId = typeof problema.traceId === 'string' ? problema.traceId : undefined;
  const tipo = typeof problema.type === 'string' && problema.type.startsWith(PREFIXO_TIPO)
    ? problema.type.slice(PREFIXO_TIPO.length)
    : codigoPorStatus(status);

  const base = { codigo: tipo, status, ...(traceId ? { traceId } : {}) };

  switch (tipo) {
    case 'validacao':
      return { ...base, mensagem: MENSAGENS.validacao, campos: lerCampos(problema.errors) };
    case 'email-ja-cadastrado':
      return { ...base, mensagem: MENSAGENS.emailJaCadastrado, acao: 'ir-para-login' };
    case 'documento-ja-cadastrado':
      return { ...base, mensagem: MENSAGENS.documentoJaCadastrado, acao: 'ir-para-login' };
    case 'credenciais-invalidas':
      return { ...base, mensagem: MENSAGENS.credenciaisInvalidas };
    case 'nao-autenticado':
      return { ...base, mensagem: MENSAGENS.sessaoExpirada, encerrarSessao: true, acao: 'ir-para-login' };
    case 'acesso-negado':
      return { ...base, mensagem: MENSAGENS.acessoNegado, acao: 'ir-para-inicio' };
    case 'carteira-nao-encontrada':
    case 'usuario-nao-encontrado':
      return { ...base, mensagem: MENSAGENS.contaNaoEncontrada, encerrarSessao: true, acao: 'ir-para-login' };
    default:
      return { ...base, codigo: 'inesperado', mensagem: MENSAGENS.inesperado, acao: 'tentar-de-novo' };
  }
}

function codigoPorStatus(status: number): string {
  switch (status) {
    case 400:
      return 'validacao';
    case 401:
      return 'nao-autenticado';
    case 403:
      return 'acesso-negado';
    default:
      return 'inesperado';
  }
}

function lerCampos(errors: unknown): Record<string, string[]> {
  if (errors === null || typeof errors !== 'object') {
    return {};
  }

  const campos: Record<string, string[]> = {};
  for (const [campo, mensagens] of Object.entries(errors)) {
    if (Array.isArray(mensagens)) {
      campos[campo] = mensagens.filter((m): m is string => typeof m === 'string');
    }
  }

  return campos;
}
