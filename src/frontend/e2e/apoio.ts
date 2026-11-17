import AxeBuilder from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';
import { randomInt, randomUUID } from 'node:crypto';

/**
 * Apoio aos testes ponta a ponta contra a API real (specs/004-frontend-base, research R3):
 * dados válidos e únicos por teste, sem depender de limpeza da base.
 */

export const SENHA = 'SenhaForte@123';

export type Tipo = 'PessoaFisica' | 'PessoaJuridica';

export function email(): string {
  return `e2e.${randomUUID().replace(/-/g, '')}@exemplo.com.br`;
}

export function cpf(): string {
  const d = Array.from({ length: 9 }, () => randomInt(10));
  if (new Set(d).size === 1) d[0] = (d[0]! + 1) % 10;
  for (const n of [9, 10]) {
    const soma = d.reduce((acc, v, i) => acc + v * (n + 1 - i), 0);
    const resto = soma % 11;
    d.push(resto < 2 ? 0 : 11 - resto);
  }
  return d.join('');
}

/** CNPJ alfanumérico válido (IN RFB nº 2.229/2024). */
export function cnpj(): string {
  const alfabeto = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';
  const c = Array.from({ length: 12 }, () => alfabeto[randomInt(alfabeto.length)]!);
  const pesos = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
  for (const n of [12, 13]) {
    const p = pesos.slice(pesos.length - n);
    const soma = c.reduce((acc, ch, i) => acc + (ch.charCodeAt(0) - 48) * p[i]!, 0);
    const resto = soma % 11;
    c.push(String(resto < 2 ? 0 : 11 - resto));
  }
  return c.join('');
}

export function mascararCpf(v: string): string {
  return `${v.slice(0, 3)}.${v.slice(3, 6)}.${v.slice(6, 9)}-${v.slice(9)}`;
}

export function mascararCnpj(v: string): string {
  return `${v.slice(0, 2)}.${v.slice(2, 5)}.${v.slice(5, 8)}/${v.slice(8, 12)}-${v.slice(12)}`;
}

export interface Conta {
  email: string;
  documento: string;
  tipo: Tipo;
  nome: string;
}

/** Cria a conta direto pela API (rápido); o fluxo pela tela é testado em cadastro.spec.ts. */
export async function criarContaPelaApi(page: Page, tipo: Tipo = 'PessoaFisica', nome?: string): Promise<Conta> {
  const conta: Conta = {
    email: email(),
    documento: tipo === 'PessoaFisica' ? cpf() : cnpj(),
    tipo,
    nome: nome ?? (tipo === 'PessoaFisica' ? 'Lucas Silva' : 'Empresa Exemplo Ltda'),
  };
  const resposta = await page.request.post('/api/v1/autenticacao/registrar', {
    data: { nomeCompleto: conta.nome, email: conta.email, documento: conta.documento, tipoConta: tipo, senha: SENHA },
  });
  expect(resposta.status()).toBe(201);
  return conta;
}

/** Login pela tela; com a senha correta, espera sair da tela de login. */
export async function entrar(page: Page, conta: Pick<Conta, 'email'>, senha = SENHA): Promise<void> {
  await page.goto('/entrar');
  await page.getByLabel('E-mail').fill(conta.email);
  await page.getByLabel('Senha', { exact: true }).fill(senha);
  await page.getByRole('button', { name: 'Entrar' }).click();
  if (senha === SENHA) {
    await expect(page).not.toHaveURL(/\/entrar/);
    await expect(page.getByRole('main').getByRole('heading', { level: 1 })).toBeVisible();
  }
}

export async function semRolagemHorizontal(page: Page): Promise<void> {
  const excesso = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(excesso, 'a página não pode rolar na horizontal').toBeLessThanOrEqual(0);
}

/** WCAG 2.2 A/AA sem violações (FR-001a, SC-006). */
export async function semViolacoesA11y(page: Page): Promise<void> {
  const resultado = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']).analyze();
  const resumo = resultado.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`);
  expect(resumo).toEqual([]);
}
