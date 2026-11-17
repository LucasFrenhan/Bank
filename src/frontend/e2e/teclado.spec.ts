import { expect, test } from '@playwright/test';
import { cpf, email, SENHA } from './apoio';

// US5 / FR-001a — cadastro, login e saída só com teclado; foco visível; erros anunciados.
test.describe('Teclado', () => {
  test.skip(({ isMobile }) => isMobile, 'navegação por teclado verificada no desktop');

  test('cadastro com erro, correção, saída e login só pelo teclado', async ({ page }) => {
    const conta = { email: email(), documento: cpf() };
    await page.goto('/criar-conta');

    // Envio vazio: foco no primeiro campo inválido e erros anunciados.
    await page.getByRole('button', { name: 'Criar conta' }).focus();
    await page.keyboard.press('Enter');
    await expect(page.getByLabel('Nome completo')).toBeFocused();
    await expect(page.getByLabel('Nome completo')).toHaveAttribute('aria-invalid', 'true');

    await page.keyboard.type('Lucas Silva');
    await page.keyboard.press('Tab');
    await expect(page.getByLabel('CPF', { exact: true })).toBeFocused();
    await page.keyboard.type(conta.documento);
    await page.keyboard.press('Tab');
    await page.keyboard.type(conta.email);
    await page.keyboard.press('Tab');
    await page.keyboard.type(SENHA);
    await page.keyboard.press('Enter');

    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();

    // "Pular para o conteúdo" é a primeira parada e leva ao conteúdo principal.
    await page.keyboard.press('Tab');
    const pular = page.getByRole('link', { name: 'Pular para o conteúdo' });
    await expect(pular).toBeFocused();
    const contorno = await pular.evaluate((el) => getComputedStyle(el).outlineStyle);
    expect(contorno).not.toBe('none');

    // Sair pelo teclado (menu lateral).
    const sair = page.getByRole('navigation', { name: 'Menu principal', exact: true }).getByRole('button', { name: 'Sair' });
    for (let i = 0; i < 15 && !(await sair.evaluate((el) => el === document.activeElement)); i++) {
      await page.keyboard.press('Tab');
    }
    await expect(sair).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(/\/entrar$/);

    // Login pelo teclado.
    await page.getByLabel('E-mail').focus();
    await page.keyboard.type(conta.email);
    await page.keyboard.press('Tab');
    await page.keyboard.type(SENHA);
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'Olá, Lucas' })).toBeVisible();
  });
});
