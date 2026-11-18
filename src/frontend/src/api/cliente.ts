import createClient, { type Middleware } from 'openapi-fetch';
import { lerSessao } from '../sessao/armazenamento';
import { registrarFalhaApi } from '../telemetria/ponte';
import { traduzirErro, type ContextoErro, type MensagemErro } from './erros';
import type { components, paths } from './schema';

/**
 * Único ponto do frontend que conversa com a API (plan.md, Constitution Check II). Tipado pelo
 * OpenAPI (`schema.d.ts`), mesma origem (research R4).
 */

export type Esquemas = components['schemas'];
export type Resultado<T> = { ok: true; dados: T; status: number } | { ok: false; erro: MensagemErro };

export type RegistrarRequisicao = Esquemas['RegistrarRequisicao'];
export type UsuarioRegistrado = Esquemas['UsuarioRegistradoResposta'];
export type TokenAcesso = Esquemas['LoginResposta'];
export type MinhaCarteira = Esquemas['MinhaCarteiraResposta'];
export type Titular = Esquemas['TitularResposta'];
// Transferências (specs/006 e specs/007-frontend-transferencias).
export type Destinatario = Esquemas['DestinatarioResposta'];
export type Comprovante = Esquemas['ComprovanteResposta'];
export type ExtratoPagina = Esquemas['ExtratoResposta'];
export type ItemExtrato = Esquemas['ItemExtratoResposta'];
export type DirecaoExtrato = 'Entrada' | 'Saida';

export interface NovaTransferencia {
  documentoDestino: string;
  valor: number;
  descricao?: string;
}

export interface ConsultaExtrato {
  pagina?: number;
  tamanhoPagina?: number;
  direcao?: DirecaoExtrato;
}

/** Código de uma consulta cancelada (documento alterado): não é erro para a pessoa. */
export const CODIGO_CANCELADA = 'cancelada';

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

async function chamar<T>(
  requisicao: () => Promise<{ data?: T; error?: unknown; response: Response }>,
  opcoes: { contexto?: ContextoErro; sinal?: AbortSignal } = {},
): Promise<Resultado<T>> {
  try {
    const { data, error, response } = await requisicao();
    if (response.ok && data !== undefined) {
      return { ok: true, dados: data, status: response.status };
    }

    return falhar(traduzirErro(response.status, error, opcoes.contexto));
  } catch {
    if (opcoes.sinal?.aborted) {
      return { ok: false, erro: { codigo: CODIGO_CANCELADA, mensagem: '' } };
    }

    return falhar(traduzirErro(null, null, opcoes.contexto));
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

  /** Nome e tipo de conta do destinatário (US1a da spec 006). O documento vai no corpo. */
  consultarDestinatario: (documento: string, sinal?: AbortSignal) =>
    chamar<Destinatario>(
      () =>
        cliente.POST('/api/v1/transferencias/destinatarios/consultar', {
          body: { documento },
          ...(sinal ? { signal: sinal } : {}),
        }),
      { contexto: 'transferencia', ...(sinal ? { sinal } : {}) },
    ),

  /**
   * Envia a transferência com a chave de idempotência do cliente. `repetida` indica um reenvio
   * já processado (200), em vez de uma transferência nova (201).
   */
  transferir: async (corpo: NovaTransferencia, chave: string) => {
    const resultado = await chamar<Comprovante>(
      () =>
        cliente.POST('/api/v1/transferencias', {
          params: { header: { 'Idempotency-Key': chave } },
          body: { documentoDestino: corpo.documentoDestino, valor: corpo.valor, descricao: corpo.descricao ?? null },
        }),
      { contexto: 'transferencia' },
    );

    return resultado.ok
      ? { ok: true as const, dados: { comprovante: resultado.dados, repetida: resultado.status === 200 } }
      : resultado;
  },

  obterExtrato: ({ pagina, tamanhoPagina, direcao }: ConsultaExtrato = {}) =>
    chamar<ExtratoPagina>(() =>
      cliente.GET('/api/v1/transferencias/extrato', {
        params: {
          query: {
            ...(pagina !== undefined ? { pagina: String(pagina) } : {}),
            ...(tamanhoPagina !== undefined ? { tamanhoPagina: String(tamanhoPagina) } : {}),
            ...(direcao ? { direcao } : {}),
          },
        },
      }),
    ),
};
