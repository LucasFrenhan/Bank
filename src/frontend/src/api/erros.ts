/**
 * Tradução de respostas de erro da API (ProblemDetails) em mensagens para a pessoa
 * (specs/004-frontend-base, data-model "Tradução de erros"; FR-018, FR-019).
 * Decide pelo `type`, nunca pelo `detail`.
 */

import { formatarCentavos, saldoEmCentavos } from '../formatacao/valor';
import { NOME_BANCO } from '../marca';

export const PREFIXO_TIPO = 'urn:bank:problema:';

export type AcaoErro = 'ir-para-login' | 'tentar-de-novo' | 'ir-para-inicio';

/** Estilo do protótipo (docs/design: estados de erro e feedback). */
export type VarianteErro = 'erro' | 'erro-campo' | 'atencao' | 'info' | 'neutro';

/**
 * Contexto da chamada: muda a mensagem de `acesso-negado` (specs/007, research R9) e, nas áreas da specs/015, também a
 * de `idempotencia-divergente`, `saldo-insuficiente` e falha de rede ao emitir cobrança.
 */
export type ContextoErro = 'transferencia' | 'cartao' | 'fatura' | 'pagamento' | 'cobranca' | 'emissao' | 'recebivel';

/** Link da próxima ação (`para`) ou ação na própria tela (specs/015, SC-006). */
export interface ProximaAcao {
  rotulo: string;
  para?: string;
}

/** Extensões do 422 `limite-excedido` (specs/009, contracts/transferencias-api.md v1.2.0). */
export interface LimiteExcedido {
  tipo: string;
  limiteCentavos: number;
  disponivelCentavos: number;
}

export interface MensagemErro {
  /** Código do problema (sufixo do `type`), `rede` ou `inesperado`. */
  codigo: string;
  /** Texto principal (título do aviso). */
  mensagem: string;
  /** Texto complementar abaixo do título. */
  detalhe?: string;
  variante?: VarianteErro;
  /** Erros por campo (apenas em `validacao`). */
  campos?: Record<string, string[]>;
  traceId?: string;
  acao?: AcaoErro;
  /** Próxima ação própria da recusa (specs/015, contracts/frontend.md "Tradução de erros"). */
  proximaAcao?: ProximaAcao;
  /** Limite que recusou (`limite-excedido`, spec 009), em centavos. */
  limite?: LimiteExcedido;
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
  // Transferências (specs/007-frontend-transferencias, data-model "Tradução de erros").
  destinatarioNaoEncontrado: `Nenhuma conta ${NOME_BANCO} com esse documento.`,
  autotransferencia: 'Esse documento é o seu',
  autotransferenciaDetalhe: 'Não é possível transferir para a própria carteira.',
  saldoInsuficiente: 'Saldo insuficiente',
  saldoInsuficienteDetalhe: 'Ajuste o valor para continuar.',
  idempotenciaDivergente: 'Essa transferência mudou',
  idempotenciaDivergenteDetalhe: 'Os dados não batem com o envio anterior. Revise e envie de novo.',
  indisponivelPj: 'Indisponível para Pessoa Jurídica',
  indisponivelPjDetalhe: 'Contas PJ recebem transferências e geram cobranças, mas não enviam.',
  comprovanteNaoEncontrado: 'Comprovante não encontrado',
  comprovanteNaoEncontradoDetalhe: 'Confira o endereço ou procure a transferência no extrato.',
  transferenciaRepetida: 'Essa transferência já foi feita',
  transferenciaRepetidaDetalhe: 'Nada foi cobrado de novo.',
  // Limites (specs/009-limite-transferencia, contracts/frontend.md "Tradução de erros").
  limiteExcedido: 'Limite excedido',
  limiteExcedidoDetalhe: 'Ajuste o valor para continuar.',
  // Cartão, cobranças, faturas e recebíveis (specs/015, contracts/frontend.md).
  areaIndisponivel: 'Área indisponível para o seu tipo de conta',
  areaSoPf: 'O cartão é para contas Pessoa Física.',
  areaSoPj: 'Cobranças e recebíveis são para contas Pessoa Jurídica.',
  pagamentoSoPf: 'Só contas Pessoa Física pagam cobranças',
  pagamentoSoPfDetalhe: 'Entre com a sua conta Pessoa Física para pagar.',
  cobrancaAcabouDeSerPaga: 'Esta cobrança acabou de ser paga',
  cobrancaAcabouDeSerPagaDetalhe: 'Não é mais possível cancelar.',
  cobrancaJaCancelada: 'Esta cobrança já foi cancelada',
  situacaoAtualizada: 'Veja a situação atualizada abaixo.',
  previstoParaHoje: 'O valor integral já está previsto para hoje.',
} as const;

/** Recusas da specs/014, com título, orientação e próxima ação próprios (specs/015, FR-017 e SC-006). */
const RECUSAS: Record<string, { mensagem: string; detalhe: string; variante: VarianteErro; proximaAcao: ProximaAcao }> = {
  'cartao-nao-autorizado': {
    mensagem: 'Cartão não autorizado',
    detalhe: 'Confira os dados do cartão e digite o CVV de novo.',
    variante: 'atencao',
    proximaAcao: { rotulo: 'Ver os dados do meu cartão', para: '/cartao' },
  },
  'cartao-bloqueado-fatura-vencida': {
    mensagem: 'Cartão bloqueado por fatura vencida',
    detalhe: 'Pague a fatura para voltar a usar o cartão.',
    variante: 'atencao',
    proximaAcao: { rotulo: 'Ver faturas', para: '/cartao/faturas' },
  },
  'limite-cartao-insuficiente': {
    mensagem: 'Limite insuficiente',
    detalhe: 'O valor passa do seu limite disponível.',
    variante: 'atencao',
    proximaAcao: { rotulo: 'Ver limite', para: '/cartao' },
  },
  'cobranca-nao-encontrada': {
    mensagem: 'Cobrança não encontrada',
    detalhe: 'Confira o link com quem enviou.',
    variante: 'neutro',
    proximaAcao: { rotulo: 'Ir para o início', para: '/' },
  },
  'cobranca-nao-pendente': {
    mensagem: 'Esta cobrança não está mais disponível',
    detalhe: 'Ela já foi paga ou cancelada.',
    variante: 'neutro',
    proximaAcao: { rotulo: 'Ir para o cartão', para: '/cartao' },
  },
  'cartao-nao-encontrado': {
    mensagem: 'Você ainda não tem um cartão',
    detalhe: 'Peça o seu para pagar esta cobrança.',
    variante: 'info',
    proximaAcao: { rotulo: 'Solicitar cartão', para: '/cartao' },
  },
  'cartao-ja-existente': {
    mensagem: 'Você já tem um cartão',
    detalhe: 'Mostramos o seu cartão abaixo.',
    variante: 'info',
    proximaAcao: { rotulo: 'Ver cartão', para: '/cartao' },
  },
  'fatura-nao-encontrada': {
    mensagem: 'Fatura não encontrada',
    detalhe: 'Confira o mês na lista de faturas.',
    variante: 'neutro',
    proximaAcao: { rotulo: 'Ver faturas', para: '/cartao/faturas' },
  },
  'fatura-nao-disponivel': {
    mensagem: 'Esta fatura não está disponível para pagamento',
    detalhe: MENSAGENS.situacaoAtualizada,
    variante: 'neutro',
    proximaAcao: { rotulo: 'Ver faturas', para: '/cartao/faturas' },
  },
  'recebivel-nao-encontrado': {
    mensagem: 'Recebível não encontrado',
    detalhe: 'Veja a lista atualizada.',
    variante: 'neutro',
    proximaAcao: { rotulo: 'Ver recebíveis' },
  },
  'recebivel-nao-antecipavel': {
    mensagem: 'Este recebível não pode mais ser antecipado',
    detalhe: MENSAGENS.situacaoAtualizada,
    variante: 'neutro',
    proximaAcao: { rotulo: 'Ver recebíveis' },
  },
};

/** `acesso-negado` nas áreas de um só tipo de conta (specs/015, FR-002). */
const AREAS: Partial<Record<ContextoErro, { mensagem: string; detalhe: string }>> = {
  cartao: { mensagem: MENSAGENS.areaIndisponivel, detalhe: MENSAGENS.areaSoPf },
  fatura: { mensagem: MENSAGENS.areaIndisponivel, detalhe: MENSAGENS.areaSoPf },
  cobranca: { mensagem: MENSAGENS.areaIndisponivel, detalhe: MENSAGENS.areaSoPj },
  emissao: { mensagem: MENSAGENS.areaIndisponivel, detalhe: MENSAGENS.areaSoPj },
  recebivel: { mensagem: MENSAGENS.areaIndisponivel, detalhe: MENSAGENS.areaSoPj },
  pagamento: { mensagem: MENSAGENS.pagamentoSoPf, detalhe: MENSAGENS.pagamentoSoPfDetalhe },
};

interface ProblemDetailsBruto {
  type?: unknown;
  status?: unknown;
  errors?: unknown;
  traceId?: unknown;
  tipoLimite?: unknown;
  limite?: unknown;
  valorDisponivel?: unknown;
}

/**
 * @param status status HTTP, ou `null` quando a requisição nem chegou à API (rede).
 * @param corpo corpo já interpretado como JSON (ou qualquer outro valor).
 */
export function traduzirErro(status: number | null, corpo: unknown, contexto?: ContextoErro): MensagemErro {
  if (status === null && contexto === 'emissao') {
    // Emitir cobrança não tem chave de idempotência na API: reenviar poderia criar outra (specs/015, research R9).
    return {
      codigo: 'rede',
      mensagem: 'Não foi possível confirmar a emissão',
      detalhe: 'Confira na lista se a cobrança foi criada antes de enviar de novo.',
      variante: 'atencao',
      proximaAcao: { rotulo: 'Ver cobranças', para: '/cobrancas' },
    };
  }

  if (status === null) {
    return { codigo: 'rede', mensagem: MENSAGENS.rede, acao: 'tentar-de-novo' };
  }

  const problema: ProblemDetailsBruto = corpo !== null && typeof corpo === 'object' ? corpo : {};
  const traceId = typeof problema.traceId === 'string' ? problema.traceId : undefined;
  const tipo = typeof problema.type === 'string' && problema.type.startsWith(PREFIXO_TIPO)
    ? problema.type.slice(PREFIXO_TIPO.length)
    : codigoPorStatus(status);

  const base = { codigo: tipo, status, ...(traceId ? { traceId } : {}) };

  const recusa = RECUSAS[tipo];
  if (recusa) {
    return tipo === 'cartao-nao-encontrado' && contexto === 'fatura'
      ? { ...base, ...recusa, detalhe: 'As faturas aparecem depois que você pedir o seu.' }
      : { ...base, ...recusa };
  }

  const area = contexto ? AREAS[contexto] : undefined;
  if (tipo === 'acesso-negado' && area) {
    return { ...base, ...area, variante: 'neutro', proximaAcao: { rotulo: 'Ir para o início', para: '/' } };
  }

  if (tipo === 'idempotencia-divergente' && area) {
    return {
      ...base,
      mensagem: 'Confira antes de tentar de novo',
      detalhe: 'Os dados não batem com o envio anterior.',
      variante: 'atencao',
      proximaAcao: { rotulo: 'Conferir' },
    };
  }

  if (tipo === 'saldo-insuficiente' && contexto === 'fatura') {
    return {
      ...base,
      mensagem: MENSAGENS.saldoInsuficiente,
      detalhe: 'O saldo da carteira não cobre a fatura.',
      variante: 'atencao',
      proximaAcao: { rotulo: 'Voltar à fatura' },
    };
  }

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
      return contexto === 'transferencia'
        ? { ...base, mensagem: MENSAGENS.indisponivelPj, detalhe: MENSAGENS.indisponivelPjDetalhe, variante: 'neutro', acao: 'ir-para-inicio' }
        : { ...base, mensagem: MENSAGENS.acessoNegado, acao: 'ir-para-inicio' };
    case 'destinatario-nao-encontrado':
      return {
        ...base,
        mensagem: MENSAGENS.destinatarioNaoEncontrado,
        variante: 'erro-campo',
        campos: { documento: [MENSAGENS.destinatarioNaoEncontrado] },
      };
    case 'autotransferencia':
      return { ...base, mensagem: MENSAGENS.autotransferencia, detalhe: MENSAGENS.autotransferenciaDetalhe, variante: 'atencao' };
    case 'saldo-insuficiente':
      return { ...base, mensagem: MENSAGENS.saldoInsuficiente, detalhe: MENSAGENS.saldoInsuficienteDetalhe, variante: 'atencao' };
    case 'limite-excedido':
      return traduzirLimite(base, problema);
    case 'idempotencia-divergente':
      return {
        ...base,
        mensagem: MENSAGENS.idempotenciaDivergente,
        detalhe: MENSAGENS.idempotenciaDivergenteDetalhe,
        variante: 'atencao',
      };
    case 'transferencia-nao-encontrada':
      return {
        ...base,
        mensagem: MENSAGENS.comprovanteNaoEncontrado,
        detalhe: MENSAGENS.comprovanteNaoEncontradoDetalhe,
        variante: 'neutro',
      };
    case 'carteira-nao-encontrada':
    case 'usuario-nao-encontrado':
      return { ...base, mensagem: MENSAGENS.contaNaoEncontrada, encerrarSessao: true, acao: 'ir-para-login' };
    default:
      return { ...base, codigo: 'inesperado', mensagem: MENSAGENS.inesperado, acao: 'tentar-de-novo' };
  }
}

/** Texto por tipo de limite; extensões ausentes ou tipo desconhecido → texto genérico. */
function traduzirLimite(base: Pick<MensagemErro, 'codigo' | 'status' | 'traceId'>, problema: ProblemDetailsBruto): MensagemErro {
  const limite = lerLimite(problema);
  const mensagem = { ...base, mensagem: MENSAGENS.limiteExcedido, variante: 'atencao' as const };
  if (!limite) {
    return { ...mensagem, detalhe: MENSAGENS.limiteExcedidoDetalhe };
  }

  return { ...mensagem, detalhe: detalheLimite(limite), limite };
}

function detalheLimite(limite: LimiteExcedido): string {
  if (limite.tipo === 'PorOperacao') {
    return `O limite por transferência é ${formatarCentavos(limite.limiteCentavos)}. ${MENSAGENS.limiteExcedidoDetalhe}`;
  }

  if (limite.tipo === 'Diario') {
    return limite.disponivelCentavos > 0
      ? `Você ainda pode transferir ${formatarCentavos(limite.disponivelCentavos)} hoje. ${MENSAGENS.limiteExcedidoDetalhe}`
      : `Você já usou todo o limite diário de ${formatarCentavos(limite.limiteCentavos)}. Ele renova à meia-noite (horário de Brasília).`;
  }

  return MENSAGENS.limiteExcedidoDetalhe;
}

function lerLimite(problema: ProblemDetailsBruto): LimiteExcedido | undefined {
  const { tipoLimite, limite, valorDisponivel } = problema;
  if (tipoLimite !== 'PorOperacao' && tipoLimite !== 'Diario') {
    return undefined;
  }

  const valido = (v: unknown): v is number | string => typeof v === 'number' || (typeof v === 'string' && /^-?\d+(\.\d+)?$/.test(v.trim()));
  if (!valido(limite) || !valido(valorDisponivel)) {
    return undefined;
  }

  return { tipo: tipoLimite, limiteCentavos: saldoEmCentavos(limite), disponivelCentavos: saldoEmCentavos(valorDisponivel) };
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
