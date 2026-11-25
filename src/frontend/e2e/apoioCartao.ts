import { expect, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { criarContaPelaApi, tokenPelaApi, type Conta } from './apoio';

/** Cartão, cobranças e faturas pela API (specs/015-frontend-cartao-credito, research R13). */

export interface ContaComToken extends Conta {
  token: string;
}

export interface CartaoPf {
  pf: ContaComToken;
  dados: { nomeImpresso: string; numero: string; validade: string; cvv: string };
}

async function chamar<T>(page: Page, metodo: 'get' | 'post', caminho: string, token: string, corpo?: unknown, chave?: string): Promise<T> {
  const resposta = await page.request[metodo](`/api/v1${caminho}`, {
    headers: { Authorization: `Bearer ${token}`, ...(chave ? { 'Idempotency-Key': chave } : {}) },
    ...(corpo !== undefined ? { data: corpo } : {}),
  });
  expect(resposta.ok(), `${metodo} ${caminho}: ${resposta.status()} ${await resposta.text()}`).toBe(true);
  return (await resposta.json()) as T;
}

export async function criarPj(page: Page, nome = 'Cafeteria Grão Ltda'): Promise<ContaComToken> {
  const conta = await criarContaPelaApi(page, 'PessoaJuridica', nome);
  return { ...conta, token: await tokenPelaApi(page, conta) };
}

export async function criarPf(page: Page, nome = 'Lucas Cartão'): Promise<ContaComToken> {
  const conta = await criarContaPelaApi(page, 'PessoaFisica', nome);
  return { ...conta, token: await tokenPelaApi(page, conta) };
}

export async function criarPfComCartao(page: Page, nome?: string, bandeira = '001'): Promise<CartaoPf> {
  const pf = await criarPf(page, nome);
  await chamar(page, 'post', '/cartoes', pf.token, { bandeira });
  return { pf, dados: await chamar(page, 'get', '/cartoes/meu/dados', pf.token) };
}

export interface CobrancaEmitida {
  cobrancaId: string;
  codigo: string;
}

export function emitirCobranca(page: Page, pj: ContaComToken, valor = 5, descricao = 'Café especial'): Promise<CobrancaEmitida> {
  return chamar(page, 'post', '/cobrancas', pj.token, { valor, descricao });
}

export async function pagarCobranca(page: Page, { pf, dados }: CartaoPf, cobranca: CobrancaEmitida): Promise<void> {
  await chamar(
    page,
    'post',
    `/cobrancas/${cobranca.cobrancaId}/pagar`,
    pf.token,
    { nomeTitularCartao: dados.nomeImpresso, numeroCartao: dados.numero, validade: dados.validade, cvv: dados.cvv },
    randomUUID(),
  );
}

export async function comprar(page: Page, cartao: CartaoPf, pj: ContaComToken, valor = 5, descricao = 'Café especial'): Promise<CobrancaEmitida> {
  const cobranca = await emitirCobranca(page, pj, valor, descricao);
  await pagarCobranca(page, cartao, cobranca);
  return cobranca;
}

export async function saldoDe(page: Page, conta: ContaComToken): Promise<number> {
  return Number((await chamar<{ saldo: number | string }>(page, 'get', '/carteiras/minha', conta.token)).saldo);
}

export async function limiteDisponivel(page: Page, conta: ContaComToken): Promise<number> {
  return Number((await chamar<{ limiteDisponivel: number | string }>(page, 'get', '/cartoes/meu', conta.token)).limiteDisponivel);
}

export async function faturasDe(page: Page, conta: ContaComToken): Promise<{ competencia: string; situacao: string }[]> {
  return (await chamar<{ itens: { competencia: string; situacao: string }[] }>(page, 'get', '/faturas', conta.token)).itens;
}

const DATAS = {
  // Dez dias de atraso, dois meses atrás.
  Vencida: { mes: 2, inicio: 75, fechamento: 45, vencimento: -10 },
  // Vence em cinco dias, no mês passado.
  Disponivel: { mes: 1, inicio: 35, fechamento: 4, vencimento: 5 },
} as const;

/**
 * Antedata a fatura aberta da pessoa e a deixa liberada, com as compras lançadas. Tudo numa transação e já com
 * `liberada_em`: o worker do Compose nunca a vê antedatada sem liberação (research R13).
 */
export function prepararFatura(email: string, situacao: keyof typeof DATAS): string {
  const { mes, inicio, fechamento, vencimento } = DATAS[situacao];
  const hoje = `(now() AT TIME ZONE 'America/Sao_Paulo')::date`;
  const sql = `
    BEGIN;
    CREATE TEMP TABLE alvo ON COMMIT DROP AS
      SELECT f.id FROM faturas f JOIN cartoes c ON c.id = f.cartao_id JOIN usuarios u ON u.id = c.usuario_id
      WHERE u.email = '${email}' AND f.liberada_em IS NULL;
    INSERT INTO lancamentos_fatura (id, fatura_id, compra_id, estabelecimento, descricao, valor, data_compra, lancado_em)
      SELECT uuidv7(), k.fatura_id, k.id, k.estabelecimento, k.descricao, k.valor, k.data_compra, now()
      FROM compras_cartao k WHERE k.fatura_id IN (SELECT id FROM alvo);
    UPDATE faturas SET
      competencia = (date_trunc('month', ${hoje}) - interval '${mes} months')::date,
      inicio = ${hoje} - ${inicio}, fechamento = ${hoje} - ${fechamento}, vencimento = ${hoje} + (${vencimento}),
      total = (SELECT sum(valor) FROM compras_cartao k WHERE k.fatura_id = faturas.id), liberada_em = now()
      WHERE id IN (SELECT id FROM alvo);
    SELECT to_char(competencia, 'YYYY-MM') FROM faturas WHERE id IN (SELECT id FROM alvo);
    COMMIT;`;
  const saida = execFileSync('docker', ['compose', 'exec', '-T', 'postgres', 'sh', '-c', 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -Atq'], {
    cwd: '../..',
    input: sql,
    encoding: 'utf8',
  }).trim();
  expect(saida, 'uma fatura preparada').toMatch(/^\d{4}-\d{2}$/);
  return saida;
}
