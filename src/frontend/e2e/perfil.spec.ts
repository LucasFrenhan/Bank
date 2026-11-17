import { expect, test } from '@playwright/test';
import { criarContaPelaApi, entrar, semViolacoesA11y } from './apoio';

// US4 — perfil (parte independente da issue #18).
test.describe('Perfil', () => {
  test('PF: dados, tipo não alterável e permissões', async ({ page }) => {
    const conta = await criarContaPelaApi(page);
    await entrar(page, conta);
    await page.goto('/perfil');

    await expect(page.getByRole('heading', { name: 'Lucas Silva' })).toBeVisible();
    await expect(page.getByText(conta.email)).toBeVisible();
    await expect(page.getByText('Pessoa Física · não pode ser alterado')).toBeVisible();
    await expect(page.getByText('Enviar transferências para PF e PJ')).toBeVisible();
    await semViolacoesA11y(page);
  });

  test('PJ: permissões de receber e gerar cobranças', async ({ page }) => {
    const conta = await criarContaPelaApi(page, 'PessoaJuridica');
    await entrar(page, conta);
    await page.goto('/perfil');

    await expect(page.getByText('Gerar cobranças de cartão de crédito')).toBeVisible();
    await expect(page.getByText('Enviar transferências para PF e PJ')).toHaveCount(0);
    await semViolacoesA11y(page);
  });
});
