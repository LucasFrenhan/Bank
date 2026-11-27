import { expect, test, type Locator, type Page } from '@playwright/test';
import { cpf, criarContaPelaApi, email, entrar, SENHA } from './apoio';

/** Avança com Tab até o elemento receber o foco (no máximo `limite` paradas). */
async function tabAte(page: Page, alvo: Locator, limite = 25): Promise<void> {
  for (let i = 0; i < limite && !(await alvo.evaluate((el) => el === document.activeElement)); i++) {
    await page.keyboard.press('Tab');
  }
  await expect(alvo).toBeFocused();
  const contorno = await alvo.evaluate((el) => getComputedStyle(el).outlineStyle);
  expect(contorno, 'foco visível').not.toBe('none');
}

// Spec 007 US6 / FR-028 — transferência e extrato só com teclado.
test.describe('Teclado — transferências e extrato', () => {
  test.skip(({ isMobile }) => isMobile, 'navegação por teclado verificada no desktop');

  test('transferência completa e filtros/páginas do extrato só pelo teclado', async ({ page }) => {
    const pf = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Teclado');
    const pj = await criarContaPelaApi(page, 'PessoaJuridica', 'Padaria Teclado Ltda');
    await entrar(page, pf);
    await page.goto('/transferir');
    // A tela só aparece depois de a sessão ser recuperada (specs/016, FR-017).
    await expect(page.getByRole('heading', { level: 1, name: 'Transferir dinheiro' })).toBeVisible();

    await page.keyboard.press('Tab');
    await expect(page.getByRole('link', { name: 'Pular para o conteúdo' })).toBeFocused();
    const documento = page.getByLabel('CPF ou CNPJ de quem vai receber');
    await tabAte(page, documento);
    await page.keyboard.type(pj.documento);
    await expect(page.getByRole('group', { name: 'Destinatário' })).toContainText('Padaria Teclado Ltda');

    await page.keyboard.press('Tab');
    await expect(page.getByLabel('Valor')).toBeFocused();
    await page.keyboard.type('1,25');
    await tabAte(page, page.getByRole('button', { name: 'Continuar' }));
    await page.keyboard.press('Enter');

    await expect(page.getByRole('heading', { name: 'Confira antes de enviar' })).toBeVisible();
    await expect(page.locator('[aria-current="step"]')).toContainText('Confirmar');
    await tabAte(page, page.getByRole('button', { name: /^Transferir R\$/ }));
    await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();
    await tabAte(page, page.getByRole('link', { name: 'Voltar ao início' }));

    await page.goto('/extrato');
    await expect(page.getByText('Padaria Teclado Ltda')).toBeVisible();
    await tabAte(page, page.getByRole('button', { name: 'Saídas' }));
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(/\/extrato\?direcao=saidas$/);
    await expect(page.getByText('Saldo inicial', { exact: true })).toHaveCount(0);
    await page.keyboard.press('Space');
    await expect(page.getByRole('button', { name: 'Saídas' })).toHaveAttribute('aria-pressed', 'true');
  });
});

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

// Spec 013 US3 — copiar os dados da conta só pelo teclado.
test.describe('Teclado — dados da conta', () => {
  test.skip(({ isMobile }) => isMobile, 'navegação por teclado verificada no desktop');

  test('chega a "Copiar dados da conta" com Tab e copia com Enter', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await entrar(page, await criarContaPelaApi(page));

    const cartao = page.getByRole('region', { name: 'Dados da conta' });
    await tabAte(page, cartao.getByRole('button', { name: 'Copiar dados da conta' }));
    await page.keyboard.press('Enter');
    await expect(cartao.getByRole('status')).toHaveText('Dados da conta copiados');
  });
});
