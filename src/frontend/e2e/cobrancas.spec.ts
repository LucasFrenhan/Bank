import { expect, test } from '@playwright/test';
import { entrar } from './apoio';
import { criarPfComCartao, criarPj, pagarCobranca, emitirCobranca } from './apoioCartao';

// Spec 015 US2 — emitir e acompanhar cobranças.
test.describe('Cobranças', () => {
  test('emite, copia o link, filtra, abre e cancela', async ({ page, context, baseURL }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    const pj = await criarPj(page);
    await entrar(page, pj);
    await page.getByRole('link', { name: 'Cobranças' }).filter({ visible: true }).click();
    await expect(page.getByText('Nenhuma cobrança ainda')).toBeVisible();
    await page.getByRole('link', { name: 'Nova cobrança' }).first().click();

    await page.getByRole('button', { name: 'Emitir cobrança' }).click();
    await expect(page.getByText('Informe a descrição da cobrança')).toBeVisible();
    await page.getByLabel('Valor').fill('1.299,90');
    await page.getByLabel('Descrição').fill('Curso de barista');
    await page.getByRole('button', { name: 'Emitir cobrança' }).click();

    const link = page.getByRole('region', { name: 'Link de pagamento' });
    await expect(link).toBeVisible();
    await page.getByRole('button', { name: 'Copiar link' }).click();
    await expect(page.getByText('Link copiado')).toBeVisible();
    const copiado = await page.evaluate(() => navigator.clipboard.readText());
    expect(copiado).toMatch(new RegExp(`^${baseURL}/c/[2-9A-HJKMNP-TV-Z]{4}-[2-9A-HJKMNP-TV-Z]{4}$`));
    expect(page.url()).toBe(copiado);

    await page.getByRole('link', { name: 'Voltar às cobranças' }).click();
    await page.getByRole('button', { name: 'Pagas' }).click();
    await expect(page.getByText('Nenhuma cobrança nesta situação')).toBeVisible();
    await page.getByRole('button', { name: 'Pendentes' }).click();
    await expect(page).toHaveURL(/situacao=pendentes/);
    await page.getByRole('link', { name: /Curso de barista/ }).click();

    await page.getByRole('button', { name: 'Cancelar cobrança' }).click();
    const confirmacao = page.getByRole('region', { name: 'Cancelar esta cobrança?' });
    await page.keyboard.press('Escape');
    await expect(confirmacao).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Cancelar cobrança' })).toBeFocused();
    await page.getByRole('button', { name: 'Cancelar cobrança' }).click();
    await confirmacao.getByRole('button', { name: 'Cancelar cobrança' }).click();
    await expect(page.getByText('Cancelada', { exact: true })).toBeVisible();
    await expect(link).toHaveCount(0);
  });

  test('o detalhe aberto muda para "Paga" em até 10 segundos', async ({ page }) => {
    const pj = await criarPj(page);
    const cartao = await criarPfComCartao(page);
    const cobranca = await emitirCobranca(page, pj, 5, 'Encomenda 218');
    await entrar(page, pj);
    await page.goto(`/c/${cobranca.codigo}`);
    await expect(page.getByText('Pendente', { exact: true })).toBeVisible();

    await pagarCobranca(page, cartao, cobranca);
    await expect(page.getByText('Paga', { exact: true })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText(`Visa final ${cartao.dados.numero.slice(-4)}`)).toBeVisible();
    await expect(page.locator('body')).not.toContainText('Lucas Cartão');
    await page.getByRole('link', { name: 'Ver recebíveis' }).click();
    await expect(page.getByText('Encomenda 218')).toBeVisible();
  });
});
