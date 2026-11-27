import { http, HttpResponse } from 'msw';
import type { TipoConta } from '../sessao/claims';
import type { ContaBancaria } from '../api/cliente';

/** Respostas simuladas da API (specs 003, 005 e 006) para testes de componentes (research R3). */

export const TRACE_ID = '4bf92f3577b34da6a3ce929d0e0e4736';

export function problema(codigo: string, status: number, extra: object = {}) {
  return HttpResponse.json(
    { type: `urn:bank:problema:${codigo}`, title: 'Título técnico', status, detail: 'Detalhe técnico', traceId: TRACE_ID, ...extra },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

function base64Url(valor: object): string {
  return btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(valor))))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
}

/** Prazos da sessão na resposta do login e da renovação (specs/016, contrato 1.1.0): 15 min e 8 h. */
export const PRAZOS_SESSAO = { sessaoExpiraPorInatividadeEmSegundos: 900, sessaoExpiraEmSegundos: 28800 };

/** Corpo do login e da renovação. */
export function sessaoEmitida(tipoConta: TipoConta = 'PessoaFisica', prazos: Partial<typeof PRAZOS_SESSAO> = {}) {
  return { tokenAcesso: tokenFalso(tipoConta), tipoToken: 'Bearer', expiraEmSegundos: 300, ...PRAZOS_SESSAO, ...prazos };
}

/** JWT falso (assinatura irrelevante para o frontend) com as claims do contrato. */
export function tokenFalso(tipoConta: TipoConta = 'PessoaFisica', email = 'lucas@exemplo.com.br'): string {
  return [
    base64Url({ alg: 'HS256', typ: 'JWT' }),
    base64Url({ sub: 'usuario-1', email, tipo_conta: tipoConta, carteira_id: 'carteira-1' }),
    'assinatura',
  ].join('.');
}

/** Dados bancários do exemplo do contrato 1.1.0 (specs/012 e specs/013). */
export function contaBancaria(alteracoes: Partial<ContaBancaria> = {}): ContaBancaria {
  return {
    banco: { codigo: '001', nome: 'One Bank' },
    agencia: '0001',
    numero: '00000042',
    digito: '0',
    tipo: 'ContaCorrente',
    ...alteracoes,
  };
}

export function carteira(tipoConta: TipoConta = 'PessoaFisica') {
  return {
    carteiraId: 'carteira-1',
    usuarioId: 'usuario-1',
    nomeTitular: tipoConta === 'PessoaFisica' ? 'Lucas Silva' : 'Empresa Exemplo Ltda',
    tipoConta,
    saldo: tipoConta === 'PessoaFisica' ? 10.0 : 0.0,
    moeda: 'BRL',
    atualizadoEm: '2026-11-17T17:32:00Z',
    contaBancaria: contaBancaria(),
  };
}

export function titular(tipoConta: TipoConta = 'PessoaFisica') {
  return {
    usuarioId: 'usuario-1',
    nomeCompleto: tipoConta === 'PessoaFisica' ? 'Lucas Silva' : 'Empresa Exemplo Ltda',
    email: tipoConta === 'PessoaFisica' ? 'lucas@exemplo.com.br' : 'contato@exemplo.com.br',
    documento: tipoConta === 'PessoaFisica' ? '52998224725' : '12ABC34501DE35',
    tipoConta,
    criadoEm: '2026-11-09T12:00:00Z',
  };
}

export const ID_TRANSFERENCIA = '019a8f55-2222-7890-abcd-ef1234567890';

/** Destinatário PJ de exemplo (CNPJ alfanumérico do contrato da spec 006). */
export const CNPJ_DESTINO = '12ABC34501DE35';

export function destinatario(tipoConta: TipoConta = 'PessoaJuridica') {
  return { nome: tipoConta === 'PessoaJuridica' ? 'Padaria Boa Massa Ltda' : 'Mariana Costa', tipoConta };
}

export function comprovante(extra: object = {}) {
  return {
    transferenciaId: ID_TRANSFERENCIA,
    chaveIdempotencia: '019a8f50-1111-4890-abcd-ef1234567890',
    pagador: { usuarioId: 'usuario-1', nome: 'Lucas Silva', documento: '•••.982.247-••' },
    recebedor: { usuarioId: 'usuario-2', nome: 'Padaria Boa Massa Ltda', documento: CNPJ_DESTINO, tipoConta: 'PessoaJuridica' },
    valor: 4.5,
    descricao: 'Café',
    status: 'Concluida',
    criadoEm: '2026-11-17T11:10:00Z',
    ...extra,
  };
}

export function itemExtrato(indice: number, extra: object = {}) {
  return {
    lancamentoId: `019a8f55-4444-7890-abcd-${String(indice).padStart(12, '0')}`,
    tipo: 'Transferencia',
    direcao: indice % 2 === 0 ? 'Entrada' : 'Saida',
    transferenciaId: `019a8f55-2222-7890-abcd-${String(indice).padStart(12, '0')}`,
    contraparte: { nome: `Contraparte ${indice}`, documento: '•••.982.247-••', tipoConta: 'PessoaFisica' },
    valor: 1 + indice,
    descricao: `Descrição ${indice}`,
    criadoEm: new Date(Date.UTC(2026, 10, 17, 12, 0, 0) - indice * 3_600_000).toISOString(),
    ...extra,
  };
}

export const ITEM_ABERTURA = {
  lancamentoId: '019a8f12-5555-7890-abcd-ef1234567890',
  tipo: 'Abertura',
  direcao: 'Entrada',
  transferenciaId: null,
  contraparte: null,
  valor: 10,
  descricao: 'Saldo inicial de demonstração',
  criadoEm: '2026-11-09T12:00:00Z',
};

/** Extrato paginado como a API: aplica direcao, pagina e tamanhoPagina sobre os itens dados. */
export function extratoDe(itens: object[]) {
  return http.get('*/api/v1/transferencias/extrato', ({ request }) => {
    const url = new URL(request.url);
    const pagina = Number(url.searchParams.get('pagina') ?? '1');
    const tamanho = Number(url.searchParams.get('tamanhoPagina') ?? '20');
    const direcao = url.searchParams.get('direcao');
    const filtrados = direcao ? itens.filter((i) => (i as { direcao: string }).direcao === direcao) : itens;
    return HttpResponse.json({
      itens: filtrados.slice((pagina - 1) * tamanho, pagina * tamanho),
      paginaAtual: pagina,
      tamanhoPagina: tamanho,
      totalItens: filtrados.length,
    });
  });
}

/** Limites da PF (spec 009, GET /limites). Rota literal antes de /:transferenciaId. */
export function limitesDe(extra: { porOperacao?: number | null; diario?: number | null; transferidoHoje?: number; disponivelHoje?: number | null } = {}) {
  return http.get('*/api/v1/transferencias/limites', () =>
    HttpResponse.json({ porOperacao: 1000, diario: 5000, transferidoHoje: 0, disponivelHoje: 5000, dia: '2026-11-18', ...extra }),
  );
}

/** Sobrescreve uma rota da spec 006 com um problema (ou falha de rede). */
export function falhaEm(metodo: 'get' | 'post', caminho: string, codigo: string, status = 400, extra: object = {}) {
  return http[metodo](`*${caminho}`, () => (codigo === 'rede' ? HttpResponse.error() : problema(codigo, status, extra)));
}

/* ---------- Cartão, cobranças, faturas e recebíveis (specs/014 e specs/015) ---------- */

export const VISA = { codigo: '001', nome: 'Visa' };
export const DADOS_CARTAO = { nomeImpresso: 'LUCAS SILVA', numero: '4000001234567899', validade: '11/31', cvv: '123', bandeira: VISA };
export const CODIGO = '7K3F-9QXM';
export const ID_COBRANCA = '019a8f77-1111-7890-abcd-ef1234567890';

export function cartaoCredito(extra: object = {}) {
  return {
    cartaoId: '019a8f77-5555-7890-abcd-ef1234567890',
    bandeira: VISA,
    nomeImpresso: 'LUCAS SILVA',
    ultimosDigitos: '7899',
    validade: '11/31',
    limiteTotal: 5000,
    limiteDisponivel: 4700.1,
    comprasALancar: 299.9,
    criadoEm: '2026-11-24T12:00:00Z',
    ...extra,
  };
}

/** Cobrança da PJ da sessão de teste (`usuario-1`) por padrão não é: o lojista é `loja-1`. */
export function cobranca(extra: object = {}) {
  return {
    cobrancaId: ID_COBRANCA,
    codigo: CODIGO,
    lojista: { usuarioId: 'loja-1', nome: 'Cafeteria Grão Especial LTDA', documento: '98765432000199' },
    valor: 299.9,
    descricao: 'Kit 3 Pacotes',
    status: 'Pendente',
    criadoEm: '2026-11-24T16:30:00Z',
    pagaEm: null,
    canceladaEm: null,
    pagamento: null,
    ultimaRecusa: null,
    ...extra,
  };
}

export const COBRANCA_PAGA = {
  status: 'Paga',
  pagaEm: '2026-11-24T16:35:00Z',
  pagamento: { bandeira: VISA, ultimosDigitosCartao: '7899', codigoAutorizacao: 'AUT84K' },
};

export function pagamento(extra: object = {}) {
  return {
    cobrancaId: ID_COBRANCA,
    status: 'Paga',
    valor: 299.9,
    bandeira: VISA,
    ultimosDigitosCartao: '7899',
    codigoAutorizacao: 'AUT84K',
    lojistaNome: 'Cafeteria Grão Especial LTDA',
    pagaEm: '2026-11-24T16:35:00Z',
    ...extra,
  };
}

export function fatura(extra: object = {}) {
  return {
    competencia: '2026-12',
    inicio: '2026-11-08',
    fechamento: '2026-12-07',
    vencimento: '2026-12-10',
    situacao: 'Aberta',
    total: 0,
    diasAtraso: 0,
    juros: 0,
    valorAPagar: null,
    pagaEm: null,
    valorPago: null,
    ...extra,
  };
}

export function faturaDetalhada(extra: object = {}) {
  return { ...fatura(), lancamentos: [], ...extra };
}

export function recebivel(extra: object = {}) {
  return {
    recebivelId: '019a8f77-9999-7890-abcd-ef1234567890',
    cobrancaId: ID_COBRANCA,
    descricao: 'Kit 3 Pacotes',
    situacao: 'Pendente',
    valorBruto: 299.9,
    taxa: 0,
    valorLiquido: 299.9,
    dataPrevista: '2099-01-07',
    recebidoEm: null,
    criadoEm: '2026-11-24T16:35:00Z',
    ...extra,
  };
}

/** Lista paginada como a API: aplica o filtro, a página e o tamanho. */
export function paginado(caminho: string, itens: object[], filtro?: { parametro: string; campo: string }) {
  return http.get(`*${caminho}`, ({ request }) => {
    const url = new URL(request.url);
    const pagina = Number(url.searchParams.get('pagina') ?? '1');
    const tamanho = Number(url.searchParams.get('tamanhoPagina') ?? '20');
    const valor = filtro ? url.searchParams.get(filtro.parametro) : null;
    const filtrados = valor && filtro ? itens.filter((i) => (i as Record<string, unknown>)[filtro.campo] === valor) : itens;
    return HttpResponse.json({
      itens: filtrados.slice((pagina - 1) * tamanho, pagina * tamanho),
      paginaAtual: pagina,
      tamanhoPagina: tamanho,
      totalItens: filtrados.length,
    });
  });
}

export const handlersPadrao = [
  http.post('*/api/v1/autenticacao/login', () =>
    HttpResponse.json({ tokenAcesso: tokenFalso(), tipoToken: 'Bearer', expiraEmSegundos: 300, ...PRAZOS_SESSAO }),
  ),
  // Sem sessão no servidor por padrão: a renovação na carga leva ao login (specs/016, FR-017).
  http.post('*/api/v1/autenticacao/renovar', () => problema('nao-autenticado', 401)),
  http.post('*/api/v1/autenticacao/sair', () => new HttpResponse(null, { status: 204 })),
  http.post('*/api/v1/autenticacao/registrar', () =>
    HttpResponse.json(
      {
        usuarioId: 'usuario-1',
        carteiraId: 'carteira-1',
        nomeCompleto: 'Lucas Silva',
        email: 'lucas@exemplo.com.br',
        documento: '52998224725',
        tipoConta: 'PessoaFisica',
        saldoInicial: 10.0,
        moeda: 'BRL',
      },
      { status: 201 },
    ),
  ),
  http.get('*/api/v1/carteiras/minha', () => HttpResponse.json(carteira())),
  http.get('*/api/v1/usuarios/me', () => HttpResponse.json(titular())),
  http.post('*/api/v1/transferencias/destinatarios/consultar', () => HttpResponse.json(destinatario())),
  http.post('*/api/v1/transferencias', () => HttpResponse.json(comprovante(), { status: 201 })),
  // Sem limites por padrão (a dica fica como na spec 007); os testes da spec 009 usam limitesDe.
  limitesDe({ porOperacao: null, diario: null, transferidoHoje: 0, disponivelHoje: null }),
  extratoDe([ITEM_ABERTURA]),
  // Sem cartão por padrão: o início e as telas da PF ficam como antes da specs/015.
  http.get('*/api/v1/cartoes/meu', () => problema('cartao-nao-encontrado', 404)),
  http.get('*/api/v1/faturas', () => problema('cartao-nao-encontrado', 404)),
  // Depois do extrato: a rota literal /extrato tem precedência, como na API (spec 008).
  consultaDe({ [ID_TRANSFERENCIA]: comprovante() }),
];

/** Consulta por identificador (spec 008): comprovantes conhecidos → 200; demais → 404. */
export function consultaDe(comprovantes: Record<string, object>) {
  return http.get('*/api/v1/transferencias/:transferenciaId', ({ params }) => {
    const id = String(params.transferenciaId);
    return comprovantes[id] ? HttpResponse.json(comprovantes[id]) : problema('transferencia-nao-encontrada', 404);
  });
}
