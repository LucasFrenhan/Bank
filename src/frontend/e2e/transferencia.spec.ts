import { expect, test, type Page } from '@playwright/test';
import { acumularSaldo, cpf, criarContaPelaApi, entrar, extratoPelaApi, mascararCnpj, type Conta } from './apoio';

/**
 * Transferência ponta a ponta contra a API real (specs/007-frontend-transferencias: US1, US2, US3;
 * quickstart, cenários 3 a 5). Roda nos projetos desktop e mobile.
 */

const campoDocumento = (page: Page) => page.getByLabel('CPF ou CNPJ de quem vai receber');

async function preencherDados(page: Page, documento: string, nomeEsperado: string, valor: string, descricao = '') {
  await page.goto('/transferir');
  await expect(page.getByRole('heading', { level: 1, name: 'Transferir dinheiro' })).toBeVisible();
  await campoDocumento(page).fill(documento);
  await campoDocumento(page).blur();
  // SC-004: titular em até 2 s após o documento completo.
  await expect(page.getByRole('group', { name: 'Destinatário' }).getByText(nomeEsperado)).toBeVisible({ timeout: 2000 });
  await page.getByLabel('Valor').fill(valor);
  if (descricao) await page.getByLabel('Descrição (opcional)').fill(descricao);
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page).toHaveURL(/\/transferir\/confirmar$/);
}

test.describe('Transferir (US1)', () => {
  test('PF → PJ com CNPJ alfanumérico em minúsculas, comprovante e saldo atualizado', async ({ page }) => {
    const pagador = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Pagador');
    const recebedor = await criarContaPelaApi(page, 'PessoaJuridica', 'Padaria Boa Massa Ltda');
    await entrar(page, pagador);

    await page.goto('/transferir');
    await campoDocumento(page).pressSequentially(recebedor.documento.toLowerCase());
    await expect(campoDocumento(page)).toHaveValue(mascararCnpj(recebedor.documento));
    const titular = page.getByRole('group', { name: 'Destinatário' });
    await expect(titular.getByText('Padaria Boa Massa Ltda')).toBeVisible({ timeout: 2000 });
    await expect(titular.getByText(/Pessoa Jurídica · CNPJ ••\.•••\./)).toBeVisible();

    await page.getByLabel('Valor').fill('4,50');
    await page.getByLabel('Descrição (opcional)').fill('Café');
    await page.getByRole('button', { name: 'Continuar' }).click();

    await expect(page.getByRole('heading', { name: 'Confira antes de enviar' })).toBeVisible();
    const resumo = page.getByRole('list', { name: 'Resumo da transferência' });
    await expect(resumo.getByText(/R\$\s5,50/)).toBeVisible();

    await page.getByRole('button', { name: /^Transferir R\$\s4,50$/ }).click();
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();
    await expect(page.getByText(/R\$\s4,50 enviados para Padaria Boa Massa Ltda\./)).toBeVisible();
    await expect(page.getByText(/^Lucas Pagador · CPF •••\./)).toBeVisible();

    await page.getByRole('link', { name: 'Voltar ao início' }).click();
    await expect(page.getByText(/R\$\s5,50/).first()).toBeVisible();

    const extratoRecebedor = await extratoPelaApi(page, recebedor, 'Entrada');
    expect(extratoRecebedor.itens.map((i) => i.valor)).toEqual([4.5]);
  });

  test('PF → PF pelo CPF', async ({ page }) => {
    const pagador = await criarContaPelaApi(page, 'PessoaFisica', 'Ana Pagadora');
    const recebedor = await criarContaPelaApi(page, 'PessoaFisica', 'Mariana Costa');
    await entrar(page, pagador);

    await preencherDados(page, recebedor.documento, 'Mariana Costa', '1');
    await page.getByRole('button', { name: /^Transferir R\$/ }).click();

    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();
    await expect(page.getByText(/Mariana Costa · CPF •••\./)).toBeVisible();
  });
});

test.describe('Recusas (US2)', () => {
  let pagador: Conta;

  test.beforeEach(async ({ page }) => {
    pagador = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Recusado');
    await entrar(page, pagador);
    await page.goto('/transferir');
  });

  test('CPF sem conta → erro no campo', async ({ page }) => {
    await campoDocumento(page).fill(cpf());
    await campoDocumento(page).blur();

    await expect(page.getByText('Nenhuma conta Bank com esse documento.')).toBeVisible();
    await expect(campoDocumento(page)).toHaveAttribute('aria-invalid', 'true');
  });

  test('próprio documento → "Esse documento é o seu"', async ({ page }) => {
    await campoDocumento(page).fill(pagador.documento);
    await campoDocumento(page).blur();

    await expect(page.getByRole('alert')).toContainText('Esse documento é o seu');
  });

  test('valor acima do saldo e com 3 casas decimais', async ({ page }) => {
    const recebedor = await criarContaPelaApi(page, 'PessoaJuridica');
    await campoDocumento(page).fill(recebedor.documento);
    await expect(page.getByRole('group', { name: 'Destinatário' })).toBeVisible();

    await page.getByLabel('Valor').fill('10,01');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByRole('alert')).toContainText(/Saldo insuficiente.*Você tem R\$\s10,00 disponíveis/);

    await page.getByLabel('Valor').fill('10,005');
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByText('Use no máximo 2 casas decimais')).toBeVisible();
    await expect(page).toHaveURL(/\/transferir$/);
  });
});

test('PJ abrindo /transferir vê "Indisponível para Pessoa Jurídica"', async ({ page }) => {
  const empresa = await criarContaPelaApi(page, 'PessoaJuridica');
  await entrar(page, empresa);

  await page.goto('/transferir');
  await expect(page.getByText('Indisponível para Pessoa Jurídica')).toBeVisible();
  await expect(campoDocumento(page)).toHaveCount(0);
});

test.describe('Reenvio sem cobrar duas vezes (US3)', () => {
  test('voltar à confirmação e recarregar não cobram de novo; duplo clique gera um único débito', async ({ page }) => {
    const pagador = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Reenvio');
    const recebedor = await criarContaPelaApi(page, 'PessoaJuridica', 'Oficina Rápida ME');
    await acumularSaldo(page, pagador, 1);
    await entrar(page, pagador);

    await preencherDados(page, recebedor.documento, 'Oficina Rápida ME', '2,00');
    await page.getByRole('button', { name: /^Transferir R\$/ }).click();
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();

    // Voltar pelo navegador até a confirmação e enviar de novo.
    await page.goBack();
    await expect(page).toHaveURL(/\/transferir\/confirmar$/);
    await page.getByRole('button', { name: /^Transferir R\$/ }).click();
    await expect(page.getByText('Essa transferência já foi feita')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Ver comprovante' })).toBeVisible();

    // Recarregar a confirmação e enviar de novo.
    await page.reload();
    await page.getByRole('button', { name: /^Transferir R\$/ }).click();
    await expect(page.getByText('Essa transferência já foi feita')).toBeVisible();

    // Nova transferência com duplo clique.
    await preencherDados(page, recebedor.documento, 'Oficina Rápida ME', '3,00');
    await page.getByRole('button', { name: /^Transferir R\$/ }).dblclick();
    await expect(page.getByRole('heading', { name: 'Transferência concluída' })).toBeVisible();

    const saidas = await extratoPelaApi(page, pagador, 'Saida');
    expect(saidas.itens.map((i) => i.valor)).toEqual([3, 2]);
  });
});

test.describe('Limites (specs/009-limite-transferencia, US4)', () => {
  test('a dica do valor mostra o limite agora (padrão R$ 1.000,00 por transferência)', async ({ page }) => {
    const pagador = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Limite');
    await entrar(page, pagador);

    await page.goto('/transferir');

    // O saldo de demonstração (R$ 10,00) não alcança o limite padrão; a recusa fica nos testes de componente.
    await expect(page.getByText(/Disponível: R\$\s10,00 · Limite agora: R\$\s1\.000,00 · mínimo R\$\s0,01/)).toBeVisible();
  });
});
