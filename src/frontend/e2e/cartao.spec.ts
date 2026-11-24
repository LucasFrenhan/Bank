import { expect, test } from '@playwright/test';
import { entrar } from './apoio';
import { criarPf, criarPfComCartao, emitirCobranca, criarPj } from './apoioCartao';

// Spec 015 US1 — solicitar e ver o cartão.
test.describe('Cartão', () => {
  test('solicita, vê os limites, exibe, copia e oculta os dados', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    const pf = await criarPf(page, 'Lucas Solicita');
    await entrar(page, pf);
    await page.getByRole('link', { name: 'Cartão' }).filter({ visible: true }).click();

    await expect(page.getByRole('heading', { name: 'Peça o seu cartão de crédito' })).toBeVisible();
    await page.getByRole('button', { name: 'Solicitar cartão' }).click();
    await expect(page.getByText('Escolha a bandeira', { exact: true }).last()).toBeVisible();
    await page.getByRole('radio', { name: 'Mastercard' }).check();
    await page.getByRole('button', { name: 'Solicitar cartão Mastercard' }).click();

    const cartao = page.getByRole('region', { name: 'Meu cartão' });
    await expect(cartao.getByText('Mastercard')).toBeVisible();
    await expect(cartao.getByText('LUCAS SOLICITA')).toBeVisible();
    await expect(page.getByRole('region', { name: 'Limite', exact: true })).toContainText('R$ 5.000,00');
    await expect(page.getByRole('region', { name: 'Fatura atual' })).toContainText('Aberta');

    await page.getByRole('button', { name: 'Exibir dados do cartão' }).click();
    await page.getByRole('button', { name: 'Copiar número' }).click();
    await expect(page.getByText('Número copiado')).toBeVisible();
    const numero = await page.evaluate(() => navigator.clipboard.readText());
    expect(numero).toMatch(/^5\d{15}$/);
    await expect(cartao.getByText(numero.replace(/(.{4})(?=.)/g, '$1 '))).toBeVisible();

    await page.getByRole('button', { name: 'Ocultar dados do cartão' }).click();
    await expect(cartao.getByText(`•••• •••• •••• ${numero.slice(-4)}`)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Copiar número' })).toHaveCount(0);

    // Sobreviver a recarregar não é permitido.
    await page.getByRole('button', { name: 'Exibir dados do cartão' }).click();
    await expect(page.getByRole('button', { name: 'Copiar número' })).toBeVisible();
    await page.reload();
    await expect(page.getByRole('button', { name: 'Exibir dados do cartão' })).toBeVisible();
    await expect(page.locator('body')).not.toContainText(numero.slice(0, 4) + ' ' + numero.slice(4, 8));
  });

  test('some ao sair da tela, e o cartão de outra aba é reconhecido', async ({ page, context }) => {
    const pf = await criarPf(page);
    await entrar(page, pf);
    await page.goto('/cartao');
    await page.getByRole('radio', { name: 'Visa' }).check();
    const sessao = await page.evaluate(() => sessionStorage.getItem('bank.sessao'));
    const outra = await context.newPage();
    await outra.addInitScript((s) => sessionStorage.setItem('bank.sessao', s!), sessao);
    await outra.goto('/cartao');
    await outra.getByRole('radio', { name: 'Visa' }).check();
    await outra.getByRole('button', { name: 'Solicitar cartão Visa' }).click();
    await expect(outra.getByRole('region', { name: 'Meu cartão' })).toBeVisible();

    await page.getByRole('button', { name: 'Solicitar cartão Visa' }).click();
    await expect(page.getByText('Você já tem um cartão')).toBeVisible();
    await expect(page.getByRole('region', { name: 'Meu cartão' })).toBeVisible();

    await page.getByRole('button', { name: 'Exibir dados do cartão' }).click();
    await expect(page.getByRole('button', { name: 'Copiar número' })).toBeVisible();
    await page.getByRole('link', { name: 'Ver faturas' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Faturas' })).toBeVisible();
    await page.goBack();
    await expect(page.getByRole('button', { name: 'Exibir dados do cartão' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Copiar número' })).toHaveCount(0);
  });

  test('abre a cobrança pelo código em minúsculas e sem hífen; PJ vê área indisponível', async ({ page }) => {
    const pj = await criarPj(page);
    const { codigo } = await emitirCobranca(page, pj);
    const { pf } = await criarPfComCartao(page);
    await entrar(page, pf);
    await page.goto('/cartao');
    const campo = page.getByLabel('Link ou código da cobrança');
    await campo.fill('nada');
    await page.getByRole('button', { name: 'Abrir' }).click();
    await expect(page.getByText('Confira o link ou o código')).toBeVisible();
    await campo.fill(codigo.replace('-', '').toLowerCase());
    await page.getByRole('button', { name: 'Abrir' }).click();
    await expect(page).toHaveURL(new RegExp(`/c/${codigo}$`));
    await expect(page.getByRole('heading', { name: 'Pagar cobrança' })).toBeVisible();

    await page.evaluate(() => sessionStorage.clear());
    await entrar(page, pj);
    await page.goto('/cartao');
    await expect(page.getByText('Área indisponível para o seu tipo de conta')).toBeVisible();
  });
});
