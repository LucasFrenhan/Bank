import createClient, { type Middleware } from 'openapi-fetch';
import { lerSessao } from '../sessao/armazenamento';
import { registrarFalhaApi } from '../telemetria/ponte';
import { traduzirErro, type MensagemErro } from './erros';
import type { components, paths } from './schema';

/**
 * Único ponto do frontend que conversa com a API (plan.md, Constitution Check II). Tipado pelo
 * OpenAPI (`schema.d.ts`), mesma origem (research R4).
 */

export type Esquemas = components['schemas'];
export type Resultado<T> = { ok: true; dados: T } | { ok: false; erro: MensagemErro };

export type RegistrarRequisicao = Esquemas['RegistrarRequisicao'];
export type UsuarioRegistrado = Esquemas['UsuarioRegistradoResposta'];
export type TokenAcesso = Esquemas['LoginResposta'];
export type MinhaCarteira = Esquemas['MinhaCarteiraResposta'];
export type Titular = Esquemas['TitularResposta'];

interface IntegracaoSessao {
  obterToken?: () => string | null;
  /** Chamado quando a API recusa o token (401 com `Authorization` enviado). */
  aoNaoAutenticado: () => void;
}

// O token vem direto do armazenamento: as telas podem chamar a API antes de os efeitos do
// provedor de sessão rodarem (efeitos dos filhos rodam antes dos do pai).
let sessao: IntegracaoSessao = { obterToken: () => lerSessao()?.token ?? null, aoNaoAutenticado: () => undefined };

export function integrarSessao(integracao: IntegracaoSessao): void {
  sessao = { ...sessao, ...integracao };
}

const middlewareSessao: Middleware = {
  onRequest({ request }) {
    const token = sessao.obterToken?.() ?? null;
    if (token) {
      request.headers.set('Authorization', `Bearer ${token}`);
    }

    return request;
  },
  onResponse({ request, response }) {
    if (response.status === 401 && request.headers.has('Authorization')) {
      sessao.aoNaoAutenticado();
    }

    return response;
  },
};

const cliente = createClient<paths>({
  baseUrl: typeof window === 'undefined' ? '' : window.location.origin,
  // Resolve o fetch a cada chamada: a instrumentação do OpenTelemetry (carregada depois) e os
  // testes substituem o fetch global; capturá-lo na criação ignoraria essas substituições.
  fetch: (requisicao) => globalThis.fetch(requisicao),
});
cliente.use(middlewareSessao);

async function chamar<T>(requisicao: () => Promise<{ data?: T; error?: unknown; response: Response }>): Promise<Resultado<T>> {
  try {
    const { data, error, response } = await requisicao();
    if (response.ok && data !== undefined) {
      return { ok: true, dados: data };
    }

    return falhar(traduzirErro(response.status, error));
  } catch {
    return falhar(traduzirErro(null, null));
  }
}

function falhar<T>(erro: MensagemErro): Resultado<T> {
  registrarFalhaApi({
    codigo: erro.codigo,
    ...(erro.status !== undefined ? { status: erro.status } : {}),
    ...(erro.traceId ? { traceId: erro.traceId } : {}),
  });
  return { ok: false, erro };
}

export const api = {
  registrar: (corpo: RegistrarRequisicao) =>
    chamar<UsuarioRegistrado>(() => cliente.POST('/api/v1/autenticacao/registrar', { body: corpo })),

  entrar: (email: string, senha: string) =>
    chamar<TokenAcesso>(() => cliente.POST('/api/v1/autenticacao/login', { body: { email, senha } })),

  obterMinhaCarteira: () => chamar<MinhaCarteira>(() => cliente.GET('/api/v1/carteiras/minha')),

  /** Dados do titular (specs/005-usuarios-me): documento completo e data de cadastro. */
  obterTitular: () => chamar<Titular>(() => cliente.GET('/api/v1/usuarios/me')),
};
