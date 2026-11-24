import { expect, test } from '@playwright/test';
import { acumularSaldo, entrar, extratoPelaApi } from './apoio';
import { comprar, criarPfComCartao, criarPj, emitirCobranca, faturasDe, limiteDisponivel, prepararFatura, saldoDe } from './apoioCartao';

// Spec 015 US4 e US5 — consultar e pagar faturas. Datas preparadas por SQL (research R13).
test.describe('Faturas', () => {
  test('aberta do ciclo, sem a compra de hoje e sem aviso no início', async ({ page }) => {
    const pj = await criarPj(page);
    const cartao = await criarPfComCartao(page);
    await comprar(page, cartao, pj);
    await entrar(page, cartao.pf);
    await expect(page.getByRole('region', { name: 'Aviso de fatura' })).toHaveCount(0);

    await page.goto('/cartao/faturas');
    await page.getByRole('region', { name: 'Lista de faturas' }).getByRole('link').first().click();
    await expect(page.getByText('Nenhum lançamento ainda.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Pagar fatura' })).toHaveCount(0);
    await page.goto('/cartao/faturas/2020-01');
    await expect(page.getByText('Fatura não encontrada')).toBeVisible();
  });

  test('disponível: aviso no início, detalhe e pagamento com saldo', async ({ page }) => {
    const pj = await criarPj(page);
    const cartao = await criarPfComCartao(page);
    await comprar(page, cartao, pj, 6.5, 'Grãos');
    const competencia = prepararFatura(cartao.pf.email, 'Disponivel');
    await entrar(page, cartao.pf);

    const aviso = page.getByRole('region', { name: 'Aviso de fatura' });
    await expect(aviso).toContainText(/R\$\s6,50, vence em/);
    await aviso.getByRole('link', { name: 'Ver fatura' }).click();
    await expect(page).toHaveURL(new RegExp(`/cartao/faturas/${competencia}$`));
    await expect(page.getByRole('region', { name: 'Lançamentos' })).toContainText('Cafeteria Grão Ltda');

    await page.getByRole('button', { name: 'Pagar fatura' }).click();
    const confirmacao = page.getByRole('region', { name: 'Confira antes de pagar' });
    await expect(confirmacao).toContainText(/Saldo depois\s*R\$\s3,50/);
    await confirmacao.getByRole('button', { name: /^Pagar R\$\s6,50$/ }).dblclick();
    await expect(page.getByText('Paga', { exact: true })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Resumo' })).toContainText(/Valor pago\s*R\$\s6,50/);

    expect(await saldoDe(page, cartao.pf)).toBe(3.5);
    expect(await limiteDisponivel(page, cartao.pf)).toBe(5000);
    const saidas = await extratoPelaApi(page, cartao.pf, 'Saida');
    expect(saidas.itens.filter((i) => i.tipo === 'PagamentoFatura')).toHaveLength(1);
    await page.goto('/');
    await expect(page.getByText('Pagamento de fatura')).toBeVisible();
    await expect(aviso).toHaveCount(0);
  });

  test('vencida: bloqueio, juros, saldo insuficiente e pagamento', async ({ page }) => {
    const pj = await criarPj(page);
    const cartao = await criarPfComCartao(page);
    await comprar(page, cartao, pj, 10, 'Curso');
    const competencia = prepararFatura(cartao.pf.email, 'Vencida');
    expect((await faturasDe(page, cartao.pf)).find((f) => f.competencia === competencia)?.situacao).toBe('Vencida');
    await entrar(page, cartao.pf);
    await expect(page.getByRole('region', { name: 'Aviso de fatura' })).toContainText(/Fatura vencida — R\$\s10,50 hoje\./);

    await page.goto(`/cartao/faturas/${competencia}`);
    await expect(page.getByText('Fatura vencida há 10 dias')).toBeVisible();
    await expect(page.getByRole('region', { name: 'Resumo' })).toContainText(/Juros \(10 dias de atraso\)\s*R\$\s0,50/);
    await page.getByRole('button', { name: 'Pagar fatura' }).click();
    const confirmacao = page.getByRole('region', { name: 'Confira antes de pagar' });
    await expect(confirmacao).toContainText(/Você tem R\$\s10,00 e a fatura é de R\$\s10,50\./);
    await expect(confirmacao.getByRole('button', { name: /^Pagar R\$/ })).toBeDisabled();

    await acumularSaldo(page, cartao.pf, 1);
    await page.reload();
    await page.getByRole('button', { name: 'Pagar fatura' }).click();
    await confirmacao.getByRole('button', { name: /^Pagar R\$\s10,50$/ }).click();
    await expect(page.getByRole('region', { name: 'Resumo' })).toContainText(/Valor pago\s*R\$\s10,50/);
    expect(await saldoDe(page, cartao.pf)).toBe(9.5);
    expect(await limiteDisponivel(page, cartao.pf)).toBe(5000);
  });

  test('pagamento recusado por fatura vencida leva às faturas', async ({ page }) => {
    const pj = await criarPj(page);
    const cartao = await criarPfComCartao(page);
    await comprar(page, cartao, pj, 1);
    prepararFatura(cartao.pf.email, 'Vencida');
    const { codigo } = await emitirCobranca(page, pj, 2);
    await entrar(page, cartao.pf);
    await page.goto(`/c/${codigo}`);
    await page.getByRole('button', { name: 'Usar meu cartão' }).click();
    await page.getByLabel('CVV').fill(cartao.dados.cvv);
    await page.getByRole('button', { name: /^Pagar R\$/ }).click();
    await expect(page.getByRole('alert')).toContainText('Cartão bloqueado por fatura vencida');
    await page.getByRole('link', { name: 'Ver faturas' }).click();
    await expect(page.getByText('Vencida', { exact: true })).toBeVisible();
  });
});
