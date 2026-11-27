import { expect, test } from '@playwright/test';
import { criarContaPelaApi, encerrarSessaoDoNavegador, entrar, tokenPelaApi, transferirPelaApi } from './apoio';

/**
 * Reabrir o comprovante (specs/007-frontend-transferencias: FR-014, US4 cenário 9; consulta da
 * spec 008). Quickstart da spec 007, cenário 7. Desktop e mobile.
 */
test.describe('Comprovante reaberto (fase 10)', () => {
  test('pelo extrato, pelo endereço em outra sessão, como recebedor e por um terceiro', async ({ page }) => {
    const pagador = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Reabre');
    const recebedor = await criarContaPelaApi(page, 'PessoaJuridica', 'Padaria Reabre Ltda');
    const terceiro = await criarContaPelaApi(page, 'PessoaFisica', 'Carlos Terceiro');
    // Feita pela API: o comprovante não está guardado em nenhuma aba.
    const { transferenciaId } = await transferirPelaApi(page, await tokenPelaApi(page, pagador), recebedor.documento, 3.25, 'Pão');
    const endereco = `/transferencias/${transferenciaId}`;

    // 1. Pagador abre pelo extrato.
    await entrar(page, pagador);
    await page.goto('/extrato');
    await page.getByRole('link', { name: /Padaria Reabre Ltda/ }).click();
    await expect(page).toHaveURL(new RegExp(`${endereco}$`));
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();
    await expect(page.getByText(/^Lucas Reabre · CPF •••\.\d{3}\.\d{3}-••$/)).toBeVisible();
    await expect(page.getByText(transferenciaId)).toBeVisible();

    // 2. Pelo endereço em outra sessão: o login leva de volta ao comprovante.
    await encerrarSessaoDoNavegador(page);
    await page.goto(endereco);
    await expect(page).toHaveURL(/\/entrar$/);
    await page.getByLabel('E-mail').fill(pagador.email);
    await page.getByLabel('Senha', { exact: true }).fill('SenhaForte@123');
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page).toHaveURL(new RegExp(`${endereco}$`));
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();

    // 3. Recebedor (PJ): "Transferência recebida", com o CPF do pagador mascarado.
    await encerrarSessaoDoNavegador(page);
    await entrar(page, recebedor);
    await page.goto('/extrato?direcao=entradas');
    await page.getByRole('link', { name: /Lucas Reabre/ }).click();
    await expect(page.getByRole('heading', { name: 'Transferência recebida' })).toBeVisible();
    await expect(page.getByText(/R\$\s3,25 recebidos de Lucas Reabre\./)).toBeVisible();
    await expect(page.getByText(/^Lucas Reabre · CPF •••\.\d{3}\.\d{3}-••$/)).toBeVisible();

    // 4. Terceiro: "Comprovante não encontrado", sem revelar a transferência.
    await encerrarSessaoDoNavegador(page);
    await entrar(page, terceiro);
    await page.goto(endereco);
    await expect(page.getByText('Comprovante não encontrado')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Ir para o extrato' })).toBeVisible();
    await expect(page.getByText('Padaria Reabre Ltda')).toHaveCount(0);
  });
});
