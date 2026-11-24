import { expect, test } from '@playwright/test';
import { entrar, extratoPelaApi } from './apoio';
import { comprar, criarPfComCartao, criarPj, saldoDe } from './apoioCartao';

// Spec 015 US6 — recebíveis e antecipação.
test.describe('Recebíveis', () => {
  test('antecipa R$ 299,90 com taxa de R$ 6,00, uma só vez', async ({ page }) => {
    const pj = await criarPj(page);
    await comprar(page, await criarPfComCartao(page), pj, 299.9, 'Kit 3 Pacotes');
    await entrar(page, pj);
    await page.goto('/cobrancas');
    await page.getByRole('link', { name: 'Recebíveis' }).click();
    await page.getByRole('button', { name: 'Antecipar Kit 3 Pacotes' }).click();

    const confirmacao = page.getByRole('region', { name: 'Antecipar este recebível' });
    await expect(confirmacao).toContainText(/Taxa \(2%\)\s*− R\$\s6,00/);
    await expect(confirmacao).toContainText(/Você recebe agora\s*R\$\s293,90/);
    await confirmacao.getByRole('button', { name: /^Receber R\$\s293,90$/ }).dblclick();
    await expect(page.getByText('Antecipado', { exact: true })).toBeVisible();

    expect(await saldoDe(page, pj)).toBe(293.9);
    const entradas = await extratoPelaApi(page, pj, 'Entrada');
    expect(entradas.itens.filter((i) => i.tipo === 'AntecipacaoRecebivel')).toHaveLength(1);
    await page.getByRole('button', { name: 'Antecipados' }).click();
    await expect(page.getByText(/menos taxa de R\$\s6,00/)).toBeVisible();
    await page.getByRole('button', { name: 'Pendentes' }).click();
    await expect(page.getByText('Nenhum recebível nesta situação')).toBeVisible();
  });

  test('antecipado em outra aba: explica e atualiza', async ({ page, context }) => {
    const pj = await criarPj(page);
    await comprar(page, await criarPfComCartao(page), pj, 100, 'Moedor');
    await entrar(page, pj);
    await page.goto('/cobrancas/recebiveis');
    await page.getByRole('button', { name: 'Antecipar Moedor' }).click();

    const sessao = await page.evaluate(() => sessionStorage.getItem('bank.sessao'));
    const outra = await context.newPage();
    await outra.addInitScript((s) => sessionStorage.setItem('bank.sessao', s!), sessao);
    await outra.goto('/cobrancas/recebiveis');
    await outra.getByRole('button', { name: 'Antecipar Moedor' }).click();
    await outra.getByRole('button', { name: /^Receber R\$/ }).click();
    await expect(outra.getByText('Antecipado', { exact: true })).toBeVisible();

    await page.getByRole('button', { name: /^Receber R\$/ }).click();
    await expect(page.getByText('Este recebível não pode mais ser antecipado')).toBeVisible();
    await expect(page.getByText('Antecipado', { exact: true })).toBeVisible();
    expect(await saldoDe(page, pj)).toBe(98);
  });
});
