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
// Dados bancários da conta, todos como texto (specs/013-frontend-dados-bancarios).
export type ContaBancaria = Esquemas['ContaBancariaResposta'];
export type Titular = Esquemas['TitularResposta'];
// Transferências (specs/006 e specs/007-frontend-transferencias).
export type Destinatario = Esquemas['DestinatarioResposta'];
export type Comprovante = Esquemas['ComprovanteResposta'];
export type ExtratoPagina = Esquemas['ExtratoResposta'];
export type ItemExtrato = Esquemas['ItemExtratoResposta'];
export type Limites = Esquemas['LimitesResposta'];
export type DirecaoExtrato = 'Entrada' | 'Saida';
// Cartão, cobranças, faturas e recebíveis (specs/014 e specs/015-frontend-cartao-credito; contrato 1.1.0).
export type CartaoCredito = Esquemas['CartaoResposta'];
export type DadosCartao = Esquemas['DadosCartaoResposta'];
export type Cobranca = Esquemas['CobrancaResposta'];
export type PaginaCobrancas = Esquemas['PaginaCobrancasResposta'];
export type PagamentoCobranca = Esquemas['PagamentoResposta'];
export type Fatura = Esquemas['FaturaResposta'];
export type FaturaDetalhada = Esquemas['FaturaDetalhadaResposta'];
export type PaginaFaturas = Esquemas['PaginaFaturasResposta'];
export type Recebivel = Esquemas['RecebivelResposta'];
export type PaginaRecebiveis = Esquemas['PaginaRecebiveisResposta'];

/** Dados digitados no pagamento: nunca guardados (specs/015, FR-008). */
export interface CartaoDigitado {
  nome: string;
  numero: string;
  validade: string;
  cvv: string;
}

export interface ConsultaPagina {
  pagina?: number;
  tamanhoPagina?: number;
}

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

interface OpcoesChamada {
  contexto?: ContextoErro;
  sinal?: AbortSignal;
  /** Códigos que fazem parte do fluxo e não geram log de falha (specs/015, research R10). */
  esperados?: readonly string[] | 'todos';
}

async function chamar<T>(
  requisicao: () => Promise<{ data?: T; error?: unknown; response: Response }>,
  opcoes: OpcoesChamada = {},
): Promise<Resultado<T>> {
  try {
    const { data, error, response } = await requisicao();
    if (response.ok && data !== undefined) {
      return { ok: true, dados: data, status: response.status };
    }

    return falhar(traduzirErro(response.status, error, opcoes.contexto), opcoes.esperados);
  } catch {
    if (opcoes.sinal?.aborted) {
      return { ok: false, erro: { codigo: CODIGO_CANCELADA, mensagem: '' } };
    }

    return falhar(traduzirErro(null, null, opcoes.contexto), opcoes.esperados);
  }
}

function paginacao({ pagina, tamanhoPagina }: ConsultaPagina) {
  return {
    ...(pagina !== undefined ? { pagina: String(pagina) } : {}),
    ...(tamanhoPagina !== undefined ? { tamanhoPagina: String(tamanhoPagina) } : {}),
  };
}

function falhar<T>(erro: MensagemErro, esperados: OpcoesChamada['esperados'] = []): Resultado<T> {
  if (esperados === 'todos' || esperados.includes(erro.codigo)) {
    return { ok: false, erro };
  }

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

  /** Limites de transferência da PF e uso no dia de Brasília (spec 009, US4). */
  obterLimites: () => chamar<Limites>(() => cliente.GET('/api/v1/transferencias/limites'), { contexto: 'transferencia' }),

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

  /**
   * Comprovante pelo identificador, para pagador e recebedor (specs/008-consulta-transferencia).
   * Inexistente ou de outra pessoa → 404 `transferencia-nao-encontrada`.
   */
  obterTransferencia: (transferenciaId: string) =>
    chamar<Comprovante>(() =>
      cliente.GET('/api/v1/transferencias/{transferenciaId}', { params: { path: { transferenciaId } } }),
    ),

  /* ---------- Cartão (PF) ---------- */

  solicitarCartao: (bandeira: string) =>
    chamar<CartaoCredito>(() => cliente.POST('/api/v1/cartoes', { body: { bandeira } }), {
      contexto: 'cartao',
      esperados: ['cartao-ja-existente'],
    }),

  /** PF sem cartão: 404 `cartao-nao-encontrado`, que faz parte do fluxo. */
  obterMeuCartao: () =>
    chamar<CartaoCredito>(() => cliente.GET('/api/v1/cartoes/meu'), { contexto: 'cartao', esperados: ['cartao-nao-encontrado'] }),

  /** Número e CVV: só em memória, no momento do pedido (specs/015, FR-007 e FR-008). */
  obterDadosCartao: (sinal?: AbortSignal) =>
    chamar<DadosCartao>(() => cliente.GET('/api/v1/cartoes/meu/dados', { cache: 'no-store', ...(sinal ? { signal: sinal } : {}) }), {
      contexto: 'cartao',
      ...(sinal ? { sinal } : {}),
    }),

  /* ---------- Cobranças ---------- */

  emitirCobranca: (valor: number, descricao: string) =>
    chamar<Cobranca>(() => cliente.POST('/api/v1/cobrancas', { body: { valor, descricao } }), { contexto: 'emissao' }),

  listarCobrancas: ({ status, ...pagina }: ConsultaPagina & { status?: string }) =>
    chamar<PaginaCobrancas>(
      () => cliente.GET('/api/v1/cobrancas/minhas', { params: { query: { ...paginacao(pagina), ...(status ? { status } : {}) } } }),
      { contexto: 'cobranca' },
    ),

  /** `silenciosa`: atualização automática, cujas falhas não geram log. */
  consultarCobranca: (codigo: string, opcoes: { silenciosa?: boolean } = {}) =>
    chamar<Cobranca>(() => cliente.GET('/api/v1/cobrancas/codigo/{codigo}', { params: { path: { codigo } } }), {
      contexto: 'cobranca',
      ...(opcoes.silenciosa ? { esperados: 'todos' as const } : {}),
    }),

  cancelarCobranca: (cobrancaId: string) =>
    chamar<Cobranca>(() => cliente.POST('/api/v1/cobrancas/{cobrancaId}/cancelar', { params: { path: { cobrancaId } } }), {
      contexto: 'cobranca',
    }),

  pagarCobranca: (cobrancaId: string, cartao: CartaoDigitado, chave: string) =>
    chamar<PagamentoCobranca>(
      () =>
        cliente.POST('/api/v1/cobrancas/{cobrancaId}/pagar', {
          params: { path: { cobrancaId }, header: { 'Idempotency-Key': chave } },
          body: { nomeTitularCartao: cartao.nome, numeroCartao: cartao.numero, validade: cartao.validade, cvv: cartao.cvv },
        }),
      { contexto: 'pagamento' },
    ),

  /* ---------- Faturas (PF) ---------- */

  listarFaturas: (pagina: ConsultaPagina = {}) =>
    chamar<PaginaFaturas>(() => cliente.GET('/api/v1/faturas', { params: { query: paginacao(pagina) } }), {
      contexto: 'fatura',
      esperados: ['cartao-nao-encontrado'],
    }),

  obterFatura: (competencia: string) =>
    chamar<FaturaDetalhada>(() => cliente.GET('/api/v1/faturas/{competencia}', { params: { path: { competencia } } }), {
      contexto: 'fatura',
    }),

  pagarFatura: (competencia: string, chave: string) =>
    chamar<FaturaDetalhada>(
      () =>
        cliente.POST('/api/v1/faturas/{competencia}/pagar', {
          params: { path: { competencia }, header: { 'Idempotency-Key': chave } },
        }),
      { contexto: 'fatura' },
    ),

  /* ---------- Recebíveis (PJ) ---------- */

  listarRecebiveis: ({ situacao, ...pagina }: ConsultaPagina & { situacao?: string }) =>
    chamar<PaginaRecebiveis>(
      () => cliente.GET('/api/v1/recebiveis', { params: { query: { ...paginacao(pagina), ...(situacao ? { situacao } : {}) } } }),
      { contexto: 'recebivel' },
    ),

  anteciparRecebivel: (recebivelId: string, chave: string) =>
    chamar<Recebivel>(
      () =>
        cliente.POST('/api/v1/recebiveis/{recebivelId}/antecipar', {
          params: { path: { recebivelId }, header: { 'Idempotency-Key': chave } },
        }),
      { contexto: 'recebivel' },
    ),
};
