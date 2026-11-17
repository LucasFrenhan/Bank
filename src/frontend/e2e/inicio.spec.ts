import { expect, test } from '@playwright/test';
import { criarContaPelaApi, entrar, SENHA, semViolacoesA11y } from './apoio';

// US3 — tela inicial PF e PJ com a carteira real (FR-014 a FR-016; SC-005).
test.describe('Início', () => {
  test('PF: saldo da API em até 2 s após o login, ações "Em breve" e axe', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await page.goto('/entrar');
    await page.getByLabel('E-mail').fill(conta.email);
    await page.getByLabel('Senha', { exact: true }).fill(SENHA);

    const inicio = Date.now();
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByText(/R\$\s10,00/)).toBeVisible();
    expect(Date.now() - inicio).toBeLessThanOrEqual(2000);

    const saldo = page.getByRole('region', { name: 'Saldo disponível' });
    await expect(saldo.getByRole('button', { name: /Transferir/ })).toHaveAttribute('aria-disabled', 'true');
    await expect(saldo.getByRole('button', { name: /Ver extrato/ })).toHaveAttribute('aria-disabled', 'true');
    await expect(page.getByText('Suas movimentações aparecerão aqui')).toBeVisible();
    await semViolacoesA11y(page);
  });

  test('PJ: sem ação de transferir e com o aviso da RN-06', async ({ page }) => {
    const conta = await criarContaPelaApi(page, 'PessoaJuridica');
    await entrar(page, conta);

    await expect(page.getByRole('heading', { name: 'Empresa Exemplo Ltda' })).toBeVisible();
    await expect(page.getByText(/R\$\s0,00/)).toBeVisible();
    await expect(page.getByRole('main').getByRole('button', { name: /Transferir/ })).toHaveCount(0);
    await semViolacoesA11y(page);
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
