import { expect, test, type Locator } from '@playwright/test';
import {
  contaBancariaPelaApi,
  criarContaPelaApi,
  entrar,
  mascararCnpj,
  SENHA,
  semRolagemHorizontal,
  semViolacoesA11y,
  tokenPelaApi,
  transferirPelaApi,
  type Tipo,
} from './apoio';

// Spec 004 US3 e spec 007 US5 — tela inicial PF e PJ com a carteira e as movimentações reais.
test.describe('Início', () => {
  test('PF: saldo da API em até 2 s após o login, ações ativas, saldo inicial nas movimentações e axe', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await page.goto('/entrar');
    await page.getByLabel('E-mail').fill(conta.email);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA);

    const inicio = Date.now();
    await page.getByRole('button', { name: 'Entrar' }).click();
    const saldo = page.getByRole('region', { name: 'Saldo disponível' });
    await expect(saldo.getByText(/R\$\s10,00/)).toBeVisible();
    expect(Date.now() - inicio).toBeLessThanOrEqual(2000);

    await expect(saldo.getByRole('link', { name: /Transferir/ })).toHaveAttribute('href', '/transferir');
    await expect(saldo.getByRole('link', { name: /Ver extrato/ })).toHaveAttribute('href', '/extrato');
    const movimentacoes = page.getByRole('region', { name: 'Últimas movimentações' });
    await expect(movimentacoes.getByText('Saldo inicial', { exact: true })).toBeVisible();
    await expect(page).toHaveTitle('Início · One Bank');
    await expect(movimentacoes.getByText(/^Saldo inicial de demonstração · Hoje, \d{2}:\d{2}$/)).toBeVisible();
    await semViolacoesA11y(page);
  });

  test('PJ: sem ação de transferir e com o aviso da RN-06', async ({ page }) => {
    const conta = await criarContaPelaApi(page, 'PessoaJuridica');
    await entrar(page, conta);

    await expect(page.getByRole('heading', { name: 'Empresa Exemplo Ltda' })).toBeVisible();
    await expect(page.getByText(/R\$\s0,00/)).toBeVisible();
    await expect(page.getByRole('main').getByRole('button', { name: /Transferir/ })).toHaveCount(0);
    await expect(page.getByText(mascararCnpj(conta.documento))).toBeVisible();
    await semViolacoesA11y(page);
  });

  test('PJ: "Copiar" grava o CNPJ sem máscara e confirma', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    const conta = await criarContaPelaApi(page, 'PessoaJuridica');
    await entrar(page, conta);

    await page.getByRole('button', { name: 'Copiar CNPJ' }).click();

    await expect(page.getByRole('status').filter({ hasText: 'CNPJ copiado' })).toBeVisible();
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(conta.documento);
  });

  test('o valor exibido é o da API ao recarregar', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();

    const token = await page.evaluate(() => (JSON.parse(sessionStorage.getItem('bank.sessao')!) as { token: string }).token);
    const carteira = (await (await page.request.get('/api/v1/carteiras/minha', { headers: { Authorization: `Bearer ${token}` } })).json()) as { saldo: number };
    await page.reload();

    const esperado = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(carteira.saldo);
    await expect(page.getByRole('region', { name: 'Saldo disponível' })).toContainText(esperado.replace(/\s/g, ' '));
  });
});

// Spec 007 US5 — navegação ativa e listas reais depois de transferências.
test.describe('Início — movimentações e navegação (spec 007, US5)', () => {
  test('PF vê a transferência enviada; PJ vê o recebimento e "Recebimentos" abre o extrato filtrado', async ({ page, isMobile }) => {
    const pf = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Inicio');
    const pj = await criarContaPelaApi(page, 'PessoaJuridica', 'Livraria Página Ltda');
    await transferirPelaApi(page, await tokenPelaApi(page, pf), pj.documento, 3.5, 'Livros');

    await entrar(page, pf);
    const ultimas = page.getByRole('region', { name: 'Últimas movimentações' });
    await expect(ultimas.getByText('Livraria Página Ltda')).toBeVisible();
    await expect(ultimas.getByText(/^Transferência enviada · Hoje, \d{2}:\d{2}$/)).toBeVisible();
    await expect(ultimas.getByText(/−\sR\$\s3,50/)).toBeVisible();

    const menu = page.getByRole('navigation', { name: isMobile ? 'Menu principal (celular)' : 'Menu principal' });
    await expect(menu.getByRole('link', { name: 'Extrato' })).toBeVisible();
    await expect(menu.getByRole('link', { name: 'Transferir' })).toHaveCount(isMobile ? 0 : 1);
    await ultimas.getByRole('link', { name: 'Ver tudo' }).click();
    await expect(page).toHaveURL(/\/extrato$/);

    await page.evaluate(() => sessionStorage.clear()); // troca de conta sem depender do menu de cada layout
    await entrar(page, pj);
    const recebimentos = page.getByRole('region', { name: 'Últimos recebimentos' });
    await expect(recebimentos.getByText('Lucas Inicio')).toBeVisible();
    await expect(recebimentos.getByText(/^Livros · Hoje, \d{2}:\d{2}$/)).toBeVisible();

    const menuPj = page.getByRole('navigation', { name: isMobile ? 'Menu principal (celular)' : 'Menu principal' });
    await menuPj.getByRole('link', { name: 'Recebimentos' }).click();
    await expect(page).toHaveURL(/\/extrato\?direcao=entradas$/);
    await expect(page.getByRole('button', { name: 'Entradas' })).toHaveAttribute('aria-pressed', 'true');
    await expect(menuPj.getByText('Cobranças')).toBeVisible();
  });
});

// Spec 013 — dados bancários da própria conta, iguais aos da API, em desktop e mobile.
test.describe('Início — dados da conta (spec 013)', () => {
  async function caixa(alvo: Locator) {
    const retangulo = await alvo.boundingBox();
    expect(retangulo).not.toBeNull();
    return retangulo!;
  }

  for (const tipo of ['PessoaFisica', 'PessoaJuridica'] as Tipo[]) {
    test(`${tipo}: valores da API, posição do cartão e axe`, async ({ page, isMobile }) => {
      const pj = tipo === 'PessoaJuridica';
      const conta = await criarContaPelaApi(page, tipo);
      const dados = await contaBancariaPelaApi(page, conta);
      expect(dados.numero).toMatch(/^\d{8}$/);
      await entrar(page, conta);

      const cartao = page.getByRole('region', { name: 'Dados da conta' });
      await expect(cartao.getByText(`${dados.banco.codigo} – ${dados.banco.nome}`, { exact: true })).toBeVisible();
      await expect(cartao.getByText(dados.agencia, { exact: true })).toBeVisible();
      await expect(cartao.getByText(`${dados.numero}-${dados.digito}`, { exact: true })).toBeVisible();
      await expect(cartao.getByText('Conta corrente', { exact: true })).toBeVisible();
      await expect(cartao.getByText(`Para receber de clientes One Bank, informe o seu ${pj ? 'CNPJ' : 'CPF'}.`)).toBeVisible();
      if (pj) {
        await expect(page.getByRole('button', { name: 'Copiar CNPJ' })).toBeVisible();
      }

      const saldo = await caixa(page.getByRole('region', { name: 'Saldo disponível' }));
      const doCartao = await caixa(cartao);
      const movimentacoes = await caixa(page.getByRole('region', { name: pj ? 'Últimos recebimentos' : 'Últimas movimentações' }));
      expect(doCartao.y + doCartao.height).toBeLessThanOrEqual(movimentacoes.y);
      if (isMobile) {
        expect(doCartao.y).toBeGreaterThanOrEqual(saldo.y + saldo.height);
      } else {
        expect(doCartao.x).toBeGreaterThanOrEqual(saldo.x + saldo.width);
      }
      if (pj) {
        const receber = await caixa(page.locator('section', { has: page.getByRole('heading', { name: 'Receber transferências' }) }));
        expect(doCartao.y).toBeGreaterThanOrEqual(receber.y + receber.height);
        expect(Math.abs(doCartao.x - receber.x)).toBeLessThan(1);
      }

      await semRolagemHorizontal(page);
      await semViolacoesA11y(page);
    });
  }

  test('nome de banco longo não causa rolagem horizontal em 320 px', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    const nome = `${'Banco'.repeat(12)} de Nome Comprido`;
    await page.route('**/api/v1/carteiras/minha', async (rota) => {
      const resposta = await rota.fetch();
      const corpo = (await resposta.json()) as { contaBancaria: { banco: { nome: string } } };
      corpo.contaBancaria.banco.nome = nome;
      await rota.fulfill({ response: resposta, json: corpo });
    });
    await page.setViewportSize({ width: 320, height: 800 });
    await entrar(page, conta);

    await expect(page.getByRole('region', { name: 'Dados da conta' }).getByText(`001 – ${nome}`)).toBeVisible();
    await semRolagemHorizontal(page);
  });
});
