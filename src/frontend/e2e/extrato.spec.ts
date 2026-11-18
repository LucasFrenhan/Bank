import { expect, test } from '@playwright/test';
import { acumularSaldo, criarContaPelaApi, criarLancamentos, entrar } from './apoio';

/**
 * Extrato ponta a ponta contra a API real (specs/007-frontend-transferencias: US4; quickstart,
 * cenário 6). Desktop: tabela com páginas; mobile: lista agrupada com "Carregar mais".
 */

test.describe('Extrato (US4)', () => {
  test('PF recém-criada vê o saldo inicial de demonstração, e os filtros mudam o endereço', async ({ page }) => {
    const conta = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Extrato');
    await entrar(page, conta);

    const inicio = Date.now();
    await page.goto('/extrato');
    await expect(page.getByRole('heading', { level: 1, name: 'Extrato' })).toBeVisible();
    await expect(page.getByText(/Saldo inicial de demonstração/)).toBeVisible();
    expect(Date.now() - inicio, 'primeira página em até 2 s (SC-004)').toBeLessThanOrEqual(2000);
    await expect(page.getByText(/Saldo atual R\$\s10,00/)).toBeVisible();
    await expect(page.getByText('Saldo inicial', { exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Saídas' }).click();
    await expect(page).toHaveURL(/\/extrato\?direcao=saidas$/);
    await expect(page.getByText('Nenhuma movimentação ainda')).toBeVisible();

    await page.reload();
    await expect(page.getByRole('button', { name: 'Saídas' })).toHaveAttribute('aria-pressed', 'true');

    await page.getByRole('button', { name: 'Entradas' }).click();
    await expect(page.getByText(/Saldo inicial de demonstração/)).toBeVisible();
  });

  test('mais de 20 lançamentos: páginas no desktop e "Carregar mais" no mobile', async ({ page, isMobile }) => {
    const conta = await criarContaPelaApi(page, 'PessoaFisica', 'Lucas Paginas');
    const destino = await criarContaPelaApi(page, 'PessoaJuridica', 'Destino Paginas Ltda');
    await criarLancamentos(page, conta, destino, 21); // + saldo inicial = 22 lançamentos
    await entrar(page, conta);
    await page.goto('/extrato');

    if (isMobile) {
      await expect(page.getByRole('heading', { level: 2, name: 'Hoje' })).toBeVisible();
      await expect(page.getByText('Destino Paginas Ltda')).toHaveCount(20);
      await page.getByRole('button', { name: 'Carregar mais' }).click();
      await expect(page.getByText('Destino Paginas Ltda')).toHaveCount(21);
      await expect(page.getByText('Saldo inicial', { exact: true })).toBeVisible();
      await expect(page.getByRole('button', { name: 'Carregar mais' })).toHaveCount(0);
      await expect(page).toHaveURL(/\/extrato\?pagina=2$/);

      await page.reload();
      await expect(page.getByText('Destino Paginas Ltda')).toHaveCount(21);
    } else {
      await expect(page.getByText('Página 1 de 2 · 20 por página')).toBeVisible();
      await expect(page.getByRole('button', { name: 'Anterior' })).toBeDisabled();
      await page.getByRole('button', { name: 'Próxima' }).click();
      await expect(page).toHaveURL(/\/extrato\?pagina=2$/);
      await expect(page.getByText('Página 2 de 2 · 20 por página')).toBeVisible();
      await expect(page.getByRole('table', { name: 'Lançamentos' }).getByRole('row')).toHaveCount(3);
      await expect(page.getByRole('button', { name: 'Próxima' })).toBeDisabled();

      await page.reload();
      await expect(page.getByText('Página 2 de 2 · 20 por página')).toBeVisible();
    }
  });

  test('PJ vê as entradas recebidas', async ({ page }) => {
    const empresa = await criarContaPelaApi(page, 'PessoaJuridica', 'Empresa Recebe Ltda');
    await acumularSaldo(page, empresa, 1);
    await entrar(page, empresa);

    await page.goto('/extrato?direcao=entradas');
    await expect(page.getByText('Apoio 1 Silva')).toBeVisible();
    await expect(page.getByText(/\+\sR\$\s10,00/)).toBeVisible();
  });
});
